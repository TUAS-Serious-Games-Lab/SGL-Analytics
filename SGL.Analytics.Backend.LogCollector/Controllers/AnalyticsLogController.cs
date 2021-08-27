using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.LogCollector.Data;
using SGL.Analytics.Backend.Domain.Entity;

namespace SGL.Analytics.Backend.LogCollector.Controllers {
    [Route("api/[controller]")]
    [ApiController]
    public class AnalyticsLogController : ControllerBase {
        private readonly LogCollectorContext _context;

        public AnalyticsLogController(LogCollectorContext context) {
            _context = context;
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
    }
}
