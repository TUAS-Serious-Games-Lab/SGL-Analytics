using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using SGL.Utilities.Backend;
using SGL.Utilities.Crypto.Keys;
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

		private IQueryable<UserRegistration> ApplyQueryOptions(IQueryable<UserRegistration> query, UserQueryOptions? queryOptions) {
			if (queryOptions == null) {
				queryOptions = new UserQueryOptions();
			}
			if (queryOptions.FetchRecipientKeys) {
				query = query.Include(u => u.PropertyRecipientKeys);
			}
			else if (queryOptions.FetchRecipientKey != null) {
				query = query.Include(u => u.PropertyRecipientKeys.Where(rk => rk.RecipientKeyId == queryOptions.FetchRecipientKey));
			}
			if (queryOptions.FetchProperties) {
				query = query.Include(u => u.App)
					.ThenInclude(a => a.UserProperties)
					.Include(u => u.AppSpecificProperties)
					.ThenInclude(p => p.Definition)
					.AsSplitQuery();
			}
			else {
				query = query.Include(u => u.App);
			}
			switch (queryOptions.Ordering) {
				case UserQuerySortCriteria.UserId:
					query = query.OrderBy(ur => ur.Id);
					break;
				default:
					break;
			}
			if (queryOptions.Offset > 0) {
				query = query.Skip(queryOptions.Offset);
			}
			if (queryOptions.Limit > 0) {
				query = query.Take(queryOptions.Limit);
			}
			if (!queryOptions.ForUpdating) {
				query = query.AsNoTracking();
			}
			return query;
		}

		/// <inheritdoc/>
		public async Task<UserRegistration?> GetUserByIdAsync(Guid id, UserQueryOptions? queryOptions = null, CancellationToken ct = default) {
			var query = context.UserRegistrations.Where(u => u.Id == id);
			query = ApplyQueryOptions(query, queryOptions);
			return await query.SingleOrDefaultAsync<UserRegistration?>(ct);
		}

		/// <inheritdoc/>
		public async Task<UserRegistration?> GetUserByUsernameAndAppNameAsync(string username, string appName, UserQueryOptions? queryOptions = null, CancellationToken ct = default) {
			var query = context.UserRegistrations.Where(u => u.Username == username && u.App.Name == appName);
			query = ApplyQueryOptions(query, queryOptions);
			return await query.SingleOrDefaultAsync<UserRegistration?>(ct);
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
					throw new EntityUniquenessConflictException(nameof(UserRegistration), nameof(UserRegistration.Username), userReg.Username, ex);
				}
				else if (userReg.BasicFederationUpstreamUserId != null && await context.UserRegistrations.CountAsync(u => u.AppId == userReg.App.Id && u.BasicFederationUpstreamUserId == userReg.BasicFederationUpstreamUserId, ct) > 0) {
					throw new EntityUniquenessConflictException(nameof(UserRegistration), nameof(UserRegistration.BasicFederationUpstreamUserId), userReg.BasicFederationUpstreamUserId!, ex);
				}
				else if (await context.UserRegistrations.CountAsync(u => u.Id == userReg.Id, ct) > 0) {
					throw new EntityUniquenessConflictException(nameof(UserRegistration), nameof(UserRegistration.Id), userReg.Id, ex);
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
			return await query.AsNoTracking().ToDictionaryAsync(e => e.AppName, e => e.UsersCount, ct);
		}

		/// <inheritdoc/>
		public async Task<IEnumerable<UserRegistration>> ListUsersAsync(string appName, KeyId? notForKeyId = null, UserQueryOptions? queryOptions = null, CancellationToken ct = default) {
			var query = context.UserRegistrations.Where(u => u.App.Name == appName);
			if (notForKeyId != null) {
				query = query.Where(u => !u.PropertyRecipientKeys.Any(rk => rk.RecipientKeyId == notForKeyId));
			}
			query = ApplyQueryOptions(query, queryOptions);
			return await query.ToListAsync(ct);
		}

		/// <inheritdoc/>
		public async Task<IList<UserRegistration>> UpdateUsersAsync(IList<UserRegistration> userRegs, CancellationToken ct = default) {
			Debug.Assert(userRegs.All(userReg => context.Entry(userReg).State is EntityState.Modified or EntityState.Unchanged));
			await context.SaveChangesAsync(ct);
			return userRegs;
		}

		/// <inheritdoc/>
		public async Task<IEnumerable<UserRegistration>> GetUsersByIdsAsync(IReadOnlyCollection<Guid> ids, UserQueryOptions? queryOptions = null, CancellationToken ct = default) {
			var idsSet = ids.ToHashSet();
			var query = context.UserRegistrations
				.Where(u => idsSet.Contains(u.Id));
			query = ApplyQueryOptions(query, queryOptions);
			return await query.ToListAsync(ct);
		}

		/// <inheritdoc/>
		public async Task<UserRegistration?> GetUserByBasicFederationUpstreamUserIdAsync(Guid upstreamUserId, string appName, UserQueryOptions? queryOptions = null, CancellationToken ct = default) {
			var query = context.UserRegistrations.Where(u => u.App.Name == appName && u.BasicFederationUpstreamUserId == upstreamUserId);
			query = ApplyQueryOptions(query, queryOptions);
			return await query.SingleOrDefaultAsync<UserRegistration?>(ct);
		}
	}
}
