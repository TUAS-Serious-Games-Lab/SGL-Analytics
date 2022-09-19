using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGL.Analytics.DTO;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Collector.Controllers {
	[Route("api/analytics/log/v2")]
	[ApiController]
	[Authorize(Policy = "ExporterUser")]
	public class LogExporterController : ControllerBase {
		[HttpGet()]
		public Task<ActionResult<IEnumerable<Guid>>> GetLogIds(CancellationToken ct = default) {
			throw new NotImplementedException();
		}

		[HttpGet("all")]
		public Task<ActionResult<IEnumerable<DownstreamLogMetadataDTO>>> GetMetadataForAllLogs([FromQuery] KeyId? recipient = null, CancellationToken ct = default) {
			throw new NotImplementedException();
		}

		[HttpGet("{id:Guid}/metadata")]
		public Task<ActionResult<DownstreamLogMetadataDTO>> GetLogMetadataById(Guid id, [FromQuery] KeyId? recipient = null) {
			throw new NotImplementedException();
		}

		[HttpGet("{id:Guid}/content")]
		public Task<ActionResult> GetLogContentById(Guid id) {
			throw new NotImplementedException();
		}
	}
}
