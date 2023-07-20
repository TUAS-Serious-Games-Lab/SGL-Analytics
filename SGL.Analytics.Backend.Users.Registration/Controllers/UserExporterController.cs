using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Application.Model;
using SGL.Analytics.DTO;
using SGL.Utilities.Backend.Security;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Registration.Controllers {
	[Route("api/analytics/user/v1")]
	[ApiController]
	[Authorize(Policy = "ExporterUser")]
	public class UserExporterController : ControllerBase {
		private readonly IUserManager userManager;
		private readonly ILogger<UserExporterController> logger;
		private readonly IMetricsManager metrics;

		public UserExporterController(IUserManager userManager, ILogger<UserExporterController> logger, IMetricsManager metrics) {
			this.userManager = userManager;
			this.logger = logger;
			this.metrics = metrics;
		}

		private ActionResult? GetCredentials(out string appName, out KeyId keyId, out string exporterDN, string operationName) {
			try {
				appName = User.GetClaim("appname");
				keyId = User.GetClaim<KeyId>("keyid", KeyId.TryParse!);
				exporterDN = User.GetClaim("exporter-dn");
				return null;
			}
			catch (ClaimException ex) {
				logger.LogError(ex, "{operationName} operation failed due to an error with the required security token claims.", operationName);
				metrics.HandleIncorrectSecurityTokenClaimsError();
				appName = null!;
				keyId = null!;
				exporterDN = null!;
				return Unauthorized("The operation failed due to a security token error.");
			}
		}

		private UserMetadataDTO ToDto(User user) {
			return new UserMetadataDTO(user.Id, user.Username, user.AppSpecificProperties, user.EncryptedProperties, user.PropertyEncryptionInfo);
		}

		[HttpGet()]
		public async Task<ActionResult<IEnumerable<Guid>>> GetUserIdList(CancellationToken ct = default) {
			var credResult = GetCredentials(out var appName, out var exporterKeyId, out var exporterDN, nameof(GetUserIdList));
			if (credResult != null) return credResult;
			try {
				logger.LogInformation("Listing user ids in application {appName} for exporter {keyId} ({exporterDN}).", appName, exporterKeyId, exporterDN);
				var userIds = await userManager.ListUserIdsAsync(appName, exporterDN, ct);
				var result = userIds.ToList();
				return result;
			}
			catch (OperationCanceledException) {
				throw;
			}
			catch (ApplicationDoesNotExistException ex) {
				logger.LogError(ex, "GetUserIdList GET request for non-existent application {appName} from exporter {keyId} ({exporterDN}).", appName, exporterKeyId, exporterDN);
				metrics.HandleUnknownAppError(appName);
				return NotFound($"Application {appName} not found.");
			}
			catch (Exception ex) {
				logger.LogError(ex, "GetUserIdList GET request for application {appName} from exporter {keyId} ({exporterDN}) failed due to unexpected exception.",
					appName, exporterKeyId, exporterDN);
				metrics.HandleUnexpectedError(appName, ex);
				throw;
			}
		}

		[HttpGet("all")]
		public async Task<ActionResult<IEnumerable<UserMetadataDTO>>> GetMetadataForAllUsers([FromQuery(Name = "recipient")] KeyId? recipientKeyId = null, CancellationToken ct = default) {
			var credResult = GetCredentials(out var appName, out var exporterKeyId, out var exporterDN, nameof(GetMetadataForAllUsers));
			if (credResult != null) return credResult;
			try {
				logger.LogInformation("Listing user metadata for all users in application {appName} with recipient keys for {recipientKeyId} for exporter {exporterKeyId} ({exporterDN}).",
				appName, recipientKeyId, exporterKeyId, exporterDN);
				var users = await userManager.ListUsersAsync(appName, recipientKeyId, exporterDN, ct);
				var result = users.Select(user => ToDto(user)).ToList();
				return result;
			}
			catch (OperationCanceledException) {
				throw;
			}
			catch (ApplicationDoesNotExistException ex) {
				logger.LogError(ex, "GetMetadataForAllUsers GET request for non-existent application {appName} from exporter {keyId} ({exporterDN}).", appName, exporterKeyId, exporterDN);
				metrics.HandleUnknownAppError(appName);
				return NotFound($"Application {appName} not found.");
			}
			catch (Exception ex) {
				logger.LogError(ex, "GetMetadataForAllUsers GET request for application {appName} from exporter {keyId} ({exporterDN}) failed due to unexpected exception.",
					appName, exporterKeyId, exporterDN);
				metrics.HandleUnexpectedError(appName, ex);
				throw;
			}
		}

		[HttpGet("{id:Guid}")]
		public async Task<ActionResult<UserMetadataDTO>> GetUserMetadataById(Guid id, [FromQuery(Name = "recipient")] KeyId? recipientKeyId = null, CancellationToken ct = default) {
			var credResult = GetCredentials(out var appName, out var exporterKeyId, out var exporterDN, nameof(GetUserMetadataById));
			if (credResult != null) return credResult;
			try {
				logger.LogInformation("Fetching user metadata for user {userId} in application {appName} with recipient key for {recipientKeyId} for exporter {exporterKeyId} ({exporterDN}).",
				id, appName, recipientKeyId, exporterKeyId, exporterDN);
				var user = await userManager.GetUserByIdAsync(id, recipientKeyId, fetchProperties: true, ct: ct);
				if (user == null) {
					logger.LogError("GetUserMetadataById GET request for application {appName} from exporter {keyId} ({exporterDN}) failed because the requested user {userId} was not found.",
						appName, exporterKeyId, exporterDN, id);
					metrics.HandleUserNotFoundError(appName);
					throw new UserNotFoundException($"User {id} not found.", id);
				}
				if (user.App.Name != appName) {
					logger.LogError("GetUserMetadataById GET request for user {userId} failed because the retrieved user is not associated with the application indicated by the auth token {reqAppName}, but with {userAppName}.",
						user.Id, appName, user.App.Name);
					metrics.HandleUserIdAppMismatchError(appName);
					throw new UserNotFoundException($"User {id} not found in app {appName}.", id);
				}
				var result = ToDto(user);
				return result;
			}
			catch (OperationCanceledException) {
				throw;
			}
			catch (ApplicationDoesNotExistException ex) {
				logger.LogError(ex, "GetUserMetadataById GET request for non-existent application {appName} from exporter {keyId} ({exporterDN}).", appName, exporterKeyId, exporterDN);
				metrics.HandleUnknownAppError(appName);
				return NotFound($"Application {appName} not found.");
			}
			catch (UserNotFoundException) {
				return NotFound($"User {id} not found in app {appName}.");
			}
			catch (Exception ex) {
				logger.LogError(ex, "GetUserMetadataById GET request for application {appName} from exporter {keyId} ({exporterDN}) failed due to unexpected exception.",
					appName, exporterKeyId, exporterDN);
				metrics.HandleUnexpectedError(appName, ex);
				throw;
			}
		}
	}
}
