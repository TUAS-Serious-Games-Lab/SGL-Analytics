using SGL.Analytics.Backend.Logs.Application.Model;
using SGL.Analytics.DTO;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Interfaces {
	/// <summary>
	/// Specifies the interface for high-level log file manager classes that manages log file data as <see cref="LogFile"/> objects and 
	/// for ingest operations coordinates storing the actual log content and updating the associated metadata.
	/// </summary>
	public interface ILogManager {
		/// <summary>
		/// Asynchronously ingests an analytics log with the given metadata and content.
		/// Implementations need to handle reattempted uploads, id conflicts between users, and updating the completion status.
		/// </summary>
		/// <param name="userId">The id of the uploading user.</param>
		/// <param name="appName">The technical name to identify the application with which the user and the log are associated.</param>
		/// <param name="appApiToken">The API token authenticating the client application.</param>
		/// <param name="logMetaDTO">A data transfer object containing the metadata of the log.</param>
		/// <param name="logContent">A stream containing the log contents to store.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the ingest operation, providing a <see cref="LogFile"/> for the log upon success.</returns>
		Task<LogFile> IngestLogAsync(Guid userId, string appName, string appApiToken, LogMetadataDTO logMetaDTO, Stream logContent, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously lists the log file data in the indicated application.
		/// </summary>
		/// <param name="appName">The unique name of the application for which to list log metadata.</param>
		/// <param name="recipientKeyId">The id for the recipient key pair, for which to include the corresponding encrypted data keys.</param>
		/// <param name="exporterDN">The distinguished name of the certificate used for authenticating the current user, for logging purposes.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the log file data.</returns>
		Task<IEnumerable<LogFile>> ListLogsAsync(string appName, KeyId? recipientKeyId, string exporterDN, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously retrieves the data of the log file with the given <paramref name="logId"/>.
		/// </summary>
		/// <param name="logId">The id of the log to retrieve.</param>
		/// <param name="appName">The unique name of the application for which to get log metadata.</param>
		/// <param name="recipientKeyId">The id for the recipient key pair, for which to include the corresponding encrypted data key.</param>
		/// <param name="exporterDN">The distinguished name of the certificate used for authenticating the current user, for logging purposes.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the log file data.</returns>
		Task<LogFile> GetLogByIdAsync(Guid logId, string appName, KeyId? recipientKeyId, string exporterDN, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously adds data keys that an exporter client has rekeyed to grant another recipient user access to log file data
		/// into their corresponding log file's encryption metadata.
		/// </summary>
		/// <param name="appName">The unique name of the application on which to operate.</param>
		/// <param name="newRecipientKeyId">The key id of the recipient key pair for which the rekeyed data keys grant access.</param>
		/// <param name="dataKeys">The data keys as a dictionary mapping from log ids to the corresponding new data keys.</param>
		/// <param name="exporterDN">The distinguished name of the certificate used for authenticating the current user, for logging purposes.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation.</returns>
		Task AddRekeyedKeysAsync(string appName, KeyId newRecipientKeyId, Dictionary<Guid, DataKeyInfo> dataKeys, string exporterDN, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously retrieves a chunk of encrypted data keys and encryption metadata for a rekeying operation.
		/// </summary>
		/// <param name="appName">The unique name of the application on which to operate.</param>
		/// <param name="recipientKeyId">
		/// The key id for the key-pair the user that performs the rekeying operation.
		/// This indicates that the data keys for this key pair need to be returned.
		/// </param>
		/// <param name="targetKeyId">
		/// The key id of the key-pair to which the operation grants access.
		/// Key material and metadata are returned only for logs that don't already have a data key for that key pair.
		/// </param>
		/// <param name="exporterDN">The distinguished name of the certificate used for authenticating the current user, for logging purposes.</param>
		/// <param name="offset">
		/// The number of log encryption data to skip at the start of the listing, when ordering by user id and start date.
		/// This is used for skipping entries that could not be rekeyed in previous iterations.
		/// </param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation.</returns>
		Task<Dictionary<Guid, EncryptionInfo>> GetKeysForRekeying(string appName, KeyId recipientKeyId, KeyId targetKeyId, string exporterDN, int offset, CancellationToken ct = default);
	}
}
