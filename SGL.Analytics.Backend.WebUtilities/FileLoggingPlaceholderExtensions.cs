using SGL.Analytics.Utilities.Logging.FileLogging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.WebUtilities {

	/// <summary>
	/// Provides extension methods to add placeholders to a <see cref="FileLoggingProvider"/> that lookup webapp- / webapi-centric scope data.
	/// </summary>
	public static class FileLoggingPlaceholderExtensions {
		/// <summary>
		/// Adds the <c>RequestPath</c> and <c>RequestId</c> placeholders that lookup a <c>RequestPath:{RequestPath} RequestId:{RequestId}</c> logging scope and if found, provide the corresponding value.
		/// If no such scope is present for a <see cref="LogMessage"/>, <c>"No_Request_Path"</c> and <c>"No_Request_Id"</c> are returned respectively instead.
		/// </summary>
		/// <param name="builder">The builder for the <see cref="FileLoggingProvider"/> where the placeholders shall be added.</param>
		/// <returns>A reference to <paramref name="builder"/> for chaining.</returns>
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

		/// <summary>
		/// Adds the <c>UserId</c> placeholder that looksup a <c>UserId:{UserId}</c> logging scope and if found, provides the id given there.
		/// If no such scope is present for a <see cref="LogMessage"/>, <c>"No_User_Id"</c> is returned instead.
		/// This placeholder can e.g. be used to implement per-user log files.
		/// </summary>
		/// <param name="builder">The builder for the <see cref="FileLoggingProvider"/> where the placeholder shall be added.</param>
		/// <returns>A reference to <paramref name="builder"/> for chaining.</returns>
		public static IFileLoggingProviderBuilder AddUserIdScopePlaceholder(this IFileLoggingProviderBuilder builder) {
			builder.AddPlaceholder("UserId", m => {
				var scope = m.Scopes.FirstOrDefault(s => s.Contains("UserId")) ?? "";
				var parts = scope.Split(':');
				return parts.ElementAtOrDefault(1) ?? "No_User_Id";
			});
			return builder;
		}
		/// <summary>
		/// Adds the <c>AppName</c> placeholder that looksup a <c>AppName:{AppName}</c> logging scope and if found, provides the name given there.
		/// If no such scope is present for a <see cref="LogMessage"/>, <c>"No_App_Name"</c> is returned instead.
		/// This placeholder can e.g. be used to implement per-app log files.
		/// </summary>
		/// <param name="builder">The builder for the <see cref="FileLoggingProvider"/> where the placeholder shall be added.</param>
		/// <returns>A reference to <paramref name="builder"/> for chaining.</returns>
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
