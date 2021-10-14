using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.WebUtilities {
	public class ApplicationLogScoping {
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
	public static class ApplicationLogScopingExtensions {
		public static IApplicationBuilder UseApplicationLogScoping(this IApplicationBuilder builder) {
			return builder.UseMiddleware<ApplicationLogScoping>();
		}

		public static IDisposable BeginApplicationScope<T>(this ILogger<T> logger, string appName) {
			return logger.BeginScope("AppName:{0}", appName);
		}
	}
}
