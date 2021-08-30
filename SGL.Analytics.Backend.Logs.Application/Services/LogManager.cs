using Microsoft.Extensions.Logging;
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

		public async Task<LogFile> IngestLogAsync(LogMetadataDTO logMetaDTO, Stream logContent) {
			var app = await appRepo.GetApplicationByNameAsync(logMetaDTO.AppName);
			if (app is null) throw new ApplicationDoesNotExistException(logMetaDTO.AppName);
			var logMetadata = await logMetaRepo.GetLogMetadataByIdAsync(logMetaDTO.LogFileId);
			if (logMetadata is null) {
				logMetadata = new(logMetaDTO.LogFileId, app.Id, logMetaDTO.UserId, logMetaDTO.LogFileId, logMetaDTO.CreationTime, logMetaDTO.EndTime, DateTime.Now, LogFileSuffix, false);
				logMetadata.App = app;
				logger.LogInformation("Ingesting new log file {logId} from user {userId}.", logMetaDTO.LogFileId, logMetaDTO.UserId);
				logMetadata = await logMetaRepo.AddLogMetadataAsync(logMetadata);
			}
			else if (logMetadata.UserId != logMetaDTO.UserId) {
				var oldLogMetadata = logMetadata;
				logMetadata = new(Guid.NewGuid(), app.Id, logMetaDTO.UserId, logMetaDTO.LogFileId, logMetaDTO.CreationTime, logMetaDTO.EndTime, DateTime.Now, LogFileSuffix, false);
				logMetadata.App = app;
				logger.LogWarning("User {curUser} attempted to upload log file {origLog} which was already uploaded by user {otherUser}. Resolving this conflict by assigning a new log id {newLogId} for the new log file.", logMetaDTO.UserId, logMetaDTO.LogFileId, oldLogMetadata.UserId, logMetadata.Id);
				logMetadata = await logMetaRepo.AddLogMetadataAsync(logMetadata);
			}
			else if (logMetadata.Complete) {
				logger.LogWarning("User {user} uploads log {logId} again, although it was already marked as completely uploaded. Allowing them to proceed anyway.", logMetaDTO.UserId, logMetaDTO.LogFileId);
			}
			else {
				logger.LogInformation("Reattempted upload of logfile {logId} from user {userId}, time of original upload attempt: {uploadTime}.", logMetadata.Id, logMetadata.UserId, logMetadata.UploadTime);
				logMetadata.UploadTime = DateTime.Now;
				logMetadata = await logMetaRepo.UpdateLogMetadataAsync(logMetadata);
			}
			await logFileRepo.StoreLogAsync(logMetaDTO.AppName, logMetadata.UserId, logMetadata.Id, logMetadata.FilenameSuffix, logContent);
			logMetadata.Complete = true;
			logMetadata.UploadTime = DateTime.Now;
			logMetadata = await logMetaRepo.UpdateLogMetadataAsync(logMetadata);
			return new LogFile(logMetadata, logFileRepo);
		}
	}
}
