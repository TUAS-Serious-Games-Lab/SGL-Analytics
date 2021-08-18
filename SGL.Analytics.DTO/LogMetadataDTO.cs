using System;
using System.Collections.Generic;
using System.Text;

namespace SGL.Analytics.DTO {
	public class LogMetadataDTO {
		public string AppName { get; set; } = "";
		public Guid UserId { get; set; }
		public Guid LogFileId { get; set; }
		public DateTime CreationTime { get; set; }
		public DateTime EndTime { get; set; }

	}
}
