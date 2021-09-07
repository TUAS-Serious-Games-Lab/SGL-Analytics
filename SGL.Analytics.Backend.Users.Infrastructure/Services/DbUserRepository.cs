using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Infrastructure.Services {
	public class DbUserRepository : IUserRepository {
		private UsersContext context;

		public DbUserRepository(UsersContext context) {
			this.context = context;
		}

		public async Task<UserRegistration?> GetUserByIdAsync(Guid id) {
			return await context.UserRegistrations
				.Include(u => u.App).ThenInclude(a => a.UserProperties)
				.Include(u => u.AppSpecificProperties).ThenInclude(p => p.Definition)
				.Where(u => u.Id == id)
				.SingleOrDefaultAsync<UserRegistration?>();
		}

		public async Task<UserRegistration> RegisterUserAsync(UserRegistration userReg) {
			userReg.ValidateProperties(); // Throws on error
			context.UserRegistrations.Add(userReg);
			await context.SaveChangesAsync();
			return userReg;
		}

		public async Task<UserRegistration> UpdateUserAsync(UserRegistration userReg) {
			Debug.Assert(context.Entry(userReg).State is EntityState.Modified or EntityState.Unchanged);
			userReg.ValidateProperties(); // Throws on error
			await context.SaveChangesAsync();
			return userReg;
		}
	}
}
