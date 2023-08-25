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
	/// Specifies the interface for client implementations for interacting with the users backend service.
	/// </summary>
	public interface IUserExporterApiClient : IApiClient {
		/// <summary>
		/// Asynchronously retrieves the list of the ids of the user registrations present in the application for which the client is authenticated.
		/// </summary>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A task providing an enumerable providing the ids.</returns>
		Task<IEnumerable<Guid>> GetUserIdListAsync(CancellationToken ct = default);
		/// <summary>
		/// Asynchronously retrieves the metadata of the user registrations present in the application for which the client is authenticated.
		/// </summary>
		/// <param name="recipientKeyId">Indicates the recipient key id for which the encrypted data keys for the encrypted app-specific properties shall be returned.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A task providing an enumerable providing the user registration data.</returns>
		Task<IEnumerable<UserMetadataDTO>> GetMetadataForAllUsersAsync(KeyId? recipientKeyId = null, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously retrieves the metadata of the user registration with the id indicated by <paramref name="id"/>.
		/// The user registration must belong the application for which the client is authenticated.
		/// </summary>
		/// <param name="id">The id of the user registration for which to retrieve the metadata.</param>
		/// <param name="recipientKeyId">Indicates the recipient key id for which the encrypted data key for the encrypted app-specific properties shall be returned.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A task providing the user registration data.</returns>
		Task<UserMetadataDTO> GetUserMetadataByIdAsync(Guid id, KeyId? recipientKeyId = null, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously retrieves the authorized recipient key certificates of the application identified by <paramref name="appName"/> for end-to-end encryption of encrypted app-specific user properties.
		/// The retrieved certificates are added to <paramref name="certificateStore"/>, during which they are validated by the 
		/// <see cref="ICertificateValidator"/> configured in <paramref name="certificateStore"/>.
		/// </summary>
		/// <param name="appName">The unique name identifying the application for which to retrieve the certificates.</param>
		/// <param name="certificateStore">The certificate store into which the certificates shall be loaded.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		Task GetRecipientCertificates(string appName, CertificateStore certificateStore, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously retrieves a chunk of encryption metadata of encrypted app-specific properties of user registrations of the application for which the client is authenticated.
		/// These <see cref="EncryptionInfo"/>s are intended for rekeying, where one recipient grants access to another recipient,
		/// therefore two different key ids are taken as parameters.
		/// </summary>
		/// <param name="keyId">
		/// The recipient key id of the user doing the request and providing access.
		/// The backend provides the encrypted data keys for this recipient key, so the client can decrypt them.
		/// </param>
		/// <param name="targetKeyId">
		/// The recipient key id of the user to which the access is granted, i.e. for which the client will reencrypt the data keys.
		/// The backend will not return user registrations that already have a data key for this recipient.
		/// </param>
		/// <param name="offset">
		/// The number of user registrations to skip at the beginning when ordered by user id, after filtering.
		/// This is used by the client to skip user registrations that couldn't be reencrypted for some reason.
		/// </param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A task providing a dictionary that maps the ids of the user registrations in the chunk to their associated <see cref="EncryptionInfo"/> data.</returns>
		Task<IReadOnlyDictionary<Guid, EncryptionInfo>> GetKeysForRekeying(KeyId keyId, KeyId targetKeyId, int offset = 0, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously uploads a chunk of data keys that the client rekeyed for the recipient key pair identified by <paramref name="keyId"/>
		/// into the backend database of the application for which the client is authenticated.
		/// </summary>
		/// <param name="keyId">
		/// The recipient key id of the user to which the access is granted, i.e. for which the client has reencrypted the data keys.
		/// </param>
		/// <param name="dataKeys">A dictionary mapping the ids of the user registrations in the chunk to their added encrypted data key information.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		Task PutRekeyedKeys(KeyId keyId, Dictionary<Guid, DataKeyInfo> dataKeys, CancellationToken ct = default);
	}
}
