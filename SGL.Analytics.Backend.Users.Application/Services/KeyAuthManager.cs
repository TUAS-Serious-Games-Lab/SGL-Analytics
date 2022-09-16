using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Backend.Applications;
using SGL.Utilities.Backend.Security;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Signatures;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Services {

	public class KeyAuthOptions {
		public const string ExporterKeyAuth = "ExporterKeyAuth";
		public string? SignerCertificateFile { get; set; } = null;
		public int ChallengeSize { get; set; } = 16 * 1024;
		public SignatureDigest ChallengeDigest { get; set; } = SignatureDigest.Sha256;
		public TimeSpan ChallengeTimeout { get; set; } = TimeSpan.FromMinutes(5);
		public TimeSpan TokenValidity { get; set; } = TimeSpan.FromDays(1);
	}

	public class KeyAuthManager : IKeyAuthManager {
		private readonly IServiceProvider serviceProvider;
		private readonly ILogger<KeyAuthManager> logger;
		private readonly IKeyAuthChallengeStateHolder stateHolder;
		private readonly KeyAuthOptions options;

		public KeyAuthManager(IServiceProvider serviceProvider, ILogger<KeyAuthManager> logger, IKeyAuthChallengeStateHolder stateHolder, IOptions<KeyAuthOptions> options) {
			this.serviceProvider = serviceProvider;
			this.logger = logger;
			this.stateHolder = stateHolder;
			this.options = options.Value;
		}

		public async Task<ExporterKeyAuthChallengeDTO> OpenChallengeAsync(ExporterKeyAuthRequestDTO requestDto, CancellationToken ct = default) {
			var randomGenerator = new RandomGenerator();
			var challengeDto = new ExporterKeyAuthChallengeDTO(Guid.NewGuid(), randomGenerator.GetBytes(options.ChallengeSize), options.ChallengeDigest);
			await stateHolder.OpenChallengeAsync(new Values.ChallengeState(requestDto, challengeDto, DateTime.UtcNow + options.ChallengeTimeout), ct);
			logger.LogInformation("Opened challenge {id} for app {appName} and key id {keyId}.", challengeDto.ChallengeId, requestDto.AppName, requestDto.KeyId);
			return challengeDto;
		}
		public async Task<ExporterKeyAuthResponseDTO> CompleteChallengeAsync(ExporterKeyAuthSignatureDTO signatureDto, CancellationToken ct = default) {
			var appRepo = serviceProvider.GetRequiredService<IApplicationRepository<ApplicationWithUserProperties, ApplicationQueryOptions>>();
			var state = await stateHolder.GetChallengeAsync(signatureDto.ChallengeId, ct);
			if (state == null) {
				logger.LogError("Couldn't find an open and not-timed-out challenge with id {id}.", signatureDto.ChallengeId);
				throw new InvalidChallengeException(signatureDto.ChallengeId, "No open challenge with the given id.");
			}
			var app = await appRepo.GetApplicationByNameAsync(state.RequestData.AppName, new ApplicationQueryOptions { FetchExporterCertificate = state.RequestData.KeyId }, ct);
			if (app == null) {
				logger.LogError("Challenge {id} was opened with a non-existent app name {appname}.", signatureDto.ChallengeId, state.RequestData.AppName);
				throw new ApplicationDoesNotExistException(state.RequestData.AppName);
			}
			var keyCertEntry = app.AuthorizedExporters.SingleOrDefault(ekac => ekac.PublicKeyId == state.RequestData.KeyId);
			if (keyCertEntry == null) {
				logger.LogError("Challend {id} was opened with a key id {keyId} for which there is no exporter certificate registered in the app {appName}.", signatureDto.ChallengeId, state.RequestData.KeyId, state.RequestData.AppName);
				throw new NoCertificateForKeyIdException(state.RequestData.KeyId, "Could'n find an exporter certificate for the key id.");
			}
			var keyCert = keyCertEntry.Certificate;
			var certKeyId = keyCert.PublicKey.CalculateId();
			if (!keyCert.AllowedKeyUsages.HasValue) {
				logger.LogError("The exporter certificate {DN} with key id {keyId} doesn't have the required KeyUsage extension.", keyCert.SubjectDN, certKeyId);
				throw new CertificateException("The exporter certificate doesn't have the required KeyUsage extension.");
			}
			if (!keyCert.AllowedKeyUsages.Value.HasFlag(Utilities.Crypto.Certificates.KeyUsages.DigitalSignature)) {
				logger.LogError("The exporter certificate {DN} with key id {keyId} has a KeyUsage extension without the required DigitalSignature usage.", keyCert.SubjectDN, certKeyId);
				throw new CertificateException("The exporter certificate has a KeyUsage extension without the required DigitalSignature usage.");
			}
			if (state.RequestData.KeyId != certKeyId) {
				logger.LogError("The key id {keyId} of the public key of the exporter certificate {DN} didn't match the key id in its metadata {metaKeyId}.", certKeyId, keyCert.SubjectDN, keyCertEntry.PublicKeyId);
				throw new CertificateException("The key id of the public key of the exporter certificate didn't match the key id in its metadata.");
			}
			if (options.SignerCertificateFile != null) {
				var signerValidator = GetSignerValidator();
				if (!signerValidator.CheckCertificate(keyCert)) {
					logger.LogError("The exporter certificate {DN} with key id {keyId} failed validation.", keyCert.SubjectDN, certKeyId);
					throw new CertificateException("The exporter certificate failed validation.");
				}
			}
			else {
				logger.LogWarning("No signer certificate configured, can't check signer certificate of exporter authentication certificate.");
			}

			var verifier = new SignatureVerifier(keyCert.PublicKey, state.ChallengeData.DigestAlgorithmToUse);
			var challengeContent = ExporterKeyAuthSignatureDTO.ConstructContentToSign(state.RequestData, state.ChallengeData);
			verifier.ProcessBytes(challengeContent);
			if (!verifier.IsValidSignature(challengeContent)) {
				throw new ChallengeCompletionFailedException("The signature in the challenge response was invalid.");
			}
			// As we are issuing JWT bearer tokens, use the same key config as JwtLoginService
			var jwtOptions = serviceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;
			if (jwtOptions.SymmetricKey == null) {
				throw new InvalidOperationException("No signing key given for Jwt config.");
			}
			var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SymmetricKey));
			var signingCredentials = new SigningCredentials(signingKey, jwtOptions.LoginService.SigningAlgorithm);
			var expires = DateTime.UtcNow + options.TokenValidity;
			var claims = new[] {
				new Claim("keyid", keyCert.PublicKey.CalculateId().ToString() ?? throw new ArgumentNullException()),
				new Claim("appname", app.Name),
				new Claim("exporter-dn",keyCert.SubjectDN.ToString()?? throw new ArgumentNullException())
			};
			var token = new JwtSecurityToken(jwtOptions.Issuer, jwtOptions.Audience, claims, null, expires, signingCredentials);
			var jwtHandler = new JwtSecurityTokenHandler();
			var jwtString = jwtHandler.WriteToken(token);
			var response = new ExporterKeyAuthResponseDTO(new AuthorizationToken(AuthorizationTokenScheme.Bearer, jwtString));
			// As the challenge was sucessfully solved, close it.
			await stateHolder.CloseChallengeAsync(state);
			logger.LogInformation("Issuing JWT session token for exporter certificate {DN} (key id = {keyId}) and app {appName}.", keyCert.SubjectDN, certKeyId, state.RequestData.AppName);
			return response;
		}

		private CACertTrustValidator GetSignerValidator() {
			string file = options.SignerCertificateFile!;
			using var reader = File.OpenText(file);
			ILogger<CACertTrustValidator> validatorLogger = serviceProvider.GetService<ILogger<CACertTrustValidator>>() ?? NullLogger<CACertTrustValidator>.Instance;
			ILogger<CertificateStore> caCertStoreLogger = serviceProvider.GetService<ILogger<CertificateStore>>() ?? NullLogger<CertificateStore>.Instance;
			return new CACertTrustValidator(reader, file, ignoreValidityPeriod: false, validatorLogger, caCertStoreLogger);
		}
	}
}
