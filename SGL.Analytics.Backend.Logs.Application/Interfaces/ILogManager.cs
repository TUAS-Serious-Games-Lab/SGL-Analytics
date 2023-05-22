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
	/// Specifies the interface for high-level log file manager classes that coordinates storing the actual log content and updating the associated metadata.
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

		Task<IEnumerable<LogFile>> ListLogsAsync(string appName, KeyId? recipientKeyId, string exporterDN, CancellationToken ct = default);
		Task<LogFile> GetLogByIdAsync(Guid logId, string appName, KeyId? recipientKeyId, string exporterDN, CancellationToken ct = default);
		Task AddRekeyedKeysAsync(string appName, KeyId newRecipientKeyId, Dictionary<Guid, DataKeyInfo> dataKeys, string exporterDN, CancellationToken ct = default);
	}
}
