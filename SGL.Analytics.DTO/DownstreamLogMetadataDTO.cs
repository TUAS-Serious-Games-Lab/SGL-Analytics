using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Validation;
using System;
using System.ComponentModel.DataAnnotations;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Specifies the log metadata that is passed from the backend to an exporter client when the client browses through log metadata for export.
	/// </summary>
	public class DownstreamLogMetadataDTO : LogMetadataDTO {
		/// <summary>
		/// The id of the user who uploaded the log file.
		/// </summary>
		public Guid UserId { get; private set; }
		/// <summary>
		/// The time when the log file was uploaded.
		/// </summary>
		public DateTime UploadTime { get; private set; }
		/// <summary>
		/// The size of the log file in bytes.
		/// </summary>
		public long? Size { get; private set; }

		/// <summary>
		/// Constructs a <see cref="DownstreamLogMetadataDTO"/> with the given data.
		/// </summary>
		public DownstreamLogMetadataDTO(Guid logFileId, Guid userId, DateTime creationTime, DateTime endTime, DateTime uploadTime, long? size,
				[PlainName(false), StringLength(16)] string nameSuffix, LogContentEncoding logContentEncoding, EncryptionInfo encryptionInfo) :
				base(logFileId, creationTime, endTime, nameSuffix, logContentEncoding, encryptionInfo) {
			UserId = userId;
			UploadTime = uploadTime;
			Size = size;
		}
	}
}
