using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SGL.Analytics.Backend.Domain.Entity;
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
		public string? SignerCertificateFile { get; set; } = null;

	}

	public class KeyAuthManager : IKeyAuthManager {
		private readonly IServiceProvider serviceProvider;
		private readonly ILogger<KeyAuthManager> logger;
		private readonly IKeyAuthChallengeStateHolder stateHolder;
		private readonly KeyAuthOptions options;

		public KeyAuthManager(IServiceProvider serviceProvider, ILogger<KeyAuthManager> logger, IKeyAuthChallengeStateHolder stateHolder) {
			this.serviceProvider = serviceProvider;
			this.logger = logger;
			this.stateHolder = stateHolder;
		}

		public async Task<ExporterKeyAuthChallengeDTO> OpenChallengeAsync(ExporterKeyAuthRequestDTO requestDto, CancellationToken ct = default) {
			var randomGenerator = new RandomGenerator();
			var challengeDto = new ExporterKeyAuthChallengeDTO(Guid.NewGuid(), randomGenerator.GetBytes(16 * 1024/*TODO: Parameterize*/), SignatureDigest.Sha256/*TODO: Parameterize*/);
			await stateHolder.OpenChallengeAsync(new Values.ChallengeState(requestDto, challengeDto, DateTime.UtcNow.AddMinutes(10)/*TODO: Parameterize*/), ct);
			return challengeDto;
		}
		public async Task<ExporterKeyAuthResponseDTO> CompleteChallengeAsync(ExporterKeyAuthSignatureDTO signatureDto, CancellationToken ct = default) {
			var appRepo = serviceProvider.GetRequiredService<IApplicationRepository<ApplicationWithUserProperties, ApplicationQueryOptions>>();
			var jwtOptions = serviceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;
			var state = await stateHolder.GetChallengeAsync(signatureDto.ChallengeId, ct);
			if (state == null) {
				throw new Exception();//TODO: Make type-safe
			}
			var app = await appRepo.GetApplicationByNameAsync(state.RequestData.AppName, new ApplicationQueryOptions { FetchExporterCertificates = true }, ct); // TODO: Let's just fetch the one cert we need
			if (app == null) {
				throw new Exception();//TODO: Make type-safe
			}
			var keyCertEntry = app.AuthorizedExporters.SingleOrDefault(ekac => ekac.PublicKeyId == state.RequestData.KeyId);
			if (keyCertEntry == null) {
				throw new Exception();//TODO: Make type-safe
			}
			var keyCert = keyCertEntry.Certificate;
			if (!keyCert.AllowedKeyUsages.HasValue) {
				throw new Exception();//TODO: Make type-safe
			}
			if (!keyCert.AllowedKeyUsages.Value.HasFlag(Utilities.Crypto.Certificates.KeyUsages.DigitalSignature)) {
				throw new Exception();//TODO: Make type-safe
			}
			var certKeyId = keyCert.PublicKey.CalculateId();
			if (state.RequestData.KeyId != certKeyId) {
				throw new Exception();//TODO: Make type-safe
			}
			if (options.SignerCertificateFile != null) {
				var signerValidator = GetSignerValidator();
				if (!signerValidator.CheckCertificate(keyCert)) {
					throw new Exception();//TODO: Make type-safe
				}
			}
			else {
				logger.LogWarning("No signer certificate configured, can't check signer certificate of exporter authentication certificate.");
			}

			var verifier = new SignatureVerifier(keyCert.PublicKey, state.ChallengeData.DigestAlgorithmToUse);
			var challengeContent = ExporterKeyAuthSignatureDTO.ConstructContentToSign(state.RequestData, state.ChallengeData);
			verifier.ProcessBytes(challengeContent);
			if (!verifier.IsValidSignature(challengeContent)) {
				throw new Exception();//TODO: Make type-safe
			}
			// As we are issuing JWT bearer tokens, use the same key config as JwtLoginService
			if (jwtOptions.SymmetricKey == null) {
				throw new Exception("No signing key given for Jwt config.");//TODO: Make type-safe
			}
			var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SymmetricKey));
			var signingCredentials = new SigningCredentials(signingKey, jwtOptions.LoginService.SigningAlgorithm);
			var expires = DateTime.UtcNow.AddDays(1);//TODO: Parameterize
			var claims = new[] {
				new Claim("keyid", keyCert.PublicKey.CalculateId().ToString() ?? throw new ArgumentNullException()),
				new Claim("appname", app.Name),
				new Claim("exporter-dn",keyCert.SubjectDN.ToString()?? throw new ArgumentNullException())
			};
			var token = new JwtSecurityToken(jwtOptions.Issuer, jwtOptions.Audience, claims, null, expires, signingCredentials);
			var jwtHandler = new JwtSecurityTokenHandler();
			var jwtString = jwtHandler.WriteToken(token);
			var response = new ExporterKeyAuthResponseDTO(new AuthorizationToken(AuthorizationTokenScheme.Bearer, jwtString));
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
