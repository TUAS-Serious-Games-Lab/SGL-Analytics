using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Utilities.Backend;
using SGL.Utilities.Crypto.EntityFrameworkCore;
using SGL.Utilities.EntityFrameworkCore;

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
			modelBuilder.Ignore<SGL.Analytics.Backend.Domain.Entity.Application>();
			modelBuilder.Ignore<ApplicationCertificateBase>();
			var app = modelBuilder.Entity<ApplicationWithUserProperties>();
			app.HasIndex(a => a.Name).IsUnique();
			app.Property(a => a.Name).HasMaxLength(128);
			app.Property(a => a.ApiToken).HasMaxLength(64);
			app.Property(a => a.BasicFederationUpstreamAuthUrl).HasConversion<string>().HasMaxLength(255);

			app.OwnsMany(app => app.DataRecipients, r => {
				r.WithOwner(r => (ApplicationWithUserProperties)r.App);
				r.HasKey(r => new { r.AppId, r.PublicKeyId });
				r.Property(r => r.PublicKeyId).IsStoredAsByteArray().HasMaxLength(34);
				r.Property(r => r.Label).HasMaxLength(512);
			});

			var propDef = modelBuilder.Entity<ApplicationUserPropertyDefinition>();
			propDef.HasIndex(pd => (new { pd.AppId, pd.Name })).IsUnique();
			propDef.Property(pd => pd.Name).HasMaxLength(128);

			var userReg = modelBuilder.Entity<UserRegistration>();
			userReg.HasIndex(u => (new { u.AppId, u.Username })).IsUnique();
			userReg.Property(u => u.Username).HasMaxLength(64);
			userReg.Property(u => u.HashedSecret).HasMaxLength(128);
			userReg.Ignore(u => u.PropertyEncryptionInfo);
			userReg.HasIndex(u => new { u.AppId, u.BasicFederationUpstreamUserId }).IsUnique();
			userReg.OwnsMany(u => u.AppSpecificProperties, p => {
				p.WithOwner(p => p.User);
				p.HasIndex(pi => new { pi.DefinitionId, pi.UserId }).IsUnique();
				p.Property(pi => pi.DateTimeValue).IsStoredInUtc();
			});
			userReg.OwnsMany(u => u.PropertyRecipientKeys, rk => {
				rk.ToTable("UserPropertyRecipientKeys");
				rk.WithOwner().HasForeignKey(prk => prk.UserId).HasPrincipalKey(u => u.Id);
				rk.Property(prk => prk.RecipientKeyId).IsStoredAsByteArray().HasMaxLength(34);
				rk.HasKey(prk => new { prk.UserId, prk.RecipientKeyId });
			});
			userReg.Navigation(u => u.PropertyRecipientKeys).AutoInclude(false);

			var ekac = modelBuilder.Entity<ExporterKeyAuthCertificate>();
			ekac.HasKey(e => new { e.AppId, e.PublicKeyId });
			ekac.Property(e => e.PublicKeyId).IsStoredAsByteArray().HasMaxLength(34);
			ekac.Property(e => e.Label).HasMaxLength(512);
			ekac.HasOne(e => e.App).WithMany(a => a.AuthorizedExporters);

			var sgncert = modelBuilder.Entity<SignerCertificate>();
			sgncert.ToTable("SignerCertificates");
			sgncert.HasKey(e => new { e.AppId, e.PublicKeyId });
			sgncert.Property(e => e.PublicKeyId).IsStoredAsByteArray().HasMaxLength(34);
			sgncert.Property(e => e.Label).HasMaxLength(512);
			app.HasMany(a => a.SignerCertificates).WithOne(sc => (ApplicationWithUserProperties)sc.App);
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

		/// <summary>
		/// The accessor for the table containing <see cref="ExporterKeyAuthCertificates"/> objects.
		/// </summary>
		public DbSet<ExporterKeyAuthCertificate> ExporterKeyAuthCertificates => Set<ExporterKeyAuthCertificate>();
	}
}
