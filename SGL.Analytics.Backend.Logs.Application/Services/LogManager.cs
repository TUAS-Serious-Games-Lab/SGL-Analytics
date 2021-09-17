using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Application.Model;
using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Services {
	public class LogManager : ILogManager {
		private IApplicationRepository appRepo;
		private ILogMetadataRepository logMetaRepo;
		private ILogFileRepository logFileRepo;
		private ILogger<LogManager> logger;

		public string LogFileSuffix { get; set; } = ".log.gz";

		public LogManager(IApplicationRepository appRepo, ILogMetadataRepository logMetaRepo, ILogFileRepository logFileRepo, ILogger<LogManager> logger) {
			this.appRepo = appRepo;
			this.logMetaRepo = logMetaRepo;
			this.logFileRepo = logFileRepo;
			this.logger = logger;
		}

		public async Task<LogFile> IngestLogAsync(Guid userId, string appName, LogMetadataDTO logMetaDTO, Stream logContent) {
			var app = await appRepo.GetApplicationByNameAsync(appName);
			if (app is null) {
				logger.LogError("Attempt to ingest a log file with id {logId} for non-existent application {appName} from user {user}.", logMetaDTO.LogFileId, appName, userId);
				throw new ApplicationDoesNotExistException(appName);
			}
			try {
				var logMetadata = await logMetaRepo.GetLogMetadataByIdAsync(logMetaDTO.LogFileId);
				if (logMetadata is null) {
					logMetadata = new(logMetaDTO.LogFileId, app.Id, userId, logMetaDTO.LogFileId, logMetaDTO.CreationTime, logMetaDTO.EndTime, DateTime.Now, LogFileSuffix, false);
					logMetadata.App = app;
					logger.LogInformation("Ingesting new log file {logId} from user {userId}.", logMetaDTO.LogFileId, userId);
					logMetadata = await logMetaRepo.AddLogMetadataAsync(logMetadata);
				}
				else if (logMetadata.UserId != userId) {
					var otherLogMetadata = logMetadata;
					var oldLogMetadata = await logMetaRepo.GetLogMetadataByUserLocalIdAsync(app.Id, userId, logMetaDTO.LogFileId);
					if (oldLogMetadata is null) {
						logMetadata = new(Guid.NewGuid(), app.Id, userId, logMetaDTO.LogFileId, logMetaDTO.CreationTime, logMetaDTO.EndTime, DateTime.Now, LogFileSuffix, false);
						logMetadata.App = app;
						logger.LogWarning("User {curUser} attempted to upload log file {origLog} which was already uploaded by user {otherUser}. " +
							"Resolving this conflict by assigning a new log id {newLogId} for the new log file.",
							userId, logMetaDTO.LogFileId, otherLogMetadata.UserId, logMetadata.Id);
						logMetadata = await logMetaRepo.AddLogMetadataAsync(logMetadata);
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
						}
						else {
							logger.LogWarning("User {curUser} attempted to upload log file {origLog} which was already uploaded by user {otherUser}. " +
								"This conflict was previously resolved by assigning the new log id {newLogId} for the new log file when an upload for it was attempted at {uploadTime:O}. " +
								"That upload however didn't complete and the client is now reattempting the upload.",
								userId, logMetaDTO.LogFileId, otherLogMetadata.UserId, logMetadata.Id, logMetadata.UploadTime);
							logMetadata.UploadTime = DateTime.Now;
							logMetadata = await logMetaRepo.UpdateLogMetadataAsync(logMetadata);
						}
					}
				}
				else if (logMetadata.Complete) {
					logger.LogWarning("User {user} uploads log {logId} again, although it was already marked as completely uploaded. Allowing them to proceed anyway. " +
						"This could happen if the server wrote the log to completion, but the client crashed / disconnected before it could receive the response and " +
						"remove the file from the upload list.",
						userId, logMetaDTO.LogFileId);
				}
				else {
					logger.LogInformation("Reattempted upload of logfile {logId} from user {userId}, time of original upload attempt: {uploadTime:O}.",
						logMetadata.Id, logMetadata.UserId, logMetadata.UploadTime);
					logMetadata.UploadTime = DateTime.Now;
					logMetadata = await logMetaRepo.UpdateLogMetadataAsync(logMetadata);
				}
				await logFileRepo.StoreLogAsync(appName, logMetadata.UserId, logMetadata.Id, logMetadata.FilenameSuffix, logContent);
				logMetadata.Complete = true;
				logMetadata.UploadTime = DateTime.Now;
				logMetadata = await logMetaRepo.UpdateLogMetadataAsync(logMetadata);
				return new LogFile(logMetadata, logFileRepo);
			}
			catch (Exception ex) {
				logger.LogError(ex, "Log file ingest of file {logId} from user {userId} failed due to exception.", logMetaDTO.LogFileId, userId);
				throw;
			}
		}
	}
}
