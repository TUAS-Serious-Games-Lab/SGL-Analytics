using System;
using System.ComponentModel.DataAnnotations;

namespace SGL.Analytics.Backend.Domain.Entity {
	public class LogMetadata {
		public Guid Id { get; set; }
		public Guid AppId { get; set; }
		public Application App { get; set; } = null!;
		public Guid UserId { get; set; }
		public Guid LocalLogId { get; set; }

		public DateTime CreationTime { get; set; }
		public DateTime EndTime { get; set; }
		public DateTime UploadTime { get; set; }
		public string FilenameSuffix { get; set; }
		public bool Complete { get; set; }

		public LogMetadata(Guid id, Guid appId, Guid userId, Guid localLogId,
			DateTime creationTime, DateTime endTime, DateTime uploadTime, string filenameSuffix, bool complete = false) {
			Id = id;
			AppId = appId;
			UserId = userId;
			LocalLogId = localLogId;
			CreationTime = creationTime;
			EndTime = endTime;
			UploadTime = uploadTime;
			Complete = complete;
			FilenameSuffix = filenameSuffix;
		}
	}
}