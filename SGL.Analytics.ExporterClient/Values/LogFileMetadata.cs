using SGL.Analytics.DTO;
using SGL.Utilities.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	/// <summary>
	/// Encapsulates the metadata about an exportable log file.
	/// </summary>
	public class LogFileMetadata {
		/// <summary>
		/// The id of the uploaded log file on the client device.
		/// </summary>
		public Guid LogFileId { get; private set; }
		/// <summary>
		/// The id of the user who uploaded the log file.
		/// </summary>
		public Guid UserId { get; private set; }
		/// <summary>
		/// The time when recording of the log file was started.
		/// </summary>
		public DateTime CreationTime { get; private set; }
		/// <summary>
		/// The time when recording of the log file was ended.
		/// </summary>
		public DateTime EndTime { get; private set; }
		/// <summary>
		/// The time when the log file was uploaded.
		/// </summary>
		public DateTime UploadTime { get; private set; }
		/// <summary>
		/// The file name suffix for the log file as specified by the client application.
		/// </summary>
		public string NameSuffix { get; private set; }
		/// <summary>
		/// The encoding of the log file content as specified by the client application.
		/// This specifies the inner encoding inside the encryption.
		/// </summary>
		public LogContentEncoding LogContentEncoding { get; private set; }
		/// <summary>
		/// The size of the log file in bytes.
		/// </summary>
		public long? Size { get; private set; }

		internal LogFileMetadata(Guid logFileId, Guid userId, DateTime creationTime, DateTime endTime, DateTime uploadTime, string nameSuffix, LogContentEncoding logContentEncoding, long? size) {
			LogFileId = logFileId;
			UserId = userId;
			CreationTime = creationTime;
			EndTime = endTime;
			UploadTime = uploadTime;
			NameSuffix = nameSuffix;
			LogContentEncoding = logContentEncoding;
			Size = size;
		}
	}
}
