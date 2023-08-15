using Prometheus;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Application.Services;
using SGL.Utilities.Backend.AspNetCore;
using SGL.Utilities.Backend.Security;
using SGL.Utilities.Logging.FileLogging;

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
			config.Constants.TryAdd("ServiceName", "SGL.Analytics.Test.Upstream");
		});

		services.UseJwtLoginService(Configuration);
		services.UseJwtExplicitTokenService(Configuration);
		services.UseJwtBearerAuthentication(Configuration);
		services.AddAuthorization(options => {
			options.AddPolicy("AuthenticatedAppUser", p => p.RequireClaim("userid").RequireClaim("appname"));
			options.DefaultPolicy = options.GetPolicy("AuthenticatedAppUser") ?? throw new InvalidOperationException("Couldn't find AuthenticatedAppUser policy.");
		});

		services.AddHealthChecks().ForwardToPrometheus();

		DiagnosticSourceAdapter.StartListening();
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