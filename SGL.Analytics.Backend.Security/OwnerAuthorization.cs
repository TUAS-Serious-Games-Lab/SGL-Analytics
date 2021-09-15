using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Security {
	public static class OwnerAuthorizationExtensions {
		public static IServiceCollection AddOwnerAuthorizationHandler(this IServiceCollection services, IConfiguration config) {
			services.AddScoped<IAuthorizationHandler, OwnerAuthorizationHandler>();
			return services;
		}

		public static AuthorizationOptions AddOwnerPolicies(this AuthorizationOptions options) {
			options.AddPolicy("RouteOwnerUserId", policy => policy.AddRequirements(new OwnerAuthorizationRequirement(OwnerAuthorizationSource.Route, "UserId")));
			options.AddPolicy("HeaderOwnerUserId", policy => policy.AddRequirements(new OwnerAuthorizationRequirement(OwnerAuthorizationSource.Header, "UserId")));
			return options;
		}
	}

	public enum OwnerAuthorizationSource { Route, Header }

	public class OwnerAuthorizationRequirement : IAuthorizationRequirement {
		public OwnerAuthorizationSource OwnerParamSource { get; set; }
		public string OwnerParamName { get; set; }

		public OwnerAuthorizationRequirement(OwnerAuthorizationSource ownerParamSource, string ownerParamName) {
			OwnerParamSource = ownerParamSource;
			OwnerParamName = ownerParamName;
		}
	}

	public class OwnerAuthorizationHandler : AuthorizationHandler<OwnerAuthorizationRequirement> {
		private ILogger<OwnerAuthorizationHandler> logger;

		public OwnerAuthorizationHandler(ILogger<OwnerAuthorizationHandler> logger) {
			this.logger = logger;
		}

		protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OwnerAuthorizationRequirement requirement) {
			var currentUser = extractCurrentUserId(context);
			var targetOwner = requirement.OwnerParamSource switch {
				OwnerAuthorizationSource.Route => extractRouteOwner(context, requirement.OwnerParamName),
				OwnerAuthorizationSource.Header => extractHeaderOwner(context, requirement.OwnerParamName),
				_ => null
			};
			if (currentUser is null || targetOwner is null) return Task.CompletedTask;
			if (targetOwner == currentUser) {
				logger.LogInformation("Current user id and owner id match, granting access based on ownership.");
				context.Succeed(requirement);
			}
			else {
				logger.LogInformation("Current user id and owner id do NOT match, NOT granting access based on ownership. Other policies may still allow access.");
			}
			return Task.CompletedTask;
		}

		private Guid? extractCurrentUserId(AuthorizationHandlerContext context) {
			var userIdClaim = context.User.FindFirst(c => c.Type.Equals("userid", StringComparison.OrdinalIgnoreCase));
			if (userIdClaim is null) {
				logger.LogDebug("Found no 'userid' claim. As the current user has no id, we can't check it against any owner parameters.");
				return null;
			}
			if (Guid.TryParse(userIdClaim.Value, out var userId)) {
				logger.LogDebug("Found valid 'userid' claim.");
				return userId;
			}
			logger.LogDebug("The 'userid' claim of the current user could not be parsed as a guid, which is the expected user id type. We thus have no user to check againts any owner parameters.");
			return null;
		}

		private Guid? extractRouteOwner(AuthorizationHandlerContext context, string name) {
			if (context.Resource is HttpContext http) {
				var routeData = http.GetRouteData();
				if (routeData.Values.TryGetValue(name, out var value)) {
					if (value is Guid id) {
						logger.LogDebug("Found owner route parameter '{name}' with guid value.", name);
						return id;
					}
					else if (value is string s) {
						if (Guid.TryParse(s, out var parsedId)) {
							logger.LogDebug("Found owner route parameter '{name}' with a valid string value.", name);
							return parsedId;
						}
						else {
							logger.LogWarning("Found owner route parameter '{name}' of type string, but it could not be parsed into a valid guid.", name);
							return null;
						}
					}
					else {
						logger.LogWarning("Found owner route parameter '{name}' of unexpected type '{type}'.", name, value?.GetType()?.FullName ?? ">null<");
						return null;
					}
				}
				else {
					logger.LogWarning("Found no valid owner parameter from route.");
					return null;
				}
			}
			else if (context.Resource is null) {
				logger.LogWarning("Can't extract owner information from null ressource.");
				return null;
			}
			else throw new NotImplementedException($"Don't know how to extract target owner user id for ressource type {context.Resource?.GetType().FullName ?? string.Empty}.");
		}
		private Guid? extractHeaderOwner(AuthorizationHandlerContext context, string name) {
			if (context.Resource is HttpContext http) {
				if (http.Request.Headers.TryGetValue(name, out var values)) {
					var value = values.First(); // Using First matches the behavior of HeaderDtroModelBinder from SGL.Analytics.Backend.WebUtilities.
												// This ensures, users can't pass a different id to the model binder than to the OwnerAuthorizationHandler.
					if (Guid.TryParse(value, out var parsedId)) {
						logger.LogDebug("Found valid owner header '{name}'.", name);
						return parsedId;
					}
					else {
						logger.LogWarning("Found owner header '{name}', but it could not be parsed into a valid guid.", name);
						return null;
					}
				}
				else {
					logger.LogWarning("Found no valid owner parameter from header.");
					return null;
				}
			}
			else if (context.Resource is null) {
				logger.LogWarning("Can't extract owner information from null ressource.");
				return null;
			}
			else throw new NotImplementedException($"Don't know how to extract target owner user id for ressource type {context.Resource?.GetType().FullName ?? string.Empty}.");
		}
	}
}
