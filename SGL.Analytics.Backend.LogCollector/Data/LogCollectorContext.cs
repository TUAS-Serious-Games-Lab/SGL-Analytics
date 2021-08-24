using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Model;

namespace SGL.Analytics.Backend.LogCollector.Data {
	public class LogCollectorContext : DbContext {
		public LogCollectorContext(DbContextOptions<LogCollectorContext> options)
			: base(options) {
		}

		public DbSet<LogMetadata> LogMetadata => Set<LogMetadata>();
		public DbSet<Application> Applications => Set<Application>();
	}
}
