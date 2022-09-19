using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGL.Analytics.DTO {
	public class DownstreamLogMetadataDTO : LogMetadataDTO {
		/// <summary>
		/// The time when the log file was uploaded.
		/// </summary>
		public DateTime UploadTime { get; private set; }

		public DownstreamLogMetadataDTO(Guid logFileId, DateTime creationTime, DateTime endTime, DateTime uploadTime, [PlainName(false), StringLength(16)] string nameSuffix, LogContentEncoding logContentEncoding, EncryptionInfo encryptionInfo) : base(logFileId, creationTime, endTime, nameSuffix, logContentEncoding, encryptionInfo) {
			UploadTime = uploadTime;
		}
	}
}
