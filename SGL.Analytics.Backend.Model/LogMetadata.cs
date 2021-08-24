using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;

namespace SGL.Analytics.Backend.Model {
	[Index(nameof(AppId))]
	[Index(nameof(AppId), nameof(UserId))]
	public class LogMetadata {
		[Key]
		public Guid Id { get; set; }
		public int AppId { get; set; }
		public Application App { get; set; }
		public Guid UserId { get; set; }

		public DateTime CreationTime { get; set; }
		public DateTime EndTime { get; set; }

		public LogMetadata(Guid id, int appId, Application app, Guid userId, DateTime creationTime, DateTime endTime) {
			Id = id;
			AppId = appId;
			App = app;
			UserId = userId;
			CreationTime = creationTime;
			EndTime = endTime;
		}
	}
}
