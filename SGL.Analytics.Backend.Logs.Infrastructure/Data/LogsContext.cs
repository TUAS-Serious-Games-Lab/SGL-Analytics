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
			modelBuilder.Entity<LogMetadata>().HasIndex(lm => lm.AppId);
			modelBuilder.Entity<LogMetadata>().HasIndex(lm => new { lm.AppId, lm.UserId });
			modelBuilder.Entity<LogMetadata>().Property(lm => lm.CreationTime).IsStoredInUtc();
			modelBuilder.Entity<LogMetadata>().Property(lm => lm.EndTime).IsStoredInUtc();
			modelBuilder.Entity<LogMetadata>().Property(lm => lm.UploadTime).IsStoredInUtc();
			modelBuilder.Entity<Domain.Entity.Application>().HasIndex(a => a.Name).IsUnique();
		}

		public DbSet<LogMetadata> LogMetadata => Set<LogMetadata>();
		public DbSet<Domain.Entity.Application> Applications => Set<Domain.Entity.Application>();
	}
}
