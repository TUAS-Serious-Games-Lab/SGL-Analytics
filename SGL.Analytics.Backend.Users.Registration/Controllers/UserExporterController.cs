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

		private ActionResult? GetCredentials(out string appName, out KeyId keyId, out string exporterDN) {
			try {
				appName = User.GetClaim("appname");
				keyId = User.GetClaim<KeyId>("keyid", KeyId.TryParse!);
				exporterDN = User.GetClaim("exporter-dn");
				return null;
			}
			catch (ClaimException ex) {
				logger.LogError(ex, "GetLogIds operation failed due to an error with the required security token claims.");
				metrics.HandleIncorrectSecurityTokenClaimsError();
				appName = null!;
				keyId = null!;
				exporterDN = null!;
				return Unauthorized("The operation failed due to a security token error.");
			}
		}

		private UserMetadataDTO ToDto(User user) {
			return new UserMetadataDTO(user.Id, user.App.Name, user.Username, user.AppSpecificProperties, user.EncryptedProperties, user.PropertyEncryptionInfo);
		}

		[HttpGet()]
		public async Task<ActionResult<IEnumerable<Guid>>> GetUserIdList(CancellationToken ct = default) {
			var credResult = GetCredentials(out var appName, out var exporterKeyId, out var exporterDN);
			if (credResult != null) return credResult;
			logger.LogInformation("Listing user ids in application {appName} for exporter {keyId} ({exporterDN}).", appName, exporterKeyId, exporterDN);
			var userIds = await userManager.ListUserIdsAsync(appName, exporterDN, ct);
			var result = userIds.ToList();
			return result;
		}

		[HttpGet("all")]
		public async Task<ActionResult<IEnumerable<UserMetadataDTO>>> GetMetadataForAllUsers([FromQuery] KeyId? recipientKeyId = null, CancellationToken ct = default) {
			var credResult = GetCredentials(out var appName, out var exporterKeyId, out var exporterDN);
			if (credResult != null) return credResult;
			logger.LogInformation("Listing user metadata for all users in application {appName} with recipient keys for {recipientKeyId} for exporter {exporterKeyId} ({exporterDN}).",
				appName, recipientKeyId, exporterKeyId, exporterDN);
			var users = await userManager.ListUsersAsync(appName, recipientKeyId, exporterDN, ct);
			var result = users.Select(user => ToDto(user)).ToList();
			return result;
		}

		[HttpGet("{id:Guid}")]
		public async Task<ActionResult<UserMetadataDTO>> GetUserMetadataById(Guid id, [FromQuery] KeyId? recipientKeyId = null, CancellationToken ct = default) {
			var credResult = GetCredentials(out var appName, out var exporterKeyId, out var exporterDN);
			if (credResult != null) return credResult;
			logger.LogInformation("Fetching user metadata for user {userId} in application {appName} with recipient key for {recipientKeyId} for exporter {exporterKeyId} ({exporterDN}).",
				id, appName, recipientKeyId, exporterKeyId, exporterDN);
			var user = await userManager.GetUserByIdAsync(id, ct: ct);
			if (user == null) {
				throw new UserNotFoundException($"User {id} not found.", id);
			}
			var result = ToDto(user);
			return result;
		}
	}
}
