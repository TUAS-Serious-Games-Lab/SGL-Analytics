using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Application.Services;
using System;
using SGL.Analytics.Utilities.Logging.FileLogging;
using SGL.Analytics.Backend.WebUtilities;
using SGL.Analytics.Backend.Logs.Infrastructure;

namespace SGL.Analytics.Backend.Logs.Collector {
	public class Startup {
		public Startup(IConfiguration configuration) {
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services) {
			services.Configure<FileLoggingProviderOptions>(config => {
				config.Constants.TryAdd("ServiceName", "SGL.Analytics.LogCollector");
			});

			services.AddControllers();

			services.UseJwtBearerAuthentication(Configuration);
			services.AddAuthorization(options => {
				options.AddPolicy("AuthenticatedAppUser", p => p.RequireClaim("userid").RequireClaim("appname"));
				options.DefaultPolicy = options.GetPolicy("AuthenticatedAppUser") ?? throw new InvalidOperationException("Couldn't find AuthenticatedAppUser policy.");
			});

			services.UseLogsBackendInfrastructure(Configuration);
			services.AddScoped<ILogManager, LogManager>();
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
			if (env.IsDevelopment()) {
				app.UseDeveloperExceptionPage();
			}
			else {
				app.UseLoggingExceptionHandler<Startup>();
			}

			app.UseHttpsRedirection();

			app.UseRouting();

			app.UseAuthentication();
			app.UseAuthorization();
			app.UseUserLogScoping();
			app.UseApplicationLogScoping();

			app.UseEndpoints(endpoints => {
				endpoints.MapControllers();
			});
		}
	}
}
