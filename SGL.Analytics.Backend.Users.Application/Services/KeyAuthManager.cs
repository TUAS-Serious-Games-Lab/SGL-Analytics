using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Services {
	public class KeyAuthManager : IKeyAuthManager {
		public ExporterKeyAuthResponseDTO CompleteChallengeAsync(ExporterKeyAuthSignatureDTO signatureDto, CancellationToken ct) {
			throw new NotImplementedException();
		}

		public ExporterKeyAuthChallengeDTO OpenChallengeAsync(ExporterKeyAuthRequestDTO requestDto, CancellationToken ct) {
			throw new NotImplementedException();
		}
	}
}
