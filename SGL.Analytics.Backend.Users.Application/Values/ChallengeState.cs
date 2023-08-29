using SGL.Analytics.DTO;
using SGL.Utilities.Crypto.Keys;
using SGL.Utilities.Crypto.Signatures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Values {
	/// <summary>
	/// Encapsulates the state associated with a pending challenge, i.e. one for which the challenge was issued to the client, but the client hasn't solved it yet.
	/// </summary>
	public class ChallengeState {
		/// <summary>
		/// The unique id of the pending challenge.
		/// </summary>
		public Guid ChallengeId => ChallengeData.ChallengeId;
		/// <summary>
		/// The data submitted by the client in the initial request for opening the challenge.
		/// </summary>
		public ExporterKeyAuthRequestDTO RequestData { get; set; }
		/// <summary>
		/// The data sent back to the client when issuing the challenge.
		/// </summary>
		public ExporterKeyAuthChallengeDTO ChallengeData { get; set; }
		/// <summary>
		/// The timestamp (in UTC) when the challenge expires due to timeout.
		/// </summary>
		public DateTime Timeout { get; private set; }

		/// <summary>
		/// Construct a challenge state object with the given data.
		/// </summary>
		public ChallengeState(ExporterKeyAuthRequestDTO requestData, ExporterKeyAuthChallengeDTO challengeData, DateTime timeout) {
			RequestData = requestData;
			ChallengeData = challengeData;
			Timeout = timeout;
		}
	}
}
