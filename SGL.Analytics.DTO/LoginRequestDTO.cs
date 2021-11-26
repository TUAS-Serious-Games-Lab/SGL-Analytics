﻿using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Specifies the data transferred from the client to the server when the client attempts to login a user.
	/// </summary>
	[JsonConverter(typeof(LoginRequestDTOJsonConverter))]
	public abstract record LoginRequestDTO(string AppName, string AppApiToken, string UserSecret);
	public record IdBasedLoginRequestDTO(string AppName, string AppApiToken, Guid UserId, string UserSecret) : LoginRequestDTO(AppName, AppApiToken, UserSecret);
	public record UsernameBasedLoginRequestDTO(string AppName, string AppApiToken, string Username, string UserSecret) : LoginRequestDTO(AppName, AppApiToken, UserSecret);

	public static class LoginRequestDTOExtensions {
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

	public class LoginRequestDTOJsonConverter : JsonConverter<LoginRequestDTO> {
		private Func<string, bool> buildNameChecker(string searchedName, JsonSerializerOptions options) {
			var convertedName = options.PropertyNamingPolicy?.ConvertName(searchedName) ?? searchedName;
			return currentName => string.Equals(currentName, convertedName, options.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
		}
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
