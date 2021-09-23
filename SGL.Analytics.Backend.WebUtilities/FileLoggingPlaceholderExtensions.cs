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
				var scope0 = m.Scopes.FirstOrDefault() ?? "";
				var parts = scope0.Split(' ', ':');
				return parts.ElementAtOrDefault(1) ?? "No_Request_Path";
			});
			builder.AddPlaceholder("RequestId", m => {
				var scope0 = m.Scopes.FirstOrDefault() ?? "";
				var parts = scope0.Split(' ', ':');
				return parts.ElementAtOrDefault(3) ?? "No_Request_Id";
			});
			return builder;
		}
	}
}
