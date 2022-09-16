using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Backend.Applications;
using SGL.Utilities.Backend.Security;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Signatures;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Services {

	public class KeyAuthOptions {

	}

	public class KeyAuthManager : IKeyAuthManager {
		private readonly ILogger<KeyAuthManager> logger;
		private readonly IApplicationRepository<ApplicationWithUserProperties, ApplicationQueryOptions> appRepo;
		private readonly IKeyAuthChallengeStateHolder stateHolder;
		private readonly JwtOptions jwtOptions;
		private readonly RandomGenerator randomGenerator = new RandomGenerator();

		public KeyAuthManager(ILogger<KeyAuthManager> logger, IApplicationRepository<ApplicationWithUserProperties, ApplicationQueryOptions> appRepo,
				IKeyAuthChallengeStateHolder stateHolder, IOptions<JwtOptions> jwtOptions) {
			this.logger = logger;
			this.appRepo = appRepo;
			this.stateHolder = stateHolder;
			this.jwtOptions = jwtOptions.Value;
		}

		public async Task<ExporterKeyAuthChallengeDTO> OpenChallengeAsync(ExporterKeyAuthRequestDTO requestDto, CancellationToken ct = default) {
			var challengeDto = new ExporterKeyAuthChallengeDTO(Guid.NewGuid(), randomGenerator.GetBytes(16 * 1024/*TODO: Parameterize*/), SignatureDigest.Sha256/*TODO: Parameterize*/);
			await stateHolder.OpenChallengeAsync(new Values.ChallengeState(requestDto, challengeDto, DateTime.UtcNow.AddMinutes(10)/*TODO: Parameterize*/), ct);
			return challengeDto;
		}
		public async Task<ExporterKeyAuthResponseDTO> CompleteChallengeAsync(ExporterKeyAuthSignatureDTO signatureDto, CancellationToken ct = default) {
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
			//TODO: Verify Certificate

			var verifier = new SignatureVerifier(keyCert.PublicKey, state.ChallengeData.DigestAlgorithmToUse);
			var challengeContent = ExporterKeyAuthSignatureDTO.ConstructContentToSign(state.RequestData, state.ChallengeData);
			verifier.ProcessBytes(challengeContent);
			if (!verifier.IsValidSignature(challengeContent)) {
				throw new Exception();//TODO: Make type-safe
			}
			// As we are issuing JWT bearer tokens, use the same key config as JwtLoginService
			if (this.jwtOptions.SymmetricKey == null) {
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
	}
}
