using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;

namespace SGL.Analytics.Backend.UserDB.Controllers {
    [Route("api/[controller]")]
    [ApiController]
    public class AnalyticsUserController : ControllerBase {
        private readonly UserDBContext _context;

        public AnalyticsUserController(UserDBContext context) {
            _context = context;
        }

        // POST: api/AnalyticsUser
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        [HttpPost]
        public async Task<ActionResult<UserRegistration>> PostUserRegistration(UserRegistration userRegistration) {
            _context.UserRegistrations.Add(userRegistration);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetUserRegistration", new { id = userRegistration.Id }, userRegistration);
        }

    }
}
