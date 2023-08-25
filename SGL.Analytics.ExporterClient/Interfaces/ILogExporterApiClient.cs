using Microsoft.Extensions.Logging;
using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {

	/// <summary>
	/// Specifies the interface for client implementations for interacting with the logs backend service.
	/// </summary>
	public interface ILogExporterApiClient : IApiClient {
		/// <summary>
		/// Asynchronously retrieves the list of the ids of the log files present in the application for which the client is authenticated.
		/// </summary>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A task providing an enumerable providing the ids.</returns>
		Task<IEnumerable<Guid>> GetLogIdListAsync(CancellationToken ct = default);
		/// <summary>
		/// Asynchronously retrieves the metadata of the log files present in the application for which the client is authenticated.
		/// </summary>
		/// <param name="recipientKeyId">Indicates the recipient key id for which the encrypted data keys shall be returned.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A task providing an enumerable providing the log metadata.</returns>
		Task<IEnumerable<DownstreamLogMetadataDTO>> GetMetadataForAllLogsAsync(KeyId? recipientKeyId = null, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously retrieves the metadata of the log file with the id indicated by <paramref name="id"/>.
		/// The log must belong the application for which the client is authenticated.
		/// </summary>
		/// <param name="id">The id of the log for which to retrieve the metadata.</param>
		/// <param name="recipientKeyId">Indicates the recipient key id for which the encrypted data key shall be returned.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A task providing the log metadata.</returns>
		Task<DownstreamLogMetadataDTO> GetLogMetadataByIdAsync(Guid id, KeyId? recipientKeyId = null, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously retrieves the content of the log file with the id indicated by <paramref name="id"/>.
		/// The log must belong the application for which the client is authenticated.
		/// </summary>
		/// <param name="id">The id of the log for which to retrieve the content.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A task providing a stream containing the content.</returns>
		Task<Stream> GetLogContentByIdAsync(Guid id, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously retrieves a chunk of encryption metadata of log files of the application for which the client is authenticated.
		/// These <see cref="EncryptionInfo"/>s are intended for rekeying, where one recipient grants access to another recipient,
		/// therefore two different key ids are taken as parameters.
		/// </summary>
		/// <param name="keyId">
		/// The recipient key id of the user doing the request and providing access.
		/// The backend provides the encrypted data keys for this recipient key, so the client can decrypt them.
		/// </param>
		/// <param name="targetKeyId">
		/// The recipient key id of the user to which the access is granted, i.e. for which the client will reencrypt the data keys.
		/// The backend will not return logs that already have a data key for this recipient.
		/// </param>
		/// <param name="offset">
		/// The number of logs to skip at the beginning when ordered by user id and then creation time, after filtering.
		/// This is used by the client to skip logs that couldn't be reencrypted for some reason.
		/// </param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A task providing a dictionary that maps the ids of the logs in the chunk to their associated <see cref="EncryptionInfo"/> data.</returns>
		Task<IReadOnlyDictionary<Guid, EncryptionInfo>> GetKeysForRekeying(KeyId keyId, KeyId targetKeyId, int offset = 0, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously uploads a chunk of data keys that the client rekeyed for the recipient key pair identified by <paramref name="keyId"/>
		/// into the backend database of the application for which the client is authenticated.
		/// </summary>
		/// <param name="keyId">
		/// The recipient key id of the user to which the access is granted, i.e. for which the client has reencrypted the data keys.
		/// </param>
		/// <param name="dataKeys">A dictionary mapping the ids of the logs in the chunk to their added encrypted data key information.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		Task PutRekeyedKeys(KeyId keyId, IReadOnlyDictionary<Guid, DataKeyInfo> dataKeys, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously retrieves the authorized recipient key certificates of the application identified by <paramref name="appName"/> for end-to-end encryption of logs.
		/// The retrieved certificates are added to <paramref name="certificateStore"/>, during which they are validated by the 
		/// <see cref="ICertificateValidator"/> configured in <paramref name="certificateStore"/>.
		/// </summary>
		/// <param name="appName">The unique name identifying the application for which to retrieve the certificates.</param>
		/// <param name="certificateStore">The certificate store into which the certificates shall be loaded.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		Task GetRecipientCertificates(string appName, CertificateStore certificateStore, CancellationToken ct = default);
	}
}
