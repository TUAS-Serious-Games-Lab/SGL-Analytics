using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Tests.Dummies {
	public class DummyUserRepository : IUserRepository {
		private readonly Dictionary<Guid, UserRegistration> users = new Dictionary<Guid, UserRegistration>();

		public async Task<UserRegistration?> GetUserByIdAsync(Guid id) {
			await Task.CompletedTask;
			if (users.TryGetValue(id, out var user)) {
				return user;
			}
			else {
				return null;
			}
		}

		public async Task<UserRegistration> RegisterUserAsync(UserRegistration userReg) {
			await Task.CompletedTask;
			if (userReg.Id == Guid.Empty) userReg.Id = Guid.NewGuid();
			if (users.ContainsKey(userReg.Id)) throw new EntityUniquenessConflictException("UserRegistration", "Id");
			if (users.Values.Any(u => u.Username == userReg.Username)) throw new EntityUniquenessConflictException("UserRegistration", "Username");
			userReg.ValidateProperties();
			users.Add(userReg.Id, userReg);
			return userReg;
		}

		public async Task<UserRegistration> UpdateUserAsync(UserRegistration userReg) {
			await Task.CompletedTask;
			Debug.Assert(users.ContainsKey(userReg.Id));
			userReg.ValidateProperties();
			users[userReg.Id] = userReg;
			return userReg;
		}
	}
}
