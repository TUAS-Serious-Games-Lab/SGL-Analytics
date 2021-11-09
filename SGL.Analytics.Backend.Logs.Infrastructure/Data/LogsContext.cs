using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.DTO;
using SGL.Utilities.Backend;

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

			var application = modelBuilder.Entity<Domain.Entity.Application>();
			application.Property(a => a.Name).HasMaxLength(128);
			application.HasIndex(a => a.Name).IsUnique();
			application.Property(a => a.ApiToken).HasMaxLength(64);
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
