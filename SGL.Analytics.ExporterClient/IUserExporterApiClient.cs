using Microsoft.Extensions.Logging;
using SGL.Analytics.DTO;
using SGL.Utilities;
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
	}
}
