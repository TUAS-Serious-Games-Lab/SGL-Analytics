using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SGL.Analytics.Backend.WebUtilities {

	public static class LoggingExceptionHandlerExtensions {
		public static IApplicationBuilder UseLoggingExceptionHandler<TStartup>(this IApplicationBuilder app) {
			app.UseExceptionHandler(errorHandler => {
				errorHandler.Run(async context => {
					context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
					context.Response.ContentType = "text/plain";
					var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
					var logger = context.RequestServices.GetRequiredService<ILogger<TStartup>>();
					logger.LogError(exceptionHandlerPathFeature.Error, "Uncaught exception from request for path {path}.", exceptionHandlerPathFeature.Path);
					await context.Response.WriteAsync("Internal server error.");
					await context.Response.CompleteAsync();
				});
			});
			return app;
		}
	}
}
