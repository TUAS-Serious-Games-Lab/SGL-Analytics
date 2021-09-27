using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.WebUtilities {
	public class UserLogScoping {
		private readonly RequestDelegate next;
		private readonly ILogger<UserLogScoping> logger;

		public UserLogScoping(RequestDelegate next, ILogger<UserLogScoping> logger) {
			this.next = next;
			this.logger = logger;
		}

		public Task Invoke(HttpContext httpContext) {
			var userid = httpContext.User?.Claims?.FirstOrDefault(c => c.Type.Equals("userid", StringComparison.OrdinalIgnoreCase))?.Value;
			if (userid != null) {
				using (var scope = logger.BeginScope("UserId:{0}", userid)) {
					return next(httpContext);
				}
			}
			else {
				return next(httpContext);
			}
		}
	}

	public static class UserLogScopingExtensions {
		public static IApplicationBuilder UseUserLogScoping(this IApplicationBuilder builder) {
			return builder.UseMiddleware<UserLogScoping>();
		}
	}
}
