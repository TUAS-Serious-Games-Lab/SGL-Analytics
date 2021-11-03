using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.WebUtilities {
	internal class ApplicationLogScoping {
		private readonly RequestDelegate next;
		private readonly ILogger<ApplicationLogScoping> logger;

		public ApplicationLogScoping(RequestDelegate next, ILogger<ApplicationLogScoping> logger) {
			this.next = next;
			this.logger = logger;
		}

		public async Task Invoke(HttpContext httpContext) {
			var appname = httpContext.User?.Claims?.FirstOrDefault(c => c.Type.Equals("appname", StringComparison.OrdinalIgnoreCase))?.Value;
			if (appname != null) {
				using (var scope = logger.BeginApplicationScope(appname)) {
					await next(httpContext);
				}
			}
			else {
				await next(httpContext);
			}
		}
	}

	/// <summary>
	/// Provides extension methods to add per-application log scoping.
	/// </summary>
	public static class ApplicationLogScopingExtensions {
		/// <summary>
		/// Enables a middleware that automatically adds an <c>AppName:{Name}</c> scope to requests where an <c>appname</c> claim is persent in <see cref="HttpContext.User"/>.
		/// </summary>
		/// <param name="builder">The application builder where the middleware should be enabled.</param>
		/// <returns>A reference to <paramref name="builder"/> for chaining.</returns>
		public static IApplicationBuilder UseApplicationLogScoping(this IApplicationBuilder builder) {
			return builder.UseMiddleware<ApplicationLogScoping>();
		}

		/// <summary>
		/// Opens an <c>AppName:{Name}</c> logging scope.
		/// </summary>
		/// <typeparam name="T">The category type of the logger.</typeparam>
		/// <param name="logger">The logger to call <see cref="LoggerExtensions.BeginScope(ILogger, string, object[])"/> on.</param>
		/// <param name="appName">The name of the application for which to open the scope.</param>
		/// <returns>An <see cref="IDisposable"/> object that closes the scope when disposed.</returns>
		public static IDisposable BeginApplicationScope<T>(this ILogger<T> logger, string appName) {
			return logger.BeginScope("AppName:{0}", appName);
		}
	}
}
