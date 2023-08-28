using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.DTO;
using SGL.Utilities.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Interfaces {
	/// <summary>
	/// Specifies the interface for service objects that implement key-pair-based challenge authentication.
	/// </summary>
	public interface IKeyAuthManager {
		/// <summary>
		/// Asynchronously completes the challenge with the id and signature provided in <paramref name="signatureDto"/>.
		/// </summary>
		/// <param name="signatureDto">The completion data for the challenge submitted by the client.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providng an issued session authorization token upon success.</returns>
		/// <exception cref="InvalidChallengeException">When the supplied challenge id was not found.</exception>
		/// <exception cref="ApplicationDoesNotExistException">When the application indicated for the challenge did not exist.</exception>
		/// <exception cref="NoCertificateForKeyIdException">When no authentication certificate for the key id, supplied when openening the challenge, was found.</exception>
		/// <exception cref="CertificateException">When the authentication certificate with the key id, supplied when openening the challenge, was present but invalid.</exception>
		/// <exception cref="ChallengeCompletionFailedException">When completing the challenge failed because the signature was invalid.</exception>
		Task<ExporterKeyAuthResponseDTO> CompleteChallengeAsync(ExporterKeyAuthSignatureDTO signatureDto, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously opens a challenge with the given initialization parameters.
		/// This generates the challenge state represented by <see cref="ExporterKeyAuthResponseDTO"/>,
		/// stores it in <see cref="IKeyAuthChallengeStateHolder"/> and returns it for delivery to the client.
		/// </summary>
		/// <param name="requestDto">The initialization parameters supplied by the client.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providng the challenge state that shall be returned to the client.</returns>
		Task<ExporterKeyAuthChallengeDTO> OpenChallengeAsync(ExporterKeyAuthRequestDTO requestDto, CancellationToken ct = default);
	}
}
