using SGL.Utilities.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Specifies the data that is sent from a client to the analytics backend to request a delegated authentication
	/// using an authorization token for a trusted upstream system.
	/// </summary>
	public class UpstreamSessionRequestDTO {
		/// <summary>
		/// The unique technical name of the client application requesting the upstream session.
		/// </summary>
		[PlainName]
		[StringLength(128, MinimumLength = 1)]
		public string AppName { get; private set; }
		/// <summary>
		/// The application authentication token of the client application requesting the upstream session.
		/// </summary>
		[StringLength(64, MinimumLength = 8)]
		public string AppApiToken { get; private set; }
		/// <summary>
		/// The authorization header to pass to the upstream backend for session validation.
		/// </summary>
		public string UpstreamAuthorizationHeader { get; private set; }

		/// <summary>
		/// Constructs a <see cref="UpstreamSessionRequestDTO"/> with the given data.
		/// </summary>
		public UpstreamSessionRequestDTO([PlainName][StringLength(128, MinimumLength = 1)] string appName,
			[StringLength(64, MinimumLength = 8)] string appApiToken, string upstreamAuthorizationHeader) {
			AppName = appName;
			AppApiToken = appApiToken;
			UpstreamAuthorizationHeader = upstreamAuthorizationHeader;
		}
	}
}
