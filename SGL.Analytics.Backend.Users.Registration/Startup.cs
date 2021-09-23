using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using SGL.Analytics.Backend.Security;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Application.Services;
using SGL.Analytics.Backend.Users.Infrastructure.Services;
using SGL.Analytics.Utilities.Logging.FileLogging;

namespace SGL.Analytics.Backend.Users.Registration {
	public class Startup {
		public Startup(IConfiguration configuration) {
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services) {
			services.Configure<FileLoggingProviderOptions>(config => {
				config.Constants.TryAdd("ServiceName", "SGL.Analytics.UserRegistration");
			});

			services.AddControllers();

			services.AddDbContext<UsersContext>(options =>
					options.UseNpgsql(Configuration.GetConnectionString("UsersContext")));

			services.AddScoped<IApplicationRepository, DbApplicationRepository>();
			services.AddScoped<IUserRepository, DbUserRepository>();
			services.AddScoped<IUserManager, UserManager>();
			services.AddScoped<ILoginService, JwtLoginService>();
			services.UseJwtLoginService(Configuration);
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
			if (env.IsDevelopment()) {
				app.UseDeveloperExceptionPage();
			}

			app.UseHttpsRedirection();

			app.UseRouting();

			app.UseAuthorization();

			app.UseEndpoints(endpoints => {
				endpoints.MapControllers();
			});
		}
	}
}
