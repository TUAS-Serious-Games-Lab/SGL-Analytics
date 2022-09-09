using SGL.Analytics.DTO;
using SGL.Utilities.Crypto.EndToEnd;
using System;
using System.Collections.Generic;
using System.Linq;

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
		public byte[] InitializationVector { get; set; }
		/// <summary>
		/// The encryption mode used for the log file data.
		/// </summary>
		public DataEncryptionMode EncryptionMode { get; set; }
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
			DateTime creationTime, DateTime endTime, DateTime uploadTime, string filenameSuffix, LogContentEncoding encoding, long? size, byte[] initializationVector,
			DataEncryptionMode encryptionMode, byte[]? sharedLogPublicKey = null, bool complete = false) {
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
			InitializationVector = initializationVector;
			EncryptionMode = encryptionMode;
			SharedLogPublicKey = sharedLogPublicKey;
			Complete = complete;
		}

		public static LogMetadata Create(Guid id, Application app, Guid userId, Guid localLogId, DateTime creationTime, DateTime endTime, DateTime uploadTime, string filenameSuffix,
				LogContentEncoding encoding, long? size, EncryptionInfo encryptionInfo, bool complete = false) {
			var metadata = new LogMetadata(id, app.Id, userId, localLogId, creationTime, endTime, uploadTime, filenameSuffix, encoding, size, encryptionInfo.IVs.Single(),
				encryptionInfo.DataMode, sharedLogPublicKey: encryptionInfo.MessagePublicKey, complete: complete);
			metadata.App = app;
			metadata.RecipientKeys = new List<LogRecipientKey>();
			metadata.EncryptionInfo = encryptionInfo;
			return metadata;
		}

		public EncryptionInfo EncryptionInfo {
			get {
				return new EncryptionInfo {
					DataMode = EncryptionMode,
					IVs = new List<byte[]> { InitializationVector },
					MessagePublicKey = SharedLogPublicKey,
					DataKeys = RecipientKeys.ToDictionary(lrk => lrk.RecipientKeyId, lrk => lrk.ToDataKeyInfo())
				};
			}
			set {
				EncryptionMode = value.DataMode;
				InitializationVector = value.IVs.Single();
				SharedLogPublicKey = value.MessagePublicKey;
				var currentKeys = RecipientKeys.ToDictionary(lrk => lrk.RecipientKeyId);

				RecipientKeys.Where(rk => !value.DataKeys.ContainsKey(rk.RecipientKeyId)).ToList().ForEach(rk => RecipientKeys.Remove(rk));
				foreach (var rk in RecipientKeys) {
					var newValues = value.DataKeys[rk.RecipientKeyId];
					rk.EncryptedKey = newValues.EncryptedKey;
					rk.EncryptionMode = newValues.Mode;
					rk.LogPublicKey = newValues.MessagePublicKey;
				}
				value.DataKeys.Where(pair => !currentKeys.ContainsKey(pair.Key)).Select(pair => new LogRecipientKey {
					LogId = Id,
					RecipientKeyId = pair.Key,
					EncryptionMode = pair.Value.Mode,
					EncryptedKey = pair.Value.EncryptedKey,
					LogPublicKey = pair.Value.MessagePublicKey
				}).ToList().ForEach(k => RecipientKeys.Add(k));
			}
		}
	}
}
