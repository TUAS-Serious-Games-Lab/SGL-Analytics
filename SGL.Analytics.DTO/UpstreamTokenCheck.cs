using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Specifies the request data sent to an upstream backend when validating an upstream authorization token for delegated authentication.
	/// </summary>
	public class UpstreamTokenCheckRequest {
		/// <summary>
		/// The app name of the app requesting the token check.
		/// The token is checked against this by the upstream backend to prevent usage of a token issued for a different app.
		/// </summary>
		[StringLength(128, MinimumLength = 1)]
		public string RequestingAppName { get; set; }

		/// <summary>
		/// Constructs a <see cref="UpstreamTokenCheckRequest"/> with the given data.
		/// </summary>
		public UpstreamTokenCheckRequest(string requestingAppName) {
			RequestingAppName = requestingAppName;
		}
	}
	/// <summary>
	/// Specifies the response expected from an upstream backend upon successful validation of an upstream authorization token for delegated authentication.
	/// </summary>
	public class UpstreamTokenCheckResponse {
		/// <summary>
		/// The id of the user for which the token was issued.
		/// </summary>
		public Guid UserId { get; set; }

		/// <summary>
		/// The date and time (in UTC) when the token expires.
		/// </summary>
		public DateTime TokenExpiry { get; set; }

		/// <summary>
		/// Constructs a <see cref="UpstreamTokenCheckResponse"/> with the given data.
		/// </summary>
		public UpstreamTokenCheckResponse(Guid userId, DateTime tokenExpiry) {
			UserId = userId;
			TokenExpiry = tokenExpiry;
		}
	}
}
