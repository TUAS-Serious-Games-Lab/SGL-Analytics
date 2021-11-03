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

	/// <summary>
	/// Provides extension methods to add per-user log scoping.
	/// </summary>
	public static class UserLogScopingExtensions {
		/// <summary>
		/// Enables a middleware that automatically adds a <c>UserId:{ID}</c> scope to requests where a <c>userid</c> claim is persent in <see cref="HttpContext.User"/>.
		/// </summary>
		/// <param name="builder">The application builder where the middleware should be enabled.</param>
		/// <returns>A reference to <paramref name="builder"/> for chaining.</returns>
		public static IApplicationBuilder UseUserLogScoping(this IApplicationBuilder builder) {
			return builder.UseMiddleware<UserLogScoping>();
		}

		/// <summary>
		/// Opens a <c>UserId:{ID}</c> logging scope.
		/// </summary>
		/// <typeparam name="T">The category type of the logger.</typeparam>
		/// <param name="logger">The logger to call <see cref="LoggerExtensions.BeginScope(ILogger, string, object[])"/> on.</param>
		/// <param name="userId">The id of the user for which to open the scope.</param>
		/// <returns>An <see cref="IDisposable"/> object that closes the scope when disposed.</returns>
		public static IDisposable BeginUserScope<T>(this ILogger<T> logger, string userId) {
			return logger.BeginScope("UserId:{0}", userId);
		}

		/// <summary>
		/// Opens a <c>UserId:{ID}</c> logging scope.
		/// </summary>
		/// <typeparam name="T">The category type of the logger.</typeparam>
		/// <param name="logger">The logger to call <see cref="LoggerExtensions.BeginScope(ILogger, string, object[])"/> on.</param>
		/// <param name="userId">The id of the user for which to open the scope.</param>
		/// <returns>An <see cref="IDisposable"/> object that closes the scope when disposed.</returns>
		public static IDisposable BeginUserScope<T>(this ILogger<T> logger, Guid userId) {
			return logger.BeginScope("UserId:{0}", userId);
		}
	}
}
