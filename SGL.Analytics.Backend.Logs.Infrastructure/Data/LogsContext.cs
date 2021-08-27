using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Data {
	public class LogsContext : DbContext {
		public LogsContext(DbContextOptions<LogsContext> options)
			: base(options) {
		}

		public DbSet<LogMetadata> LogMetadata => Set<LogMetadata>();
		public DbSet<Domain.Entity.Application> Applications => Set<Domain.Entity.Application>();
	}
}
