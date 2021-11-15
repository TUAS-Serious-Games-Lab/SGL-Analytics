using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Application.Services;
using System;
using SGL.Utilities.Logging.FileLogging;
using SGL.Utilities.Backend.AspNetCore;
using SGL.Analytics.Backend.Logs.Infrastructure;
using SGL.Analytics.Backend.Logs.Infrastructure.Services;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;

namespace SGL.Analytics.Backend.Logs.Collector {
	/// <summary>
	/// Configures the hosting environment for the logs collector service.
	/// </summary>
	public class Startup {
		/// <summary>
		/// Instantiates the startup class using the give root configuration object.
		/// </summary>
		public Startup(IConfiguration configuration) {
			Configuration = configuration;
		}

		/// <summary>
		/// Provides the configuration root.
		/// </summary>
		public IConfiguration Configuration { get; }

		/// <summary>
		/// This method gets called by the runtime to add services to the container.
		/// </summary>
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

			services.AddHealthChecks()
				.AddCheck<LogFileRepositoryHealthCheck>("log_file_repository_health_check")
				.AddDbContextCheck<LogsContext>("db_health_check");
		}

		/// <summary>
		/// This method gets called by the runtime to configure the HTTP request pipeline. 
		/// </summary>
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
				endpoints.MapHealthChecks("/health").RequireHost($"localhost:{Configuration["ManagementPort"]}");
			});
		}
	}
}
