using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.DTO;
using SGL.Utilities.Backend.AspNetCore;
using SGL.Utilities.Backend.Security;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Collector.Controllers {

	/// <summary>
	/// Encapsulates configuration options for <see cref="AnalyticsLogController"/>.
	/// </summary>
	public class AnalyticsLogOptions {
		/// <summary>
		/// Provides the name of the config section as a constant with the value <c>AnalyticsLog</c>;
		/// </summary>
		public const string AnalyticsLog = "AnalyticsLog";
		/// <summary>
		/// Specifies the size limit for uploaded log files in bytes. Defaults to 200 MiB.
		/// </summary>
		public long UploadSizeLimit { get; set; } = 200 * 1024 * 1024;
	}

	/// <summary>
	/// Provides extension methods used to add and configure services and middlewares required for the configurable size limit for uploaded log files.
	/// </summary>
	public static class AnalyticsLogUploadLimitExtensions {
		/// <summary>
		/// Adds the <see cref="IOptions{AnalyticsLogOptions}"/> config object to the dependency injection container.
		/// </summary>
		/// <param name="services">The service collection for the DI container.</param>
		/// <param name="config">The root config object too lookup the config section under.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		public static IServiceCollection UseAnalyticsLogUploadLimit(this IServiceCollection services, IConfiguration config) {
			services.Configure<AnalyticsLogOptions>(config.GetSection(AnalyticsLogOptions.AnalyticsLog));
			return services;
		}

		/// <summary>
		/// Adds a conditional middleware that sets the configured log file upload size limit for the relevant requests.
		/// </summary>
		/// <param name="app">The builder object for the app to configure.</param>
		/// <returns>A reference to <paramref name="app"/> for chaining.</returns>
		public static IApplicationBuilder UseAnalyticsLogUploadLimit(this IApplicationBuilder app) {
			app.UseWhen(context => context.Request.Path.StartsWithSegments("/api/analytics/log"), appBuild => {
				appBuild.Use((context, next) => {
					var options = context.RequestServices.GetRequiredService<IOptions<AnalyticsLogOptions>>();
					var bodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
					if (bodySizeFeature != null) {
						bodySizeFeature.MaxRequestBodySize = options.Value.UploadSizeLimit;
					}
					return next();
				});
			});
			return app;
		}
	}

	/// <summary>
	/// The controller class serving the <c>api/analytics/log</c> route that accepts uploads of analytics log files for SGL Analytics.
	/// </summary>
	[Route("api/analytics/log")]
	[ApiController]
	public class AnalyticsLogController : ControllerBase {

		private readonly ILogManager logManager;
		private readonly IApplicationRepository appRepo;
		private readonly ILogger<AnalyticsLogController> logger;
		private readonly IMetricsManager metrics;

		/// <summary>
		/// Instantiates the controller, injecting the required dependency objects.
		/// </summary>
		public AnalyticsLogController(ILogManager logManager, IApplicationRepository appRepo, ILogger<AnalyticsLogController> logger, IMetricsManager metrics) {
			this.logManager = logManager;
			this.appRepo = appRepo;
			this.logger = logger;
			this.metrics = metrics;
		}

		// POST: api/analytics/log
		/// <summary>
		/// Handles POST requests to <c>api/analytics/log</c> for analytics log files with the log contents and associated metadata.
		/// Request body shall consist of the raw log file contents. The associated metadata for the log file are accepted in the for of custom HTTP headers with the names of the properties of <see cref="LogMetadataDTO"/>.
		/// This route requires authorization using a JWT bearer token issued by the controller for <c>api/analytics/user/login</c> in the user registration service.
		/// If no such token is present, the authorization layer will reject the request and respond with a <see cref="StatusCodes.Status401Unauthorized"/>, containing a <c>WWW-Authenticate</c> header as an authorization challenge.
		/// Upon successful upload, the controller responds with a <see cref="StatusCodes.Status201Created"/>.
		/// If there is an error with either the JWT bearer token or with <paramref name="appApiToken"/>, the controller responds with a <see cref="StatusCodes.Status401Unauthorized"/>.
		/// If the log file is larger than the limit of 200 MiB, the controller responds with a <see cref="StatusCodes.Status413RequestEntityTooLarge"/>.
		/// Other errors are represented by the controller responding with a <see cref="StatusCodes.Status500InternalServerError"/>.
		/// </summary>
		/// <param name="appApiToken">The API token of the client application, provided by the HTTP header <c>App-API-Token</c>.</param>
		/// <param name="logMetadata">The metadata of the uploaded log file, provided as HTTP headers with the names of the properties of <see cref="LogMetadataDTO"/>.</param>
		/// <param name="ct">A cancellation token that is triggered when the client cancels the request.</param>
		[Consumes("application/octet-stream")]
		[ProducesResponseType(StatusCodes.Status201Created)]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(StatusCodes.Status413RequestEntityTooLarge)]
		[HttpPost]
		[Authorize]
		public async Task<ActionResult> IngestLog([FromHeader(Name = "App-API-Token")][StringLength(64, MinimumLength = 8)] string appApiToken, [DtoFromHeaderModelBinder] LogMetadataDTO logMetadata, CancellationToken ct = default) {
			Guid userId;
			string appName;
			try {
				userId = User.GetClaim<Guid>("userid", Guid.TryParse);
				appName = User.GetClaim("appname");
			}
			catch (ClaimException ex) {
				logger.LogError(ex, "IngestLog operation failed due to an error with the required security token claims.");
				metrics.HandleIncorrectSecurityTokenClaimsError();
				return Unauthorized("The operation failed due to a security token error.");
			}
			Domain.Entity.Application? app = null;
			try {
				app = await appRepo.GetApplicationByNameAsync(appName, ct: ct);
			}
			catch (OperationCanceledException) {
				logger.LogDebug("IngestLog POST request from user {userId} was cancelled while fetching application metadata.", userId);
				throw;
			}
			catch (Exception ex) {
				logger.LogError(ex, "IngestLog POST request from user {userId} failed due to an unexpected exception when fetching application metadata.", userId);
				metrics.HandleUnexpectedError(appName, ex);
				throw;
			}
			if (app is null) {
				logger.LogError("IngestLog POST request from user {userId} failed due to unknown application {appName}.", userId, appName);
				metrics.HandleUnknownAppError(appName);
				return Unauthorized();
			}
			else if (app.ApiToken != appApiToken) {
				logger.LogError("IngestLog POST request from user {userId} failed due to incorrect API token for application {appName}.", userId, appName);
				metrics.HandleIncorrectAppApiTokenError(appName);
				return Unauthorized();
			}

			try {
				await logManager.IngestLogAsync(userId, appName, logMetadata, Request.Body, Request.ContentLength, ct);
				metrics.HandleLogUploadedSuccessfully(appName);
				return StatusCode(StatusCodes.Status201Created);
			}
			catch (OperationCanceledException) {
				logger.LogDebug("IngestLog POST request from user {userId} was cancelled while fetching application metadata.", userId);
				throw;
			}
			catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413RequestEntityTooLarge) {
				logger.LogCritical("IngestLog POST request from user {userId} failed because the log file was too large for the server's limit. " +
					"The Content-Length given by the client was {size}.", userId, HttpContext.Request.ContentLength);
				metrics.HandleLogFileTooLargeError(appName);
				return AbortWithErrorObject(ex.StatusCode, "The log file's size exceeds the limit.");
			}
			catch (Exception ex) {
				logger.LogError(ex, "IngestLog POST request from user {userId} failed due to unexpected exception during log ingest.", userId);
				metrics.HandleUnexpectedError(appName, ex);
				throw;
			}
		}

		private AbortWithErrorObjectResult AbortWithErrorObject(int statusCode, object value) => new AbortWithErrorObjectResult(statusCode, value);

		private class AbortWithErrorObjectResult : ObjectResult {
			public AbortWithErrorObjectResult(int statusCode, object value) : base(value) {
				StatusCode = statusCode;
			}

			public override async Task ExecuteResultAsync(ActionContext context) {
				try {
					await base.ExecuteResultAsync(context);
					await context.HttpContext.Response.CompleteAsync();
				}
				finally {
					context.HttpContext.Abort();
				}
			}
		}
	}
}
