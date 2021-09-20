using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
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
			try {
				await context.SaveChangesAsync();
			}
			catch (DbUpdateConcurrencyException ex) {
				throw new ConcurrencyConflictException(ex);
			}
			catch (DbUpdateException ex) {
				// Should happen rarely and unfortunately, at the time of writing, there is no portable way (between databases) of further classifying the error.
				// To check if ex is a unique constraint violation, we would need to inspect its inner exception and switch over exception types for all supported providers and their internal error classifications.
				// To avoid this coupling, rather pay the perf cost of querrying again in this rare case.
				if (await context.UserRegistrations.CountAsync(u => u.Username == userReg.Username && u.AppId == userReg.App.Id) > 0) {
					throw new EntityUniquenessConflictException("UserRegistration", "Username", userReg.Username, ex);
				}
				else if (await context.UserRegistrations.CountAsync(u => u.Id == userReg.Id) > 0) {
					throw new EntityUniquenessConflictException("UserRegistration", "Id", userReg.Id, ex);
				}
				else throw;
			}
			return userReg;
		}

		public async Task<UserRegistration> UpdateUserAsync(UserRegistration userReg) {
			Debug.Assert(context.Entry(userReg).State is EntityState.Modified or EntityState.Unchanged);
			userReg.ValidateProperties(); // Throws on error
			try {
				await context.SaveChangesAsync();
			}
			catch (DbUpdateConcurrencyException ex) {
				throw new ConcurrencyConflictException(ex);
			}
			catch (DbUpdateException ex) {
				// Should happen rarely and unfortunately, at the time of writing, there is no portable way (between databases) of further classifying the error.
				// To check if ex is a unique constraint violation, we would need to inspect its inner exception and switch over exception types for all supported providers and their internal error classifications.
				// To avoid this coupling, rather pay the perf cost of querrying again in this rare case.
				if (await context.UserRegistrations.CountAsync(u => u.Username == userReg.Username && u.AppId == userReg.App.Id) > 0) {
					throw new EntityUniquenessConflictException("UserRegistration", "Username", userReg.Username, ex);
				}
				else throw;
			}
			return userReg;
		}
	}
}
