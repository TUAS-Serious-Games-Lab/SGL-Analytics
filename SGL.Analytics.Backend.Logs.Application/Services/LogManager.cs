using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Application.Model;
using SGL.Analytics.DTO;
using SGL.Utilities.Backend.Applications;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Services {

	/// <summary>
	/// Implements the management logic for analytics log files and their metadata.
	/// </summary>
	public class LogManager : ILogManager {
		private IApplicationRepository<Domain.Entity.Application, ApplicationQueryOptions> appRepo;
		private ILogMetadataRepository logMetaRepo;
		private ILogFileRepository logFileRepo;
		private ILogger<LogManager> logger;
		private IMetricsManager metrics;

		/// <summary>
		/// Creates a <see cref="LogManager"/> using the given repository implementation objects, the given logger for diagnostics logging, and the given metrics manager.
		/// </summary>
		/// <param name="appRepo">The application repository to use.</param>
		/// <param name="logMetaRepo">The log metadata repository to use.</param>
		/// <param name="logFileRepo">the log file repository to use.</param>
		/// <param name="logger">A logger to log status, warning and error messages to.</param>
		/// <param name="metrics">A metrics manager to which metrics-relevant events are reported.</param>
		public LogManager(IApplicationRepository<Domain.Entity.Application, ApplicationQueryOptions> appRepo, ILogMetadataRepository logMetaRepo, ILogFileRepository logFileRepo, ILogger<LogManager> logger, IMetricsManager metrics) {
			this.appRepo = appRepo;
			this.logMetaRepo = logMetaRepo;
			this.logFileRepo = logFileRepo;
			this.logger = logger;
			this.metrics = metrics;
		}

		private void updateContentEncodingAndSuffix(LogMetadataDTO logMetaDTO, LogMetadata logMetadata, string appName) {
			if (logMetadata.FilenameSuffix != logMetaDTO.NameSuffix) {
				logger.LogWarning("Retried upload for log file {logId} from user {userId} uses a different file name sufix, which normally should not happen." +
					"The old suffix is '{oldSuffix}' and the new suffix is '{newSuffix}'.", logMetadata.Id, logMetadata.UserId, logMetadata.FilenameSuffix, logMetaDTO.NameSuffix);
				metrics.HandleUploadRetryChangedSuffixWarning(appName);
				logMetadata.FilenameSuffix = logMetaDTO.NameSuffix;
			}
			if (logMetadata.Encoding != logMetaDTO.LogContentEncoding) {
				logger.LogWarning("Retried upload for log file {logId} from user {userId} uses a different log content encoding, which normally should not happen." +
					"The old encoding is '{oldEncoding}' and the new encoding is '{newEncoding}'.", logMetadata.Id, logMetadata.UserId, logMetadata.Encoding.ToString(), logMetaDTO.LogContentEncoding.ToString());
				metrics.HandleUploadRetryChangedEncodingWarning(appName);
				logMetadata.Encoding = logMetaDTO.LogContentEncoding;
			}
		}

		/// <summary>
		/// Ingests the log file with the given metadata and content as described by <see cref="ILogManager.IngestLogAsync(Guid, string, string, LogMetadataDTO, Stream, CancellationToken)"/>.
		/// </summary>
		public async Task<LogFile> IngestLogAsync(Guid userId, string appName, string appApiToken, LogMetadataDTO logMetaDTO, Stream logContent, CancellationToken ct = default) {
			if (logMetaDTO.EncryptionInfo.DataMode != DataEncryptionMode.Unencrypted && !logMetaDTO.EncryptionInfo.DataKeys.Any()) {
				logger.LogError("Attempt to ingest a log file with id {logId} or app {appName} from user {user} with encryption mode {mode} without data keys, " +
					"but encrypted modes need data keys to be present in metadata for the data to be readable later. Refusing to accept the file that would be unreadable due to missing keys.",
					logMetaDTO.LogFileId, appName, userId, logMetaDTO.EncryptionInfo.DataMode);
				throw new MissingRecipientDataKeysForEncryptedDataException($"Attempt to ingest a log file with id {logMetaDTO.LogFileId} or app {appName} from user {userId} with " +
					$"encryption mode {logMetaDTO.EncryptionInfo.DataMode} without data keys, but encrypted modes need data keys to be present in metadata for the data to be readable later. " +
					"Refusing to accept the file that would be unreadable due to missing keys.");
			}
			var app = await appRepo.GetApplicationByNameAsync(appName, ct: ct);
			if (app is null) {
				logger.LogError("Attempt to ingest a log file with id {logId} for non-existent application {appName} from user {user}.", logMetaDTO.LogFileId, appName, userId);
				throw new ApplicationDoesNotExistException(appName);
			}
			else if (app.ApiToken != appApiToken) {
				logger.LogError("Attempt to ingest a log file with id {logId} for application {appName} using incorrect app API token {token} from user {user}.", logMetaDTO.LogFileId, appName, appApiToken, userId);
				throw new ApplicationApiTokenMismatchException(appName, appApiToken);
			}
			try {
				var queryOptions = new LogMetadataQueryOptions { ForUpdating = true, FetchRecipientKeys = true };
				var logMetadata = await logMetaRepo.GetLogMetadataByIdAsync(logMetaDTO.LogFileId, queryOptions, ct);
				if (logMetadata is null) {
					logMetadata = LogMetadata.Create(logMetaDTO.LogFileId, app, userId, logMetaDTO.LogFileId, logMetaDTO.CreationTime, logMetaDTO.EndTime, DateTime.Now,
						logMetaDTO.NameSuffix, logMetaDTO.LogContentEncoding, size: null, logMetaDTO.EncryptionInfo, complete: false);
					logger.LogInformation("Ingesting new log file {logId} from user {userId}.", logMetaDTO.LogFileId, userId);
					logMetadata = await logMetaRepo.AddLogMetadataAsync(logMetadata, ct);
				}
				else if (logMetadata.UserId != userId) {
					var otherLogMetadata = logMetadata;
					var oldLogMetadata = await logMetaRepo.GetLogMetadataByUserLocalIdAsync(app.Id, userId, logMetaDTO.LogFileId, queryOptions, ct);
					if (oldLogMetadata is null) {
						logMetadata = LogMetadata.Create(Guid.NewGuid(), app, userId, logMetaDTO.LogFileId, logMetaDTO.CreationTime, logMetaDTO.EndTime, DateTime.Now,
							logMetaDTO.NameSuffix, logMetaDTO.LogContentEncoding, size: null, logMetaDTO.EncryptionInfo, complete: false);
						logger.LogWarning("User {curUser} attempted to upload log file {origLog} which was already uploaded by user {otherUser}. " +
							"Resolving this conflict by assigning a new log id {newLogId} for the new log file.",
							userId, logMetaDTO.LogFileId, otherLogMetadata.UserId, logMetadata.Id);
						metrics.HandleLogIdConflictWarning(appName);
						logMetadata = await logMetaRepo.AddLogMetadataAsync(logMetadata, ct);
					}
					else {
						logMetadata = oldLogMetadata;
						if (logMetadata.Complete) {
							logger.LogWarning("User {curUser} attempted to upload log file {origLog} which was already uploaded by user {otherUser}. " +
								"This conflict was previously resolved by assigning the new log id {newLogId} for the new log file when an upload for it was attempted at {uploadTime:O}. " +
								"Although that attempt was already marked as complete, the client attempts another upload. Allowing it to proceed anyway. " +
								"This could happen if the server wrote the log to completion, but the client crashed / disconnected before it could receive the response and " +
								"remove the file from the upload list.",
								userId, logMetaDTO.LogFileId, otherLogMetadata.UserId, logMetadata.Id, logMetadata.UploadTime);
							metrics.HandleLogIdConflictWarning(appName);
							metrics.HandleRetryingCompletedUploadWarning(appName);
						}
						else {
							logger.LogWarning("User {curUser} attempted to upload log file {origLog} which was already uploaded by user {otherUser}. " +
								"This conflict was previously resolved by assigning the new log id {newLogId} for the new log file when an upload for it was attempted at {uploadTime:O}. " +
								"That upload however didn't complete and the client is now reattempting the upload.",
								userId, logMetaDTO.LogFileId, otherLogMetadata.UserId, logMetadata.Id, logMetadata.UploadTime);
							metrics.HandleLogIdConflictWarning(appName);
							metrics.HandleLogUploadRetryWarning(appName);
							updateContentEncodingAndSuffix(logMetaDTO, logMetadata, appName);
							logMetadata.UploadTime = DateTime.Now;
							logMetadata.Size = null;
							logMetadata.EncryptionInfo = logMetaDTO.EncryptionInfo;
							logMetadata = await logMetaRepo.UpdateLogMetadataAsync(logMetadata, ct);
						}
					}
				}
				else if (logMetadata.Complete) {
					logger.LogWarning("User {user} uploads log {logId} again, although it was already marked as completely uploaded. Allowing them to proceed anyway. " +
						"This could happen if the server wrote the log to completion, but the client crashed / disconnected before it could receive the response and " +
						"remove the file from the upload list.",
						userId, logMetaDTO.LogFileId);
					metrics.HandleRetryingCompletedUploadWarning(appName);
				}
				else {
					logger.LogInformation("Reattempted upload of logfile {logId} from user {userId}, time of original upload attempt: {uploadTime:O}.",
						logMetadata.Id, logMetadata.UserId, logMetadata.UploadTime);
					metrics.HandleLogUploadRetryWarning(appName);
					updateContentEncodingAndSuffix(logMetaDTO, logMetadata, appName);
					logMetadata.UploadTime = DateTime.Now;
					logMetadata.Size = null;
					logMetadata.EncryptionInfo = logMetaDTO.EncryptionInfo;
					logMetadata = await logMetaRepo.UpdateLogMetadataAsync(logMetadata, ct);
				}
				long storedSize = 0;
				try {
					storedSize = await logFileRepo.StoreLogAsync(appName, logMetadata.UserId, logMetadata.Id, logMetadata.FilenameSuffix, logContent, ct);
				}
				catch (IOException ex) {
					logger.LogError(ex, "Log transfer of logfile {logId} from user {userId} failed due to I/O error.", logMetadata.Id, logMetadata.UserId);
					throw;
				}
				if (logMetadata.Complete) {
					updateContentEncodingAndSuffix(logMetaDTO, logMetadata, appName);
					logMetadata.EncryptionInfo = logMetaDTO.EncryptionInfo;
				}
				logMetadata.Complete = true;
				logMetadata.UploadTime = DateTime.Now;
				logMetadata.Size = storedSize;
				logMetadata = await logMetaRepo.UpdateLogMetadataAsync(logMetadata, ct);
				logger.LogInformation("Successfully finished ingest of logfile {logId} from user {userId}.", logMetadata.Id, logMetadata.UserId);
				metrics.ObserveIngestedLogFileSize(appName, storedSize);
				return new LogFile(logMetadata, logFileRepo);
			}
			catch (OperationCanceledException) {
				logger.LogDebug("IngestLogAsync from user {userId} was cancelled.", userId);
				throw;
			}
			catch (Exception ex) {
				logger.LogError(ex, "Log file ingest of file {logId} from user {userId} failed due to exception.", logMetaDTO.LogFileId, userId);
				throw;
			}
		}

		public async Task<IEnumerable<LogFile>> ListLogsAsync(string appName, KeyId? recipientKeyId, string exporterDN, CancellationToken ct = default) {
			var app = await appRepo.GetApplicationByNameAsync(appName, ct: ct);
			if (app is null) {
				logger.LogError("Attempt to list logs from non-existent application {appName} for recipient {keyId} by exporter {dn}.", appName, recipientKeyId, exporterDN);
				throw new ApplicationDoesNotExistException(appName);
			}
			var queryOptions = new LogMetadataQueryOptions { ForUpdating = false, FetchRecipientKey = recipientKeyId };
			var logs = await logMetaRepo.ListLogMetadataForApp(app.Id, completenessFilter: true, notForKeyId: null, queryOptions: queryOptions, ct: ct);
			return logs.Select(log => new LogFile(log, logFileRepo)).ToList();
		}

		public async Task<LogFile> GetLogByIdAsync(Guid logId, string appName, KeyId? recipientKeyId, string exporterDN, CancellationToken ct = default) {
			var app = await appRepo.GetApplicationByNameAsync(appName, ct: ct);
			if (app is null) {
				logger.LogError("Attempt to retrieve log file with id {logId} from non-existent application {appName} for recipient {keyId} by exporter {dn}.", logId, appName, recipientKeyId, exporterDN);
				throw new ApplicationDoesNotExistException(appName);
			}
			var queryOptions = new LogMetadataQueryOptions { ForUpdating = false, FetchRecipientKey = recipientKeyId };
			var log = await logMetaRepo.GetLogMetadataByIdAsync(logId, queryOptions, ct);
			if (log == null) {
				logger.LogError("Attempt to retrieve non-existent log file with id {logId} from application {appName} for recipient {keyId} by exporter {dn}.", logId, appName, recipientKeyId, exporterDN);
				throw new LogNotFoundException($"The log {logId} was not found.", logId);
			}
			if (log.AppId != app.Id) {
				logger.LogError("Attempt to retrieve log file with id {logId} from application {appName} for recipient {keyId} by exporter {dn}, but the file actually belongs to application {actualAppName}.",
					logId, appName, recipientKeyId, exporterDN, log.App.Name);
				throw new LogNotFoundException($"The log {logId} was not found in application {appName}.", logId);
			}
			return new LogFile(log, logFileRepo);
		}

		public async Task AddRekeyedKeysAsync(string appName, KeyId newRecipientKeyId, Dictionary<Guid, DataKeyInfo> dataKeys, string exporterDN, CancellationToken ct = default) {
			var app = await appRepo.GetApplicationByNameAsync(appName, ct: ct);
			if (app is null) {
				logger.LogError("Attempt to upload rekeyed data keys for non-existent application {appName} for recipient {keyId} by exporter {dn}.", appName, newRecipientKeyId, exporterDN);
				throw new ApplicationDoesNotExistException(appName);
			}
			var queryOptions = new LogMetadataQueryOptions { ForUpdating = true, FetchRecipientKeys = true };
			var logs = (await logMetaRepo.ListLogMetadataForApp(app.Id, completenessFilter: true, notForKeyId: null, queryOptions: queryOptions, ct: ct)).ToList();
			logger.LogInformation("Putting {keyCount} rekeyed data keys for recipient {recipientKeyId} into matching logs out of {logCount} logs in application {appName} ...",
				dataKeys.Count, newRecipientKeyId, logs.Count, appName);
			using var logScope = logger.BeginScope("Rekey-Put {keyId}", newRecipientKeyId);
			var pendingIds = dataKeys.Keys.ToHashSet();
			foreach (var log in logs) {
				if (dataKeys.TryGetValue(log.Id, out var newDataKeyInfo)) {
					if (log.RecipientKeys.Any(rk => rk.RecipientKeyId == newRecipientKeyId)) {
						logger.LogWarning("Attempt to put rekeyed key for recipient {keyId} into log file {logId} that already has a data key for that recipient.",
							newRecipientKeyId, log.Id);
						pendingIds.Remove(log.Id);
					}
					else {
						log.RecipientKeys.Add(new LogRecipientKey {
							LogId = log.Id,
							RecipientKeyId = newRecipientKeyId,
							EncryptionMode = newDataKeyInfo.Mode,
							EncryptedKey = newDataKeyInfo.EncryptedKey,
							LogPublicKey = newDataKeyInfo.MessagePublicKey
						});
						logger.LogDebug("Put key for recipient {keyId} on log {logId}.", newRecipientKeyId, log.Id);
						pendingIds.Remove(log.Id);
					}
				}
				else {
					logger.LogWarning("No key for log {logId} and recipient {keyId} was provided.", log.Id, newRecipientKeyId);
				}
			}
			if (pendingIds.Count > 0) {
				logger.LogWarning("The following log ids given by the rekeying uploader were not present: {logIdList}", string.Join(", ", pendingIds));
			}
			await logMetaRepo.UpdateLogMetadataAsync(logs, ct);
			logger.LogInformation("... rekeying upload finished.");
		}

		public async Task<Dictionary<Guid, EncryptionInfo>> GetKeysForRekeying(string appName, KeyId recipientKeyId, KeyId targetKeyId, string exporterDN, CancellationToken ct = default) {
			var app = await appRepo.GetApplicationByNameAsync(appName, ct: ct);
			if (app is null) {
				logger.LogError("Attempt to list logs from non-existent application {appName} for recipient {keyId} by exporter {dn}.", appName, recipientKeyId, exporterDN);
				throw new ApplicationDoesNotExistException(appName);
			}
			var queryOptions = new LogMetadataQueryOptions { ForUpdating = false, FetchRecipientKey = recipientKeyId };
			var logs = await logMetaRepo.ListLogMetadataForApp(app.Id, completenessFilter: true, notForKeyId: targetKeyId, queryOptions: queryOptions, ct: ct);
			return logs.ToDictionary(log => log.Id, log => log.EncryptionInfo);
		}
	}
}
