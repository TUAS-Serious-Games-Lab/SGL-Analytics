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

		public DbSet<SGL.Analytics.Backend.Model.LogMetadata> LogMetadata { get; set; }
	}
}
