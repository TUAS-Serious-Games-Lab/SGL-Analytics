using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Model {
	public class LogFile {
		private LogMetadata entity;
		private ILogFileRepository fileRepo;

		public LogFile(LogMetadata entity, ILogFileRepository fileRepo) {
			this.entity = entity;
			this.fileRepo = fileRepo;
		}

		public Guid Id => entity.Id;
		public Domain.Entity.Application App => entity.App;
		public Guid UserId => entity.UserId;
		public Guid LocalLogId => entity.LocalLogId;
		public DateTime CreationTime => entity.CreationTime;
		public DateTime EndTime => entity.EndTime;
		public DateTime UploadTime => entity.UploadTime;
		public string FilenameSuffix => entity.FilenameSuffix;
		public bool Complete => entity.Complete;

		public async Task<Stream> OpenReadAsync() {
			return await fileRepo.ReadLogAsync(App.Name, UserId, Id, FilenameSuffix);
		}
		public async Task CopyToAsync(Stream stream) {
			await fileRepo.CopyLogIntoAsync(App.Name, UserId, Id, FilenameSuffix, stream);
		}
	}
}