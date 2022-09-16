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
		public ExporterKeyAuthChallengeDTO OpenChallengeAsync(ExporterKeyAuthRequestDTO requestDto, CancellationToken ct = default) {
			throw new NotImplementedException();
		}
		public ExporterKeyAuthResponseDTO CompleteChallengeAsync(ExporterKeyAuthSignatureDTO signatureDto, CancellationToken ct = default) {
			throw new NotImplementedException();
		}
	}
}
