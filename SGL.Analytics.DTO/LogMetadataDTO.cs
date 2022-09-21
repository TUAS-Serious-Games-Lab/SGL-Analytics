using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Validation;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Represents the different supported content encodings that can be used for an analytics log file.
	/// </summary>
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum LogContentEncoding {
		/// <summary>
		/// The log file content is uploaded as plain text JSON.
		/// </summary>
		Plain = 2,
		/// <summary>
		/// The content as it would be for <see cref="Plain"/>, but compressed using the gzip algorithm.
		/// </summary>
		GZipCompressed = 1
	}

	/// <summary>
	/// Specifies the metadata transferred along whith the contents when a client upload a game log file to the server.
	/// These properties are passed by custom headers, as the request body is already taken by the file content.
	/// </summary>
	public class LogMetadataDTO {
		/// <summary>
		/// The id of the uploaded log file on the client device.
		/// </summary>
		public Guid LogFileId { get; private set; }
		/// <summary>
		/// The time when recording of the log file was started.
		/// </summary>
		public DateTime CreationTime { get; private set; }
		/// <summary>
		/// The time when recording of the log file was ended.
		/// </summary>
		public DateTime EndTime { get; private set; }
		/// <summary>
		/// The file name suffix for the log file as specified by the client application.
		/// </summary>
		[PlainName]
		[StringLength(16)]
		public string NameSuffix { get; private set; }
		/// <summary>
		/// The encoding of the log file content as specified by the client application.
		/// This specifies the inner encoding inside the encryption.
		/// </summary>
		public LogContentEncoding LogContentEncoding { get; private set; }

		public EncryptionInfo EncryptionInfo { get; private set; }

		/// <summary>
		/// Creates a new DTO with the given data.
		/// </summary>
		/// <param name="logFileId">The id of the uploaded log file on the client device.</param>
		/// <param name="creationTime">The time when recording of the log file was started.</param>
		/// <param name="endTime">The time when recording of the log file was ended.</param>
		/// <param name="nameSuffix">The file name suffix for the log file as specified by the client application.</param>
		/// <param name="logContentEncoding">The encoding of the log file content as specified by the client application.</param>
		[JsonConstructor]
		public LogMetadataDTO(Guid logFileId, DateTime creationTime, DateTime endTime,
			[PlainName][StringLength(16)] string nameSuffix, LogContentEncoding logContentEncoding, EncryptionInfo encryptionInfo) =>
			(LogFileId, CreationTime, EndTime, NameSuffix, LogContentEncoding, EncryptionInfo) = (logFileId, creationTime, endTime, nameSuffix, logContentEncoding, encryptionInfo);

		[Obsolete("Use overload with encryption infos.")]
		public LogMetadataDTO(Guid logFileId, DateTime creationTime, DateTime endTime, [PlainName][StringLength(16)] string nameSuffix, LogContentEncoding logContentEncoding) : this(logFileId, creationTime, endTime, nameSuffix, logContentEncoding, EncryptionInfo.CreateUnencrypted()) { }

		/// <summary>
		/// Deconstructs the DTO into the contained data.
		/// </summary>
		/// <param name="logFileId">The id of the uploaded log file on the client device.</param>
		/// <param name="creationTime">The time when recording of the log file was started.</param>
		/// <param name="endTime">The time when recording of the log file was ended.</param>
		/// <param name="nameSuffix">The file name suffix for the log file as specified by the client application.</param>
		/// <param name="logContentEncoding">The encoding of the log file content as specified by the client application.</param>
		public void Deconstruct(out Guid logFileId, out DateTime creationTime, out DateTime endTime,
			out string nameSuffix, out LogContentEncoding logContentEncoding, out EncryptionInfo encryptionInfo) {
			logFileId = LogFileId;
			creationTime = CreationTime;
			endTime = EndTime;
			nameSuffix = NameSuffix;
			logContentEncoding = LogContentEncoding;
			encryptionInfo = EncryptionInfo;
		}
	}
}
