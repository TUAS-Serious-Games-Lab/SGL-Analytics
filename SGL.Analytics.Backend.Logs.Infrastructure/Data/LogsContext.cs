using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Logs.Infrastructure.Utilities;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Data {
	public class LogsContext : DbContext {
		public LogsContext(DbContextOptions<LogsContext> options)
			: base(options) {
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder) {
			var logMetadata = modelBuilder.Entity<LogMetadata>();
			logMetadata.HasIndex(lm => lm.AppId);
			logMetadata.HasIndex(lm => new { lm.AppId, lm.UserId });
			logMetadata.Property(lm => lm.CreationTime).IsStoredInUtc();
			logMetadata.Property(lm => lm.EndTime).IsStoredInUtc();
			logMetadata.Property(lm => lm.UploadTime).IsStoredInUtc();
			logMetadata.Property(lm => lm.FilenameSuffix).HasMaxLength(16);

			var application = modelBuilder.Entity<Domain.Entity.Application>();
			application.Property(a => a.Name).HasMaxLength(128);
			application.HasIndex(a => a.Name).IsUnique();
			application.Property(a => a.ApiToken).HasMaxLength(64);
		}

		public DbSet<LogMetadata> LogMetadata => Set<LogMetadata>();
		public DbSet<Domain.Entity.Application> Applications => Set<Domain.Entity.Application>();
	}
}
