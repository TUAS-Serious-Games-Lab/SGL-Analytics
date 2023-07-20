using Org.BouncyCastle.Utilities;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Utilities.Backend;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Tests.Dummies {
	public class DummyUserRepository : IUserRepository {
		private readonly Dictionary<Guid, UserRegistration> users = new Dictionary<Guid, UserRegistration>();
		private int nextPropertyInstanceId = 1;

		public async Task<UserRegistration?> GetUserByIdAsync(Guid id, UserQueryOptions? queryOptions = null, CancellationToken ct = default) {
			await Task.CompletedTask;
			ct.ThrowIfCancellationRequested();
			if (users.TryGetValue(id, out var user)) {
				return user;
			}
			else {
				return null;
			}
		}

		public async Task<UserRegistration?> GetUserByUsernameAndAppNameAsync(string username, string appName, UserQueryOptions? queryOptions = null, CancellationToken ct = default) {
			await Task.CompletedTask;
			return users.Values.Where(u => u.Username == username && u.App.Name == appName).SingleOrDefault<UserRegistration?>();
		}

		public async Task<IEnumerable<UserRegistration>> GetUsersByIdsAsync(IReadOnlyCollection<Guid> ids, UserQueryOptions? queryOptions = null, CancellationToken ct = default) {
			await Task.CompletedTask;
			ct.ThrowIfCancellationRequested();
			var idsSet = ids.ToHashSet();
			return users.Values.Where(u => idsSet.Contains(u.Id)).ToList();
		}

		public async Task<IDictionary<string, int>> GetUsersCountPerAppAsync(CancellationToken ct = default) {
			await Task.CompletedTask;
			var query = from ur in users.Values
						group ur by ur.App.Name into a
						select new { AppName = a.Key, UsersCount = a.Count() };
			return query.ToDictionary(e => e.AppName, e => e.UsersCount);
		}

		public Task<IEnumerable<UserRegistration>> ListUsersAsync(string appName, KeyId? notForKeyId = null, UserQueryOptions? queryOptions = null, CancellationToken ct = default) {
			var query = users.Values.Where(u => u.App.Name == appName);
			if (notForKeyId != null) {
				query = query.Where(u => !u.PropertyEncryptionInfo.DataKeys.ContainsKey(notForKeyId));
			}
			switch (queryOptions?.Ordering) {
				case UserQuerySortCriteria.UserId:
					query = query.OrderBy(ur => ur.Id);
					break;
				default:
					break;
			}
			if ((queryOptions?.Offset ?? 0) > 0) {
				query = query.Skip(queryOptions!.Offset);
			}
			if ((queryOptions?.Limit ?? 0) > 0) {
				query = query.Take(queryOptions!.Limit);
			}
			return Task.FromResult(query.ToList().AsEnumerable());
		}

		public async Task<UserRegistration> RegisterUserAsync(UserRegistration userReg, CancellationToken ct = default) {
			await Task.CompletedTask;
			if (userReg.Id == Guid.Empty) userReg.Id = Guid.NewGuid();
			if (users.ContainsKey(userReg.Id)) throw new EntityUniquenessConflictException("UserRegistration", "Id", userReg.Id);
			if (users.Values.Any(u => u.Username == userReg.Username)) throw new EntityUniquenessConflictException("UserRegistration", "Username", userReg.Username);
			assignPropertyInstanceIds(userReg);
			userReg.ValidateProperties();
			ct.ThrowIfCancellationRequested();
			users.Add(userReg.Id, userReg);
			return userReg;
		}

		public async Task<UserRegistration> UpdateUserAsync(UserRegistration userReg, CancellationToken ct = default) {
			await Task.CompletedTask;
			Debug.Assert(users.ContainsKey(userReg.Id));
			assignPropertyInstanceIds(userReg);
			userReg.ValidateProperties();
			ct.ThrowIfCancellationRequested();
			users[userReg.Id] = userReg;
			return userReg;
		}

		public async Task<IList<UserRegistration>> UpdateUsersAsync(IList<UserRegistration> userRegs, CancellationToken ct = default) {
			foreach (var userReg in userRegs) {
				await UpdateUserAsync(userReg, ct);
			}
			return userRegs;
		}

		private void assignPropertyInstanceIds(UserRegistration userReg) {
			foreach (var propInst in userReg.AppSpecificProperties) {
				if (propInst.Id == 0) propInst.Id = nextPropertyInstanceId++;
			}
		}
	}
}
