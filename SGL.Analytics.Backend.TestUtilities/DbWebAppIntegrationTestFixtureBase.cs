using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.TestUtilities {
	public class DbWebAppIntegrationTestFixtureBase<TContext, TStartup> : WebApplicationFactory<TStartup> where TContext : DbContext where TStartup : class {
		private readonly TestDatabase<TContext> db = new();

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			db.Dispose();
		}

		protected override void ConfigureWebHost(IWebHostBuilder builder) {

			builder.ConfigureTestServices(services => {
				var dbContextDescriptor = services.SingleOrDefault(sd => sd.ServiceType == typeof(TContext));
				if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);
				var dbContextOptionsDescriptor = services.SingleOrDefault(sd => sd.ServiceType == typeof(DbContextOptions<TContext>));
				if (dbContextOptionsDescriptor != null) services.Remove(dbContextOptionsDescriptor);
				services.AddDbContext<TContext>(options => options.UseSqlite(db.Connection));
				OverrideConfig(services);

				using (var context = Activator.CreateInstance(typeof(TContext), db.ContextOptions) as TContext ?? throw new InvalidOperationException()) {
					SeedDatabase(context);
				}
			});
		}

		protected virtual void OverrideConfig(IServiceCollection services) { }
		protected virtual void SeedDatabase(TContext context) { }
	}
}
