using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.DTO;
using SGL.Utilities.Backend.Applications;
using SGL.Utilities.Backend.AspNetCore;
using SGL.Utilities.Backend.Security;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Collector.Controllers {

	/// <summary>
	/// The controller class serving the <c>api/analytics/log</c> route that accepts uploads of analytics log files for SGL Analytics.
	/// </summary>
	[Route("api/analytics/log/v2")]
	[ApiController]
	public class AnalyticsLogController : ControllerBase {

		private readonly ILogManager logManager;
		private readonly IApplicationRepository<Domain.Entity.Application, ApplicationQueryOptions> appRepo;
		private readonly ILogger<AnalyticsLogController> logger;
		private readonly IMetricsManager metrics;

		/// <summary>
		/// Instantiates the controller, injecting the required dependency objects.
		/// </summary>
		public AnalyticsLogController(ILogManager logManager, IApplicationRepository<Domain.Entity.Application, ApplicationQueryOptions> appRepo, ILogger<AnalyticsLogController> logger, IMetricsManager metrics) {
			this.logManager = logManager;
			this.appRepo = appRepo;
			this.logger = logger;
			this.metrics = metrics;
		}

		[HttpGet("recipient-certificates")]
		[Produces("application/x-pem-file")]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		public async Task<ActionResult<IEnumerable<string>>> GetRecipientCertificates([FromQuery] string appName, [FromHeader(Name = "App-API-Token")][StringLength(64, MinimumLength = 8)] string appApiToken, CancellationToken ct = default) {
			var app = await appRepo.GetApplicationByNameAsync(appName, new ApplicationQueryOptions { FetchRecipients = true }, ct: ct);
			if (app == null) {
				return Unauthorized();
			}
			if (app.ApiToken != appApiToken) {
				return Unauthorized();
			}
			return Ok(app.DataRecipients.Select(r => r.CertificatePem));
		}

		// POST: api/analytics/log/v2
		/// <summary>
		/// Handles POST requests to <c>api/analytics/log/v2</c> for analytics log files with the log contents and associated metadata.
		/// Request body shall consist of two multipart/form-data sections:
		/// - The first section shall have a <c>Content-Type</c> of <c>application/json</c> an a <c>Content-Disposition</c> with name <c>metadata</c> and shall contain the metadata for the uploaded log file as a JSON-serialized <see cref="LogMetadataDTO"/> object.
		/// - The second section shall have a <c>Content-Type</c> of <c>application/octet-stream</c> an a <c>Content-Disposition</c> with name <c>content</c> and shall contain the raw log file contents, compressed and / or encrypted depending on <see cref="LogMetadataDTO.LogContentEncoding"/>.
		///
		/// This route requires authorization using a JWT bearer token issued by the controller for <c>api/analytics/user/login</c> in the user registration service.
		/// If no such token is present, the authorization layer will reject the request and respond with a <see cref="StatusCodes.Status401Unauthorized"/>, containing a <c>WWW-Authenticate</c> header as an authorization challenge.
		/// Upon successful upload, the controller responds with a <see cref="StatusCodes.Status201Created"/>.
		/// If there is an error with either the JWT bearer token or with <paramref name="appApiToken"/>, the controller responds with a <see cref="StatusCodes.Status401Unauthorized"/>.
		/// If the log file is larger than the limit configured in <c>AnalyticsLog:UploadSizeLimit</c>, the controller responds with a <see cref="StatusCodes.Status413RequestEntityTooLarge"/>.
		/// Errors with the request body data result in  <see cref="StatusCodes.Status400BadRequest"/>.
		/// Other errors are represented by the controller responding with a <see cref="StatusCodes.Status500InternalServerError"/>.
		/// </summary>
		/// <param name="appApiToken">The API token of the client application, provided by the HTTP header <c>App-API-Token</c>.</param>
		/// <param name="ct">A cancellation token that is triggered when the client cancels the request.</param>
		[HttpPost]
		[DisableFormValueModelBinding]
		[Consumes("multipart/form-data")]
		[ProducesResponseType(StatusCodes.Status201Created)]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(StatusCodes.Status413RequestEntityTooLarge)]
		[Authorize]
		[UseConfigurableUploadLimit]
		public async Task<ActionResult> IngestLog([FromHeader(Name = "App-API-Token")][StringLength(64, MinimumLength = 8)] string appApiToken, CancellationToken ct = default) {
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

			try {
				var streamingHelper = new MultipartStreamingHelper(Request,
					invalidContentTypeCallback: actualContentType => {
						logger.LogError("IngestLog operation from user {userId} of app {appName}: Invalid content-type {contentType}.", userId, appName, actualContentType);
						return BadRequest("Invalid content type.");
					},
					noBoundaryCallback: () => {
						logger.LogError("IngestLog operation from user {userId} of app {appName}: Missing multipart boundary in content-type.", userId, appName);
						return BadRequest("No multipart boundary found in content type.");
					},
					boundaryTooLongCallback: () => {
						logger.LogError("IngestLog operation from user {userId} of app {appName}: Multipart boundary too long.", userId, appName);
						return BadRequest("Multipart boundary too long.");
					},
					skippedUnexpectedSectionNameContentTypeCallback: (name, contentType) => logger.LogDebug("IngestLog operation from user {userId} of app {appName}: " +
						"Skipping unexpected body section with name {name} with content-type {contentType}.", userId, appName, name, contentType),
					skippedSectionWithoutValidContentDispositionCallback: contentType => logger.LogDebug("IngestLog operation from user {userId} of app {appName}: " +
						"Skipping unexpected body section without valid content disposition and with content-type {contentType}.", userId, appName, contentType),
					boundaryLengthLimit: 100);
				if (streamingHelper.InitError != null) {
					return streamingHelper.InitError;
				}

				if (!await streamingHelper.ReadUntilSection(ct, ("metadata", "application/json"), ("content", "application/octet-stream"))) {
					logger.LogError("IngestLog operation from user {userId} of app {appName}: Required request body sections (metadata,content) not present.", userId, appName);
					return BadRequest("Required request body sections not present.");
				}

				LogMetadataDTO logMetadata;
				if (streamingHelper.IsCurrentSection("metadata", "application/json")) {
					var metadata = await JsonSerializer.DeserializeAsync<LogMetadataDTO>(streamingHelper.Section!.Body, DTO.JsonOptions.RestOptions, ct);
					if (metadata == null) {
						logger.LogError("IngestLog operation from user {userId} of app {appName}: Invalid JSON for LogMetadataDTO in request body section 'metadata'.", userId, appName);
						throw new BadHttpRequestException("Invalid JSON for LogMetadataDTO in request body section 'metadata'.");
					}
					else if (ObjectValidator != null && !TryValidateModel(metadata)) {
						logger.LogError("IngestLog operation from user {userId} of app {appName}: Metadata JSON failed model state validation. Errors:\n{errors}\n-----\n", userId, appName,
							string.Join('\n', ModelState.SelectMany(kvp => kvp.Value?.Errors ?? new ModelErrorCollection()).Select(err => err.ErrorMessage)));
						return BadRequest(ModelState);
					}
					else {
						logMetadata = metadata;
					}
					await streamingHelper.ReadUntilSection(ct, ("content", "application/octet-stream"));
				}
				else {
					logger.LogError("IngestLog operation from user {userId} of app {appName}: Request body section 'content' was received before request body section 'metadata'. However, 'metadata' must be sent before 'content'.", userId, appName);
					return BadRequest("Request body section 'content' was received before request body section 'metadata', but they must be sent in opposite order.");
				}

				if (!streamingHelper.IsCurrentSection("content", "application/octet-stream")) {
					logger.LogError("IngestLog operation from user {userId} of app {appName}: Request body section 'content' not present.", userId, appName);
					return BadRequest("Request body section 'content' not present.");
				}

				await logManager.IngestLogAsync(userId, appName, appApiToken, logMetadata, streamingHelper.Section!.Body, ct);
				metrics.HandleLogUploadedSuccessfully(appName);
				logger.LogDebug("IngestLog operation from user {userId} of app {appName} successfully completed, uploaded log with id {id}.", userId, appName, logMetadata.LogFileId);
				return StatusCode(StatusCodes.Status201Created);
			}
			catch (OperationCanceledException) {
				logger.LogDebug("IngestLog POST request from user {userId} was cancelled while fetching application metadata.", userId);
				throw;
			}
			catch (ApplicationDoesNotExistException) {
				logger.LogError("IngestLog POST request from user {userId} failed due to unknown application {appName}.", userId, appName);
				metrics.HandleUnknownAppError(appName);
				return Unauthorized();
			}
			catch (ApplicationApiTokenMismatchException) {
				logger.LogError("IngestLog POST request from user {userId} failed due to incorrect API token for application {appName}.", userId, appName);
				metrics.HandleIncorrectAppApiTokenError(appName);
				return Unauthorized();
			}
			catch (MissingRecipientDataKeysForEncryptedDataException ex) {
				logger.LogError("IngestLog POST request from user {userId} in application {appName} failed due to incomplete cryptographic metadata.", userId, appName);
				metrics.HandleCryptoMetadataError(appName);
				return BadRequest(ex.Message);
			}
			catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413RequestEntityTooLarge) {
				logger.LogCritical("IngestLog POST request from user {userId} failed because the log file was too large for the server's limit. " +
					"The Content-Length given by the client was {size}.", userId, HttpContext.Request.ContentLength);
				metrics.HandleLogFileTooLargeError(appName);
				return AbortWithErrorObject(ex.StatusCode, "The log file's size exceeds the limit.");
			}
			catch (BadHttpRequestException ex) {
				// Logging is done at throwing site.
				return BadRequest(ex.Message);
			}
			catch (JsonException ex) {
				logger.LogError(ex, "IngestLog POST request from user {userId} of app {appName} failed due to invalid JSON in request body.", userId, appName);
				return BadRequest("Invalid JSON for metadata.");
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
