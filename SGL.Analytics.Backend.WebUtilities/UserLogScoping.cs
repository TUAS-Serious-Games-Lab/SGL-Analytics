using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.WebUtilities {
	internal class UserLogScoping {
		private readonly RequestDelegate next;
		private readonly ILogger<UserLogScoping> logger;

		public UserLogScoping(RequestDelegate next, ILogger<UserLogScoping> logger) {
			this.next = next;
			this.logger = logger;
		}

		public async Task Invoke(HttpContext httpContext) {
			var userid = httpContext.User?.Claims?.FirstOrDefault(c => c.Type.Equals("userid", StringComparison.OrdinalIgnoreCase))?.Value;
			if (userid != null) {
				using (var scope = logger.BeginUserScope(userid)) {
					await next(httpContext);
				}
			}
			else {
				await next(httpContext);
			}
		}
	}

	public static class UserLogScopingExtensions {
		public static IApplicationBuilder UseUserLogScoping(this IApplicationBuilder builder) {
			return builder.UseMiddleware<UserLogScoping>();
		}

		public static IDisposable BeginUserScope<T>(this ILogger<T> logger, string userId) {
			return logger.BeginScope("UserId:{0}", userId);
		}
		public static IDisposable BeginUserScope<T>(this ILogger<T> logger, Guid userId) {
			return logger.BeginScope("UserId:{0}", userId);
		}
	}
}
