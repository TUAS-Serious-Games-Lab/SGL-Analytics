using SGL.Utilities.Validation;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Specifies the data transferred from the client to the server when the client attempts to login a user.
	/// </summary>
	[JsonConverter(typeof(LoginRequestDTOJsonConverter))]
	public abstract class LoginRequestDTO {
		/// <summary>
		/// The unique technical name of the client application performing the login.
		/// </summary>
		[PlainName]
		[StringLength(128, MinimumLength = 1)]
		public string AppName { get; private set; }
		/// <summary>
		/// The application authentication token of the client application performing the login.
		/// </summary>
		[StringLength(64, MinimumLength = 8)]
		public string AppApiToken { get; private set; }
		/// <summary>
		/// The secret string to authenticate the user.
		/// This can be an auto-generated random string or a user-specified password, depending on the application.
		/// </summary>
		[StringLength(128, MinimumLength = 8)]
		public string UserSecret { get; private set; }

		/// <summary>
		/// Creates a new DTO with the given data.
		/// </summary>
		/// <param name="appName">The unique technical name of the client application performing the login.</param>
		/// <param name="appApiToken">The application authentication token of the client application performing the login.</param>
		/// <param name="userSecret">The secret string to authenticate the user.</param>
		public LoginRequestDTO([PlainName][StringLength(128, MinimumLength = 1)] string appName,
			[StringLength(64, MinimumLength = 8)] string appApiToken, [StringLength(128, MinimumLength = 8)] string userSecret) =>
			(AppName, AppApiToken, UserSecret) = (appName, appApiToken, userSecret);

		/// <summary>
		/// Deconstructs the DTO into the contained data.
		/// </summary>
		/// <param name="appName">The unique technical name of the client application performing the login.</param>
		/// <param name="appApiToken">The application authentication token of the client application performing the login.</param>
		/// <param name="userSecret">The secret string to authenticate the user.</param>
		public void Deconstruct(out string appName, out string appApiToken, out string userSecret) {
			appName = AppName;
			appApiToken = AppApiToken;
			userSecret = UserSecret;
		}
	}

	/// <summary>
	/// Specifies the data transferred from the client to the server when the client attempts to login a user by specifying a user id.
	/// </summary>
	public class IdBasedLoginRequestDTO : LoginRequestDTO {
		/// <summary>
		/// The user id to identify the user attempting to log-in.
		/// </summary>
		public Guid UserId { get; private set; }
		/// <summary>
		/// Creates a new DTO with the given data.
		/// </summary>
		/// <param name="appName">The unique technical name of the client application performing the login.</param>
		/// <param name="appApiToken">The application authentication token of the client application performing the login.</param>
		/// <param name="userSecret">The secret string to authenticate the user.</param>
		/// <param name="userId">The user id to identify the user attempting to log-in.</param>
		public IdBasedLoginRequestDTO(
			[PlainName][StringLength(128, MinimumLength = 1)] string appName,
			[StringLength(64, MinimumLength = 8)] string appApiToken,
			Guid userId,
			[StringLength(128, MinimumLength = 8)] string userSecret) :
			base(appName, appApiToken, userSecret) {
			UserId = userId;
		}
		/// <summary>
		/// Deconstructs the DTO into the contained data.
		/// </summary>
		/// <param name="appName">The unique technical name of the client application performing the login.</param>
		/// <param name="appApiToken">The application authentication token of the client application performing the login.</param>
		/// <param name="userSecret">The secret string to authenticate the user.</param>
		/// <param name="userId">The user id to identify the user attempting to log-in.</param>
		public void Deconstruct(out string appName, out string appApiToken, out string userSecret, out Guid userId) {
			appName = AppName;
			appApiToken = AppApiToken;
			userSecret = UserSecret;
			userId = UserId;
		}
	}
	/// <summary>
	/// Specifies the data transferred from the client to the server when the client attempts to login a user by specifying a username.
	/// </summary>
	public class UsernameBasedLoginRequestDTO : LoginRequestDTO {
		/// <summary>
		/// The username to identify the user attempting to log-in.
		/// </summary>
		[PlainName(allowBrackets: true)]
		[StringLength(64, MinimumLength = 1)]
		public string Username { get; private set; }
		/// <summary>
		/// Creates a new DTO with the given data.
		/// </summary>
		/// <param name="appName">The unique technical name of the client application performing the login.</param>
		/// <param name="appApiToken">The application authentication token of the client application performing the login.</param>
		/// <param name="userSecret">The secret string to authenticate the user.</param>
		/// <param name="username">The username to identify the user attempting to log-in.</param>
		public UsernameBasedLoginRequestDTO(
			[PlainName][StringLength(128, MinimumLength = 1)] string appName,
			[StringLength(64, MinimumLength = 8)] string appApiToken,
			[PlainName(allowBrackets: true)][StringLength(64, MinimumLength = 1)] string username,
			[StringLength(128, MinimumLength = 8)] string userSecret) :
			base(appName, appApiToken, userSecret) {
			Username = username;
		}
		/// <summary>
		/// Deconstructs the DTO into the contained data.
		/// </summary>
		/// <param name="appName">The unique technical name of the client application performing the login.</param>
		/// <param name="appApiToken">The application authentication token of the client application performing the login.</param>
		/// <param name="userSecret">The secret string to authenticate the user.</param>
		/// <param name="username">The username to identify the user attempting to log-in.</param>
		public void Deconstruct(out string appName, out string appApiToken, out string userSecret, out string username) {
			appName = AppName;
			appApiToken = AppApiToken;
			userSecret = UserSecret;
			username = Username;
		}
	}

	/// <summary>
	/// Provides the <see cref="GetUserIdentifier(LoginRequestDTO)"/> extension method.
	/// </summary>
	public static class LoginRequestDTOExtensions {
		/// <summary>
		/// Obtains a string representation of the user identifier used in a <see cref="LoginRequestDTO"/>.
		/// If <paramref name="loginRequest"/> is an <see cref="IdBasedLoginRequestDTO"/>, a string representation of the user id is returned.
		/// If <paramref name="loginRequest"/> is a <see cref="UsernameBasedLoginRequestDTO"/>, the username is returned.
		/// </summary>
		/// <param name="loginRequest">The login request DTO from which to obtain the identifier.</param>
		/// <returns>The identifier used in the login request.</returns>
		/// <exception cref="NotSupportedException">If an unsupported derived type of <see cref="LoginRequestDTO"/> is given.</exception>
		public static string GetUserIdentifier(this LoginRequestDTO loginRequest) {
			if (loginRequest is IdBasedLoginRequestDTO idBased) {
				return idBased.UserId.ToString();
			}
			else if (loginRequest is UsernameBasedLoginRequestDTO usernameBased) {
				return usernameBased.Username;
			}
			else {
				throw new NotSupportedException("Unsupported LoginRequestDTO type.");
			}
		}
	}

	/// <summary>
	/// Implements polymorphic JSON serialization and deserialization for <see cref="LoginRequestDTO"/>s.
	/// It doesn't use a type discriminator, but instead inspects the JSON on deserialization to determine whether a user id or username is present and 
	/// selects the appropriate derived type of <see cref="LoginRequestDTO"/>, preferring <see cref="IdBasedLoginRequestDTO"/> if both are present.
	/// </summary>
	public class LoginRequestDTOJsonConverter : JsonConverter<LoginRequestDTO> {
		private Func<string, bool> buildNameChecker(string searchedName, JsonSerializerOptions options) {
			var convertedName = options.PropertyNamingPolicy?.ConvertName(searchedName) ?? searchedName;
			return currentName => string.Equals(currentName, convertedName, options.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
		}
		/// <inheritdoc/>
		public override LoginRequestDTO? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			var isAppName = buildNameChecker(nameof(LoginRequestDTO.AppName), options);
			var isAppApiToken = buildNameChecker(nameof(LoginRequestDTO.AppApiToken), options);
			var isUserSecret = buildNameChecker(nameof(LoginRequestDTO.UserSecret), options);
			var isUserId = buildNameChecker(nameof(IdBasedLoginRequestDTO.UserId), options);
			var isUsername = buildNameChecker(nameof(UsernameBasedLoginRequestDTO.Username), options);

			string? appName = null;
			string? appApiToken = null;
			string? userSecret = null;
			Guid? userId = null;
			string? userName = null;

			using (JsonDocument doc = JsonDocument.ParseValue(ref reader)) {
				foreach (var property in doc.RootElement.EnumerateObject()) {
					if (isAppName(property.Name)) {
						if (property.Value.ValueKind == JsonValueKind.String) {
							appName = property.Value.GetString();
						}
						else {
							throw new JsonException("The AppName property has an incorrect type.");
						}
					}
					else if (isAppApiToken(property.Name)) {
						if (property.Value.ValueKind == JsonValueKind.String) {
							appApiToken = property.Value.GetString();
						}
						else {
							throw new JsonException("The AppApiToken property has an incorrect type.");
						}
					}
					else if (isUserSecret(property.Name)) {
						if (property.Value.ValueKind == JsonValueKind.String) {
							userSecret = property.Value.GetString();
						}
						else {
							throw new JsonException("The UserSecret property has an incorrect type.");
						}
					}
					else if (isUserId(property.Name)) {
						if (property.Value.ValueKind == JsonValueKind.String && property.Value.TryGetGuid(out var id)) {
							userId = id;
						}
						else {
							throw new JsonException("The UserId property has an incorrect type.");
						}
					}
					else if (isUsername(property.Name)) {
						if (property.Value.ValueKind == JsonValueKind.String) {
							userName = property.Value.GetString();
						}
						else {
							throw new JsonException("The Username property has an incorrect type.");
						}
					}
				}
			}
			if (appName == null) {
				throw new JsonException("The AppName property is missing.");
			}
			if (appApiToken == null) {
				throw new JsonException("The AppApiToken property is missing.");
			}
			if (userSecret == null) {
				throw new JsonException("The UserSecret property is missing.");
			}
			if (userId != null) {
				return new IdBasedLoginRequestDTO(appName, appApiToken, userId.Value, userSecret);
			}
			else if (userName != null) {
				return new UsernameBasedLoginRequestDTO(appName, appApiToken, userName, userSecret);
			}
			else {
				throw new NotSupportedException("Unsupported LoginRequestDTO type.");
			}
		}

		/// <inheritdoc/>
		public override void Write(Utf8JsonWriter writer, LoginRequestDTO value, JsonSerializerOptions options) {
			if (value is IdBasedLoginRequestDTO idBased) {
				JsonSerializer.Serialize(writer, idBased);
			}
			else if (value is UsernameBasedLoginRequestDTO usernameBased) {
				JsonSerializer.Serialize(writer, usernameBased);
			}
			else {
				throw new NotSupportedException("Unsupported LoginRequestDTO type.");
			}
		}
	}
}
