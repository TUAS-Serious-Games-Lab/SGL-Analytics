using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Model;

namespace SGL.Analytics.Backend.UserDB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AnalyticsUserController : ControllerBase
    {
        private readonly UserDBContext _context;

        public AnalyticsUserController(UserDBContext context)
        {
            _context = context;
        }

        // GET: api/AnalyticsUser
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserRegistration>>> GetUserRegistration()
        {
            return await _context.UserRegistration.ToListAsync();
        }

        // GET: api/AnalyticsUser/5
        [HttpGet("{id}")]
        public async Task<ActionResult<UserRegistration>> GetUserRegistration(Guid id)
        {
            var userRegistration = await _context.UserRegistration.FindAsync(id);

            if (userRegistration == null)
            {
                return NotFound();
            }

            return userRegistration;
        }

        // PUT: api/AnalyticsUser/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUserRegistration(Guid id, UserRegistration userRegistration)
        {
            if (id != userRegistration.Id)
            {
                return BadRequest();
            }

            _context.Entry(userRegistration).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserRegistrationExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/AnalyticsUser
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        [HttpPost]
        public async Task<ActionResult<UserRegistration>> PostUserRegistration(UserRegistration userRegistration)
        {
            _context.UserRegistration.Add(userRegistration);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetUserRegistration", new { id = userRegistration.Id }, userRegistration);
        }

        // DELETE: api/AnalyticsUser/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<UserRegistration>> DeleteUserRegistration(Guid id)
        {
            var userRegistration = await _context.UserRegistration.FindAsync(id);
            if (userRegistration == null)
            {
                return NotFound();
            }

            _context.UserRegistration.Remove(userRegistration);
            await _context.SaveChangesAsync();

            return userRegistration;
        }

        private bool UserRegistrationExists(Guid id)
        {
            return _context.UserRegistration.Any(e => e.Id == id);
        }
    }
}
