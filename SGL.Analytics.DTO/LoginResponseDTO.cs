﻿using SGL.Utilities;
using System;
using System.Text.Json.Serialization;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Specifies the data transferred from the server to the client after a successful login.
	/// </summary>
	public class LoginResponseDTO {
		/// <summary>
		/// The authentication token for the session, returned by the server.
		/// </summary>
		public AuthorizationToken Token { get; private set; }

		/// <summary>
		/// The id of the user that has logged in. This is also present as a claim in <see cref="Token"/>.
		/// </summary>
		public Guid? UserId { get; private set; }

		/// <summary>
		/// The date and time when <see cref="Token"/> expires.
		/// </summary>
		public DateTime? TokenExpiry { get; private set; }

		/// <summary>
		/// Constructs a <see cref="LoginResponseDTO"/> with the given data.
		/// </summary>
		[JsonConstructor]
		public LoginResponseDTO(AuthorizationToken token, Guid? userId, DateTime? tokenExpiry) {
			Token = token;
			UserId = userId;
			TokenExpiry = tokenExpiry;
		}
	}

	/// <summary>
	/// An extended version of <see cref="LoginResponseDTO"/> for delegated authentication, 
	/// that additionally provides the id of the user in the upstream backend.
	/// </summary>
	public class DelegatedLoginResponseDTO : LoginResponseDTO {
		/// <summary>
		/// The user id of the authenticated user in the upstream backend.
		/// </summary>
		public Guid UpstreamUserId { get; private set; }

		/// <summary>
		/// Constructs a <see cref="DelegatedLoginResponseDTO"/> with the given data.
		/// </summary>
		[JsonConstructor]
		public DelegatedLoginResponseDTO(AuthorizationToken token, Guid? userId, DateTime? tokenExpiry, Guid upstreamUserId) : base(token, userId, tokenExpiry) {
			UpstreamUserId = upstreamUserId;
		}
	}
}
