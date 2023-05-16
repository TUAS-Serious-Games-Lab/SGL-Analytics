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
	public interface IUserExporterApiClient : IApiClient {
		Task<IEnumerable<Guid>> GetUserIdListAsync(CancellationToken ct = default);
		Task<IEnumerable<UserMetadataDTO>> GetMetadataForAllUsersAsync(KeyId? recipientKeyId = null, CancellationToken ct = default);
		Task<UserMetadataDTO> GetUserMetadataByIdAsync(Guid id, KeyId? recipientKeyId = null, CancellationToken ct = default);
		Task GetRecipientCertificates(string appName, CertificateStore certificateStore, CancellationToken ct = default);
		Task<IReadOnlyDictionary<Guid, EncryptionInfo>> GetKeysForRekeying(KeyId keyId, CancellationToken ct = default);
		Task PutRekeyedKeys(KeyId keyId, Dictionary<Guid, DataKeyInfo> dataKeys, CancellationToken ct = default);
	}
}
