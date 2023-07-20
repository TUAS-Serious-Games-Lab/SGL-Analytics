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
	public interface ILogExporterApiClient : IApiClient {
		Task<IEnumerable<Guid>> GetLogIdListAsync(CancellationToken ct = default);
		Task<IEnumerable<DownstreamLogMetadataDTO>> GetMetadataForAllLogsAsync(KeyId? recipientKeyId = null, CancellationToken ct = default);
		Task<DownstreamLogMetadataDTO> GetLogMetadataByIdAsync(Guid id, KeyId? recipientKeyId = null, CancellationToken ct = default);
		Task<Stream> GetLogContentByIdAsync(Guid id, CancellationToken ct = default);
		Task<IReadOnlyDictionary<Guid, EncryptionInfo>> GetKeysForRekeying(KeyId keyId, KeyId targetKeyId, int offset = 0, CancellationToken ct = default);
		Task PutRekeyedKeys(KeyId keyId, IReadOnlyDictionary<Guid, DataKeyInfo> dataKeys, CancellationToken ct = default);
		Task GetRecipientCertificates(string appName, CertificateStore certificateStore, CancellationToken ct = default);
	}
}
