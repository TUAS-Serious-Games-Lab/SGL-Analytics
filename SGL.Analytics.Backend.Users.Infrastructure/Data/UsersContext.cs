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

		protected override void OnModelCreating(ModelBuilder modelBuilder) {
			modelBuilder.Entity<ApplicationWithUserProperties>().HasIndex(a => a.Name).IsUnique();
			modelBuilder.Entity<UserRegistration>().HasIndex(u => new { u.AppId, u.Username }).IsUnique();
			modelBuilder.Entity<ApplicationUserPropertyDefinition>().HasIndex(pd => new { pd.AppId, pd.Name }).IsUnique();
			modelBuilder.Entity<UserRegistration>().OwnsMany(u => u.AppSpecificProperties, p => {
				p.WithOwner(p => p.User);
				p.HasIndex(pi => new { pi.DefinitionId, pi.UserId }).IsUnique();
			});
		}

		public DbSet<UserRegistration> UserRegistrations => Set<UserRegistration>();
		public DbSet<ApplicationWithUserProperties> Applications => Set<ApplicationWithUserProperties>();
		public DbSet<ApplicationUserPropertyDefinition> ApplicationUserPropertyDefinitions => Set<ApplicationUserPropertyDefinition>();
		public DbSet<ApplicationUserPropertyInstance> ApplicationUserPropertyInstances => Set<ApplicationUserPropertyInstance>();

	}
}