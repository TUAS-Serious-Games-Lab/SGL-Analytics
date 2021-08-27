using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;

namespace SGL.Analytics.Backend.Users.Infrastructure.Data {
	public class UsersContext : DbContext {
		public UsersContext(DbContextOptions<UsersContext> options)
			: base(options) {
		}

		public DbSet<UserRegistration> UserRegistrations => Set<UserRegistration>();
		public DbSet<ApplicationWithUserProperties> Applications => Set<ApplicationWithUserProperties>();
		public DbSet<ApplicationUserPropertyDefinition> ApplicationUserPropertyDefinitions => Set<ApplicationUserPropertyDefinition>();
		public DbSet<ApplicationUserPropertyInstance> ApplicationUserPropertyInstances => Set<ApplicationUserPropertyInstance>();

	}
}