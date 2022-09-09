using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.DTO;
using SGL.Utilities.Backend;
using SGL.Utilities.Crypto.EntityFrameworkCore;
using SGL.Utilities.EntityFrameworkCore;
using System;
using System.Linq;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Data {
	/// <summary>
	/// The <see cref="DbContext"/> class for the database of the SGL Analytics logs collector service.
	/// </summary>
	public class LogsContext : DbContext {
		/// <summary>
		/// Instantiates the context with the given options.
		/// </summary>
		/// <param name="options">The options for the database access.</param>
		public LogsContext(DbContextOptions<LogsContext> options)
			: base(options) {
		}

		/// <summary>
		/// Configures the model of the database schema.
		/// This is called by Entity Framework Core.
		/// </summary>
		/// <param name="modelBuilder">The builder to use for configuring the model.</param>
		protected override void OnModelCreating(ModelBuilder modelBuilder) {
			var logMetadata = modelBuilder.Entity<LogMetadata>();
			logMetadata.HasIndex(lm => lm.AppId);
			logMetadata.HasIndex(lm => new { lm.AppId, lm.UserId });
			logMetadata.Property(lm => lm.CreationTime).IsStoredInUtc();
			logMetadata.Property(lm => lm.EndTime).IsStoredInUtc();
			logMetadata.Property(lm => lm.UploadTime).IsStoredInUtc();
			logMetadata.Property(lm => lm.FilenameSuffix).HasMaxLength(16);
			logMetadata.Property(lm => lm.Encoding).HasDefaultValue(LogContentEncoding.GZipCompressed);
			logMetadata.Ignore(lm => lm.EncryptionInfo);
			logMetadata.OwnsMany(lm => lm.RecipientKeys, rk => {
				rk.WithOwner().HasForeignKey(mrk => mrk.LogId).HasPrincipalKey(m => m.Id);
				rk.Property(mrk => mrk.RecipientKeyId).IsStoredAsByteArray().HasMaxLength(34);
				rk.HasKey(mrk => new { mrk.LogId, mrk.RecipientKeyId });
			});
			// TODO: Reactive if we need QueryOptions for log metadata.
			//logMetadata.Navigation(m => m.RecipientKeys).AutoInclude(false);

			var application = modelBuilder.Entity<Domain.Entity.Application>();
			application.Property(a => a.Name).HasMaxLength(128);
			application.HasIndex(a => a.Name).IsUnique();
			application.Property(a => a.ApiToken).HasMaxLength(64);

			application.OwnsMany(app => app.DataRecipients, r => {
				r.WithOwner(r => r.App);
				r.HasKey(r => new { r.AppId, r.PublicKeyId });
				r.Property(r => r.PublicKeyId).IsStoredAsByteArray().HasMaxLength(33);
				r.Property(r => r.Label).HasMaxLength(128);
			});
		}

		/// <summary>
		/// The accessor for the table containing <see cref="LogMetadata"/> objects.
		/// </summary>
		public DbSet<LogMetadata> LogMetadata => Set<LogMetadata>();
		/// <summary>
		/// The accessor for the table containing <see cref="Domain.Entity.Application"/> objects.
		/// </summary>
		public DbSet<Domain.Entity.Application> Applications => Set<Domain.Entity.Application>();
	}
}
