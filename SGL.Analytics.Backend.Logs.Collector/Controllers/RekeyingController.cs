using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Collector.Controllers {
	[Route("api/analytics/log/v2/rekey")]
	[ApiController]
	[Authorize(Policy = "ExporterUser")]
	public class RekeyingController : ControllerBase {
		[HttpGet("{keyId}")]
		public async Task<ActionResult<Dictionary<Guid, EncryptionInfo>>> GetKeysForRekeying([FromRoute(Name = "keyId")] KeyId recipientKeyId, CancellationToken ct = default) {
			throw new NotImplementedException();
		}

		[HttpPut("{keyId}")]
		public async Task<ActionResult> PutRekeyedKeys([FromRoute(Name = "keyId")] KeyId recipientKeyId, [FromBody] Dictionary<Guid, DataKeyInfo> dataKeys, CancellationToken ct = default) {
			throw new NotImplementedException();
		}
	}
}
