using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Security {
	public static class OwnerAuthorizationExtensions {
		public static IServiceCollection AddOwnerAuthorizationHandler(this IServiceCollection services, IConfiguration config) {
			services.Configure<OwnerAuthorizationHandlerOptions>(config.GetSection(OwnerAuthorizationHandlerOptions.OwnerAuthorizationHandler));
			services.AddScoped<IAuthorizationHandler, OwnerAuthorizationHandler>();
			return services;
		}

		public static AuthorizationOptions AddOwnerPolicy(this AuthorizationOptions options) {
			options.AddPolicy("Owner", policy => policy.AddRequirements(new OwnerAuthorizationRequirement()));
			return options;
		}
	}

	public class OwnerAuthorizationRequirement : IAuthorizationRequirement { }

	public class OwnerAuthorizationHandlerOptions {
		public const string OwnerAuthorizationHandler = "OwnerAuthorizationHandler";
		public static readonly IEnumerable<string> DefaultOwnerNames = new List<string> {
			"userId", "user", "userid", "userID", "UserId", "UserID",
			"ownerId", "owner", "ownerid", "ownerID", "OwnerId", "OwnerID"
		};
		public IEnumerable<string> OwnerRouteValueNames { get; set; } = DefaultOwnerNames;
		public IEnumerable<string> OwnerHeaderNames { get; set; } = DefaultOwnerNames;
	}

	public class OwnerAuthorizationHandler : AuthorizationHandler<OwnerAuthorizationRequirement> {
		private OwnerAuthorizationHandlerOptions options;

		public OwnerAuthorizationHandler(IOptions<OwnerAuthorizationHandlerOptions> options) {
			this.options = options.Value;
		}

		protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OwnerAuthorizationRequirement requirement) {
			var currentUser = extractCurrentUserId(context);
			var targetOwner = extractTargetOwner(context);
			if (currentUser is not null && targetOwner is not null && targetOwner == currentUser) {
				context.Succeed(requirement);
			}
			return Task.CompletedTask;
		}

		private Guid? extractCurrentUserId(AuthorizationHandlerContext context) {
			var userIdClaim = context.User.FindFirst(c => c.Type.Equals("userid", StringComparison.OrdinalIgnoreCase));
			if (userIdClaim is null) return null;
			if (Guid.TryParse(userIdClaim.Value, out var userId)) return userId;
			return null;
		}

		private Guid? extractTargetOwner(AuthorizationHandlerContext context) {
			if (context.Resource is HttpContext http) {
				var routeData = http.GetRouteData();
				foreach (var name in options.OwnerRouteValueNames) {
					if (routeData.Values.TryGetValue(name, out var value)) {
						if (value is Guid id) {
							return id;
						}
						else if (value is string s) {
							if (Guid.TryParse(s, out var parsedId)) {
								return parsedId;
							}
						}
						// else, ignore the value and try next candidate
					}
				}
				foreach (var name in options.OwnerHeaderNames) {
					if (http.Request.Headers.TryGetValue(name, out var values)) {
						if (values.Count() != 1) continue;
						var value = values.Single();
						if (Guid.TryParse(value, out var parsedId)) {
							return parsedId;
						}
					}
				}
				return null;
			}
			else if (context.Resource is null) return null;
			else throw new NotImplementedException($"Don't know how to extract target owner user id for ressource type {context.Resource?.GetType().FullName ?? string.Empty}.");
		}
	}
}
