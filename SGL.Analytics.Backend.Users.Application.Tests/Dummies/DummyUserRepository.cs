using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
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

		public async Task<UserRegistration?> GetUserByIdAsync(Guid id, CancellationToken ct = default) {
			await Task.CompletedTask;
			ct.ThrowIfCancellationRequested();
			if (users.TryGetValue(id, out var user)) {
				return user;
			}
			else {
				return null;
			}
		}

		public async Task<IDictionary<string, int>> GetUsersCountPerAppAsync(CancellationToken ct = default) {
			await Task.CompletedTask;
			var query = from ur in users.Values
						group ur by ur.App.Name into a
						select new { AppName = a.Key, UsersCount = a.Count() };
			return query.ToDictionary(e => e.AppName, e => e.UsersCount);
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

		private void assignPropertyInstanceIds(UserRegistration userReg) {
			foreach (var propInst in userReg.AppSpecificProperties) {
				if (propInst.Id == 0) propInst.Id = nextPropertyInstanceId++;
			}
		}
	}
}
