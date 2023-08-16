using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prometheus;
using SGL.Analytics.Backend.Users.Application;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Application.Services;
using SGL.Analytics.Backend.Users.Infrastructure;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using SGL.Utilities.Backend;
using SGL.Utilities.Backend.AspNetCore;
using SGL.Utilities.Backend.Security;
using SGL.Utilities.Crypto.AspNetCore;
using SGL.Utilities.Logging.FileLogging;
using System;

namespace SGL.Analytics.Backend.Users.Registration {
	/// <summary>
	/// Configures the hosting environment for the user registration service.
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
				config.Constants.TryAdd("ServiceName", "SGL.Analytics.UserRegistration");
			});

			services.AddControllers(options => options.AddPemFormatters().AddKeyIdModelBinding());

			services.UseUsersBackendInfrastructure(Configuration);
			services.UseUsersBackendAppplicationLayer(Configuration);
			services.UseJwtLoginService(Configuration);
			services.UseJwtExplicitTokenService(Configuration);
			services.UseJwtBearerAuthentication(Configuration);
			services.AddAuthorization(options => {
				options.AddPolicy("ExporterUser", p => p.RequireClaim("keyid").RequireClaim("appname").RequireClaim("exporter-dn"));
			});

			services.AddLazyScoped<IUpstreamTokenClient>()
				.AddHttpClient<IUpstreamTokenClient, UpstreamTokenClient>((httpC, services) => new UpstreamTokenClient(httpC));

			services.AddModelStateValidationErrorLogging((err, ctx) =>
				ctx.HttpContext.RequestServices.GetService<IMetricsManager>()?
				.HandleModelStateValidationError(err.ErrorMessage));

			services.AddHealthChecks()
				.AddDbContextCheck<UsersContext>("db_health_check")
				.ForwardToPrometheus();

			DiagnosticSourceAdapter.StartListening();
			services.UseApplicationMetricsService<ApplicationMetricsService>(Configuration);
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

			app.UseHttpMetrics();

			app.UseAuthentication();
			app.UseAuthorization();

			app.UseEndpoints(endpoints => {
				endpoints.MapControllers();
				endpoints.MapHealthChecks("/health").RequireHost($"localhost:{Configuration["ManagementPort"]}");
				endpoints.MapMetrics().RequireHost($"*:{Configuration["ManagementPort"]}");
			});
		}
	}
}
