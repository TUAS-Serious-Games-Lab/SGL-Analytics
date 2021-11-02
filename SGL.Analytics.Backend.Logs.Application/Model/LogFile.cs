using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Model {
	public class LogFile {
		private LogMetadata metadata;
		private ILogFileRepository fileRepo;

		public LogFile(LogMetadata metadata, ILogFileRepository fileRepo) {
			this.metadata = metadata;
			this.fileRepo = fileRepo;
		}

		public Guid Id => metadata.Id;
		public Domain.Entity.Application App => metadata.App;
		public Guid UserId => metadata.UserId;
		public Guid LocalLogId => metadata.LocalLogId;
		public DateTime CreationTime => metadata.CreationTime;
		public DateTime EndTime => metadata.EndTime;
		public DateTime UploadTime => metadata.UploadTime;
		public string FilenameSuffix => metadata.FilenameSuffix;
		public bool Complete => metadata.Complete;

		public async Task<Stream> OpenReadAsync(CancellationToken ct = default) {
			return await fileRepo.ReadLogAsync(App.Name, UserId, Id, FilenameSuffix, ct);
		}
		public async Task CopyToAsync(Stream stream, CancellationToken ct = default) {
			await fileRepo.CopyLogIntoAsync(App.Name, UserId, Id, FilenameSuffix, stream, ct);
		}
	}
}
