using SGL.Analytics.DTO;
using SGL.Utilities.Crypto.Keys;
using SGL.Utilities.Crypto.Signatures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Values {
	public class ChallengeState {
		public Guid ChallengeId => ChallengeData.ChallengeId;
		public ExporterKeyAuthRequestDTO RequestData { get; set; }
		public ExporterKeyAuthChallengeDTO ChallengeData { get; set; }
		public DateTime Timeout { get; private set; }

		public ChallengeState(ExporterKeyAuthRequestDTO requestData, ExporterKeyAuthChallengeDTO challengeData, DateTime timeout) {
			RequestData = requestData;
			ChallengeData = challengeData;
			Timeout = timeout;
		}
	}
}
