using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;

namespace SGL.Analytics.Backend.Domain.Entity {
	/// <summary>
	/// Models the metadata entry for an analytics log file.
	/// </summary>
	public class LogMetadata {
		/// <summary>
		/// The unique id of the analytics log file.
		/// </summary>
		public Guid Id { get; set; }
		/// <summary>
		/// The id of the application from which the log originates.
		/// </summary>
		public Guid AppId { get; set; }
		/// <summary>
		/// The application from which the log originates.
		/// </summary>
		public Application App { get; set; } = null!;
		/// <summary>
		/// The id of the user that uploaded the log.
		/// </summary>
		public Guid UserId { get; set; }
		/// <summary>
		/// The id of the log as orignially indicated by the client.
		/// </summary>
		/// <remarks>
		/// This is usually identical to <see cref="Id"/>, except when an id collision happens between users.
		/// While this is astronomically unlikely under normal circumstances, we still need to handle this case cleanly by assigning a new <see cref="Id"/>,
		/// because problems or user interference on the client side may lead to duplicate ids, e.g. a user copying files from one installation to another with a different user id.
		/// </remarks>
		public Guid LocalLogId { get; set; }
		/// <summary>
		/// The time the log was started on the client.
		/// </summary>
		public DateTime CreationTime { get; set; }
		/// <summary>
		/// The time when the recording of the log on the client ended.
		/// </summary>
		public DateTime EndTime { get; set; }
		/// <summary>
		/// If <see cref="Complete"/> is <see langword="true"/>, the time when the upload was completed, or,
		/// if <see cref="Complete"/> is <see langword="false"/>, the time when the upload was started.
		/// </summary>
		public DateTime UploadTime { get; set; }
		/// <summary>
		/// The suffix to use for the log file name.
		/// </summary>
		public string FilenameSuffix { get; set; }
		/// <summary>
		/// The encoding used for the file content.
		/// </summary>
		public LogContentEncoding Encoding { get; set; }
		/// <summary>
		/// The size of the content of the log file.
		/// </summary>
		public long? Size { get; set; }
		/// <summary>
		/// Indicates whether the log was uploaded completely.
		/// If this is <see langword="false"/>, it may indicate, that the upload is still running or that it was interrupted and may be reattempted.
		/// </summary>
		public bool Complete { get; set; }

		/// <summary>
		/// Contains the initialization vector for the encryption if the log is encrypted, otherwise null.
		/// </summary>
		public byte[]? InitializationVector { get; set; }
		/// <summary>
		/// If the log is encrypted and uses a shared per-log public key for ECDH, stores this key, otherwise null.
		/// </summary>
		public byte[]? SharedLogPublicKey { get; set; }
		/// <summary>
		/// If the log is encrypted, contains recipient key objects storing the copies of the data key encrypted for each recipient.
		/// </summary>
		public ICollection<LogRecipientKey> RecipientKeys { get; set; } = null!;

		/// <summary>
		/// Constructs a LogMetadata with the given data values.
		/// </summary>
		public LogMetadata(Guid id, Guid appId, Guid userId, Guid localLogId,
			DateTime creationTime, DateTime endTime, DateTime uploadTime, string filenameSuffix, LogContentEncoding encoding, long? size, bool complete = false,
			byte[]? initializationVector = null, byte[]? sharedLogPublicKey = null) {
			Id = id;
			AppId = appId;
			UserId = userId;
			LocalLogId = localLogId;
			CreationTime = creationTime;
			EndTime = endTime;
			UploadTime = uploadTime;
			FilenameSuffix = filenameSuffix;
			Encoding = encoding;
			Size = size;
			Complete = complete;
			InitializationVector = initializationVector;
			SharedLogPublicKey = sharedLogPublicKey;
		}
	}
}
