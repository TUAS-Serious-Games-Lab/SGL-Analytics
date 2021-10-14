using SGL.Analytics.Utilities.Logging.FileLogging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.WebUtilities {
	public static class FileLoggingPlaceholderExtensions {
		public static IFileLoggingProviderBuilder AddRequestScopePlaceholders(this IFileLoggingProviderBuilder builder) {
			builder.AddPlaceholder("RequestPath", m => {
				var scope = m.Scopes.FirstOrDefault(s => s.Contains("RequestPath")) ?? "";
				var parts = scope.Split(' ', ':');
				return parts.ElementAtOrDefault(1) ?? "No_Request_Path";
			});
			builder.AddPlaceholder("RequestId", m => {
				var scope = m.Scopes.FirstOrDefault(s => s.Contains("RequestId")) ?? "";
				var parts = scope.Split(' ', ':');
				return parts.ElementAtOrDefault(3) ?? "No_Request_Id";
			});
			return builder;
		}

		public static IFileLoggingProviderBuilder AddUserIdScopePlaceholder(this IFileLoggingProviderBuilder builder) {
			builder.AddPlaceholder("UserId", m => {
				var scope = m.Scopes.FirstOrDefault(s => s.Contains("UserId")) ?? "";
				var parts = scope.Split(':');
				return parts.ElementAtOrDefault(1) ?? "No_User_Id";
			});
			return builder;
		}
		public static IFileLoggingProviderBuilder AddAppNameScopePlaceholder(this IFileLoggingProviderBuilder builder) {
			builder.AddPlaceholder("AppName", m => {
				var scope = m.Scopes.FirstOrDefault(s => s.Contains("AppName")) ?? "";
				var parts = scope.Split(':');
				return parts.ElementAtOrDefault(1) ?? "No_App_Name";
			});
			return builder;
		}
	}
}
