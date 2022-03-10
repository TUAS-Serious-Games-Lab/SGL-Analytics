using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using SGL.Utilities.Backend;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Infrastructure.Services {
	/// <summary>
	/// Provides a persistent implementation of <see cref="IUserRepository"/> using Entity Framework Core to map the objects into a relational database.
	/// </summary>
	public class DbUserRepository : IUserRepository {
		private UsersContext context;

		/// <summary>
		/// Creates a repository object using the given database context object for data access.
		/// </summary>
		/// <param name="context">The <see cref="DbContext"/> implementation for the database.</param>
		public DbUserRepository(UsersContext context) {
			this.context = context;
		}

		/// <inheritdoc/>
		public async Task<UserRegistration?> GetUserByIdAsync(Guid id, CancellationToken ct = default) {
			return await context.UserRegistrations
				.Include(u => u.App).ThenInclude(a => a.UserProperties)
				.Include(u => u.AppSpecificProperties).ThenInclude(p => p.Definition)
				.Where(u => u.Id == id)
				.SingleOrDefaultAsync<UserRegistration?>(ct);
		}

		/// <inheritdoc/>
		public async Task<UserRegistration?> GetUserByUsernameAndAppNameAsync(string username, string appName, CancellationToken ct = default) {
			return await context.UserRegistrations
				.Include(u => u.App).ThenInclude(a => a.UserProperties)
				.Include(u => u.AppSpecificProperties).ThenInclude(p => p.Definition)
				.Where(u => u.Username == username && u.App.Name == appName)
				.SingleOrDefaultAsync<UserRegistration?>(ct);
		}

		/// <inheritdoc/>
		public async Task<UserRegistration> RegisterUserAsync(UserRegistration userReg, CancellationToken ct = default) {
			userReg.ValidateProperties(); // Throws on error
			context.UserRegistrations.Add(userReg);
			try {
				await context.SaveChangesAsync(ct);
			}
			catch (DbUpdateConcurrencyException ex) {
				throw new ConcurrencyConflictException(ex);
			}
			catch (DbUpdateException ex) {
				// Should happen rarely and unfortunately, at the time of writing, there is no portable way (between databases) of further classifying the error.
				// To check if ex is a unique constraint violation, we would need to inspect its inner exception and switch over exception types for all supported providers and their internal error classifications.
				// To avoid this coupling, rather pay the perf cost of querrying again in this rare case.
				if (await context.UserRegistrations.CountAsync(u => u.Username == userReg.Username && u.AppId == userReg.App.Id, ct) > 0) {
					throw new EntityUniquenessConflictException("UserRegistration", "Username", userReg.Username, ex);
				}
				else if (await context.UserRegistrations.CountAsync(u => u.Id == userReg.Id, ct) > 0) {
					throw new EntityUniquenessConflictException("UserRegistration", "Id", userReg.Id, ex);
				}
				else throw;
			}
			return userReg;
		}

		/// <inheritdoc/>
		public async Task<UserRegistration> UpdateUserAsync(UserRegistration userReg, CancellationToken ct = default) {
			Debug.Assert(context.Entry(userReg).State is EntityState.Modified or EntityState.Unchanged);
			userReg.ValidateProperties(); // Throws on error
			try {
				await context.SaveChangesAsync(ct);
			}
			catch (DbUpdateConcurrencyException ex) {
				throw new ConcurrencyConflictException(ex);
			}
			catch (DbUpdateException ex) {
				// Should happen rarely and unfortunately, at the time of writing, there is no portable way (between databases) of further classifying the error.
				// To check if ex is a unique constraint violation, we would need to inspect its inner exception and switch over exception types for all supported providers and their internal error classifications.
				// To avoid this coupling, rather pay the perf cost of querrying again in this rare case.
				if (await context.UserRegistrations.CountAsync(u => u.Username == userReg.Username && u.AppId == userReg.App.Id, ct) > 0) {
					throw new EntityUniquenessConflictException("UserRegistration", "Username", userReg.Username, ex);
				}
				else throw;
			}
			return userReg;
		}

		/// <inheritdoc/>
		public async Task<IDictionary<string, int>> GetUsersCountPerAppAsync(CancellationToken ct = default) {
			var query = from ur in context.UserRegistrations.Include(ur => ur.App)
						group ur by ur.App.Name into a
						select new { AppName = a.Key, UsersCount = a.Count() };
			return await query.ToDictionaryAsync(e => e.AppName, e => e.UsersCount, ct);
		}
	}
}
