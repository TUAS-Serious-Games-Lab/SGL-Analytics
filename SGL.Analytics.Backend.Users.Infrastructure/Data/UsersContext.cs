using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Utilities.Backend;

namespace SGL.Analytics.Backend.Users.Infrastructure.Data {
	/// <summary>
	/// The <see cref="DbContext"/> class for the database of the SGL Analytics user registration service.
	/// </summary>
	public class UsersContext : DbContext {
		/// <summary>
		/// Instantiates the context with the given options.
		/// </summary>
		/// <param name="options">The options for the database access.</param>
		public UsersContext(DbContextOptions<UsersContext> options)
			: base(options) {
		}
		/// <summary>
		/// Configures the model of the database schema.
		/// This is called by Entity Framework Core.
		/// </summary>
		/// <param name="modelBuilder">The builder to use for configuring the model.</param>
		protected override void OnModelCreating(ModelBuilder modelBuilder) {
			var app = modelBuilder.Entity<ApplicationWithUserProperties>();
			app.HasIndex(a => a.Name).IsUnique();
			app.Property(a => a.Name).HasMaxLength(128);
			app.Property(a => a.ApiToken).HasMaxLength(64);

			var propDef = modelBuilder.Entity<ApplicationUserPropertyDefinition>();
			propDef.HasIndex(pd => (new { pd.AppId, pd.Name })).IsUnique();
			propDef.Property(pd => pd.Name).HasMaxLength(128);

			var userReg = modelBuilder.Entity<UserRegistration>();
			userReg.HasIndex(u => (new { u.AppId, u.Username })).IsUnique();
			userReg.Property(u => u.Username).HasMaxLength(64);
			userReg.Property(u => u.HashedSecret).HasMaxLength(128);
			userReg.OwnsMany(u => u.AppSpecificProperties, p => {
				p.WithOwner(p => p.User);
				p.HasIndex(pi => new { pi.DefinitionId, pi.UserId }).IsUnique();
				p.Property(pi => pi.DateTimeValue).IsStoredInUtc();
			});
		}
		/// <summary>
		/// The accessor for the table containing <see cref="UserRegistration"/> objects.
		/// </summary>
		public DbSet<UserRegistration> UserRegistrations => Set<UserRegistration>();
		/// <summary>
		/// The accessor for the table containing <see cref="ApplicationWithUserProperties"/> objects.
		/// </summary>
		public DbSet<ApplicationWithUserProperties> Applications => Set<ApplicationWithUserProperties>();
		/// <summary>
		/// The accessor for the table containing <see cref="ApplicationUserPropertyDefinition"/> objects.
		/// </summary>
		public DbSet<ApplicationUserPropertyDefinition> ApplicationUserPropertyDefinitions => Set<ApplicationUserPropertyDefinition>();
		/// <summary>
		/// The accessor for the table containing <see cref="ApplicationUserPropertyInstance"/> objects.
		/// </summary>
		public DbSet<ApplicationUserPropertyInstance> ApplicationUserPropertyInstances => Set<ApplicationUserPropertyInstance>();

	}
}