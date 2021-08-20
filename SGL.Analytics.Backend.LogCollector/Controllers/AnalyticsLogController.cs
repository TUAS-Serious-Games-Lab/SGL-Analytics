using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.LogCollector.Data;
using SGL.Analytics.Backend.Model;

namespace SGL.Analytics.Backend.LogCollector.Controllers {
	[Route("api/[controller]")]
	[ApiController]
	public class AnalyticsLogController : ControllerBase {
		private readonly LogCollectorContext _context;

		public AnalyticsLogController(LogCollectorContext context) {
			_context = context;
		}

		// GET: api/AnalyticsLog
		[HttpGet]
		public async Task<ActionResult<IEnumerable<LogMetadata>>> GetLogMetadata() {
			return await _context.LogMetadata.ToListAsync();
		}

		// GET: api/AnalyticsLog/5
		[HttpGet("{id}")]
		public async Task<ActionResult<LogMetadata>> GetLogMetadata(Guid id) {
			var logMetadata = await _context.LogMetadata.FindAsync(id);

			if (logMetadata == null) {
				return NotFound();
			}

			return logMetadata;
		}

		// PUT: api/AnalyticsLog/5
		// To protect from overposting attacks, enable the specific properties you want to bind to, for
		// more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
		[HttpPut("{id}")]
		public async Task<IActionResult> PutLogMetadata(Guid id, LogMetadata logMetadata) {
			if (id != logMetadata.Id) {
				return BadRequest();
			}

			_context.Entry(logMetadata).State = EntityState.Modified;

			try {
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateConcurrencyException) {
				if (!LogMetadataExists(id)) {
					return NotFound();
				}
				else {
					throw;
				}
			}

			return NoContent();
		}

		// POST: api/AnalyticsLog
		// To protect from overposting attacks, enable the specific properties you want to bind to, for
		// more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
		[HttpPost]
		public async Task<ActionResult<LogMetadata>> PostLogMetadata(LogMetadata logMetadata) {
			_context.LogMetadata.Add(logMetadata);
			await _context.SaveChangesAsync();

			return CreatedAtAction("GetLogMetadata", new { id = logMetadata.Id }, logMetadata);
		}

		// DELETE: api/AnalyticsLog/5
		[HttpDelete("{id}")]
		public async Task<ActionResult<LogMetadata>> DeleteLogMetadata(Guid id) {
			var logMetadata = await _context.LogMetadata.FindAsync(id);
			if (logMetadata == null) {
				return NotFound();
			}

			_context.LogMetadata.Remove(logMetadata);
			await _context.SaveChangesAsync();

			return logMetadata;
		}

		private bool LogMetadataExists(Guid id) {
			return _context.LogMetadata.Any(e => e.Id == id);
		}
	}
}
