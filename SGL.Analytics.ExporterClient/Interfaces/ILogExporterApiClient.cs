using Microsoft.Extensions.Logging;
using SGL.Analytics.DTO;
using SGL.Utilities;
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
		Task<Dictionary<Guid, EncryptionInfo>> GetKeysForRekeying(KeyId? recipientKeyId, CancellationToken ct = default);
		Task PushRekeyedKeys(KeyId recipientKeyId, Dictionary<Guid, DataKeyInfo> dataKeys, CancellationToken ct = default);
	}
}
