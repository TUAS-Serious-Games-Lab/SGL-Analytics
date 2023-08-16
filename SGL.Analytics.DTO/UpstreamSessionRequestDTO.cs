using SGL.Utilities.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGL.Analytics.DTO {
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

		public string UpstreamAuthorizationHeader { get; private set; }

		public UpstreamSessionRequestDTO([PlainName][StringLength(128, MinimumLength = 1)] string appName,
			[StringLength(64, MinimumLength = 8)] string appApiToken, string upstreamAuthorizationHeader) {
			AppName = appName;
			AppApiToken = appApiToken;
			UpstreamAuthorizationHeader = upstreamAuthorizationHeader;
		}
	}
}
