using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Interfaces {
	public interface IKeyAuthManager {
		ExporterKeyAuthResponseDTO CompleteChallengeAsync(ExporterKeyAuthSignatureDTO signatureDto, CancellationToken ct);
		ExporterKeyAuthChallengeDTO OpenChallengeAsync(ExporterKeyAuthRequestDTO requestDto, CancellationToken ct);
	}
}
