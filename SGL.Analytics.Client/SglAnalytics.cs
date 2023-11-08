using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.EndToEnd;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {

	/// <summary>
	/// Can be used to annotate types used as event representations for <see cref="SglAnalytics.RecordEvent(string, ICloneable)"/> or
	/// <see cref="SglAnalytics.RecordEventUnshared(string, object)"/> to use an event type name that differs from the types name.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class EventTypeAttribute : Attribute {
		/// <summary>
		/// The name to use in the analytics logs to represent the event type.
		/// </summary>
		public string EventTypeName { get; private set; }
		/// <summary>
		/// Instantiates the attribute.
		/// </summary>
		/// <param name="eventTypeName">The name to use in the analytics logs to represent the event type.</param>
		public EventTypeAttribute(string eventTypeName) {
			EventTypeName = eventTypeName;
		}
	}

	/// <summary>
	/// Acts as the central facade class for the functionality of SGL Analytics and coordinates its operation.
	/// It provides a simple to use mechanism to record analytics log files (containing different streams of events and object state snapshots).
	/// The writing of these files to disk is done asynchronously in the background to not slow down the application and
	/// the completed files are uploaded to a collector backend that catalogs them by application and user.
	/// The upload process also happens automatically in the background and retries failed uploads on startup or when <see cref="StartRetryUploads"/> is called.
	///
	/// The public methods allow registering or authenticating the user, beginning a new analytics log file, recording events and snapshots into the current analytics log file,
	/// and finishing the analytics log operations by finishing the current file, waiting for it to be written and ensuring all pending uploads are complete.
	/// </summary>
	public partial class SglAnalytics : IAsyncDisposable {
		/// <summary>
		/// Instantiates a client facade object using the given app credentials and http client, configured by the given <paramref name="configuration"/> function.
		/// </summary>
		/// <param name="appName">The technical name of the application for which analytics logs are recorded. This is used for identifying the application in the backend and the application must be registered there for log collection and user registration to work properly.</param>
		/// <param name="appAPIToken">The API token assigned to the application in the backend. This is used as an additional security layer in the communication with the backend.</param>
		/// <param name="httpClient">
		/// The <see cref="HttpClient"/> that the client object shall use to communicate with the backend.
		/// The <see cref="HttpClient.BaseAddress"/> is must be set to the base adress of the backend server, e.g. <c>https://sgl-analytics.example.com/</c>.</param>
		/// <param name="configuration">A configurator function that performs fluent-style configuration for the client object.</param>
		public SglAnalytics(string appName, string appAPIToken, HttpClient httpClient, Action<ISglAnalyticsConfigurator> configuration) {
			this.appName = appName;
			this.appAPIToken = appAPIToken;
			this.httpClient = httpClient;
			configuration(configurator);
			mainSyncContext = configurator.SynchronizationContextGetter();
			dataDirectory = configurator.DataDirectorySource(new SglAnalyticsConfiguratorDataDirectorySourceArguments(appName));
			Directory.CreateDirectory(dataDirectory);
			var loggerFactoryBootstrapArgs = new SglAnalyticsConfiguratorFactoryArguments(appName, appAPIToken, httpClient, dataDirectory,
				NullLoggerFactory.Instance, randomGenerator, configurator.CustomArgumentFactories);
			LoggerFactory = configurator.LoggerFactory.Factory(loggerFactoryBootstrapArgs);
			var factoryArgs = new SglAnalyticsConfiguratorFactoryArguments(appName, appAPIToken, httpClient, dataDirectory, LoggerFactory,
				randomGenerator, configurator.CustomArgumentFactories);
			logger = LoggerFactory.CreateLogger<SglAnalytics>();
			cryptoConfig = configurator.CryptoConfig();
			recipientCertificateValidator = configurator.RecipientCertificateValidatorFactory.Factory(factoryArgs);
			rootDataStore = configurator.RootDataStoreFactory.Factory(factoryArgs);
			anonymousLogStorage = configurator.AnonymousLogStorageFactory.Factory(factoryArgs);
			currentLogStorage = anonymousLogStorage;
			userRegistrationClient = configurator.UserRegistrationClientFactory.Factory(factoryArgs);
			logCollectorClient = configurator.LogCollectorClientFactory.Factory(factoryArgs);
			userRegistrationClient.UserAuthenticated += async (s, e, ct) => {
				await logCollectorClient.SetAuthorizationLockedAsync(e.Authorization, ct);
				SessionAuthorization = e.Authorization;
				LoggedInUserId = e.AuthenticatedUserId;
			};
			logCollectorClient.AuthorizationExpired += async (s, e, ct) => {
				await refreshLoginDelegate(ct);
			};
		}

		/// <summary>
		/// Gets the technical name of the application that uses this SGL Analytics instance, as specified in the constructor.
		/// </summary>
		public string AppName { get => appName; }

		private ILoggerFactory LoggerFactory { get; }

		/// <summary>
		/// The id of the logged-in user, or null if no user is logged-in.
		/// </summary>
		public Guid? LoggedInUserId {
			get {
				lock (lockObject) {
					return loggedInUserId;
				}
			}
			private set {
				lock (lockObject) {
					loggedInUserId = value;
				}
			}
		}

		/// <summary>
		/// Indicates the current mode, the client is operating in.
		/// </summary>
		public SglAnalyticsClientMode CurrentClientMode { get; set; } = SglAnalyticsClientMode.Uninitialized;

		/// <summary>
		/// Checks if the this client has stored credentials, i.e. either a device secret or a stored password.
		/// If this returns true, <see cref="TryLoginWithStoredCredentialsAsync(CancellationToken)"/> can be used to authenticate using these credentials.
		/// Otherwise, a user can be logged in with username and password using <see cref="TryLoginWithPasswordAsync(string, string, bool, CancellationToken)"/>,
		/// or another user can be registered using <see cref="RegisterUserWithPasswordAsync(BaseUserData, string, bool, CancellationToken)"/> or
		/// <see cref="RegisterUserWithDeviceSecretAsync(BaseUserData, CancellationToken)"/>.
		/// </summary>
		/// <returns>true if the client has stored cedentials, false otherwise.</returns>
		public bool HasStoredCredentials() {
			lock (lockObject) {
				return rootDataStore.UserID != null || rootDataStore.Username != null;
			}
		}

		private async Task<Guid> RegisterImplAsync(BaseUserData userData, string? secret, bool storeCredentials, AuthorizationData? upstreamAuthToken = null, CancellationToken ct = default) {
			try {
				if (HasStoredCredentials()) {
					throw new InvalidOperationException("User is already registered.");
				}
				logger.LogInformation("Starting user registration process...");
				var (unencryptedUserPropDict, encryptedUserProps, userPropsEncryptionInfo) = await getUserProperties(userData);
				var userDTO = new UserRegistrationDTO(appName, userData.Username, secret,
					upstreamAuthToken != null ? upstreamAuthToken.Value.Token.ToString() : null,
					unencryptedUserPropDict, encryptedUserProps, userPropsEncryptionInfo);
				Validator.ValidateObject(userDTO, new ValidationContext(userDTO), true);
				// submit registration request
				var regResult = await userRegistrationClient.RegisterUserAsync(userDTO, ct);
				if (storeCredentials) {
					logger.LogInformation("Registration with backend succeeded. Got user id {userId}. Proceeding to store user id locally...", regResult.UserId);
					await storeCredentialsAsync(userData.Username, secret ?? "", regResult.UserId);
				}
				logger.LogInformation("Successfully registered user {userId}.", regResult.UserId);
				return regResult.UserId;
			}
			catch (UsernameAlreadyTakenException ex) {
				logger.LogError(ex, "Registration failed because the specified username is already in use.");
				throw;
			}
			catch (UserRegistrationResponseException ex) {
				logger.LogError(ex, "Registration failed due to error with the registration response.");
				throw;
			}
			catch (HttpApiResponseException ex) {
				logger.LogError(ex, "Registration failed due to error from server.");
				throw;
			}
			catch (HttpApiRequestFailedException ex) {
				logger.LogError(ex, "Registration failed due to communication problem with the backend server.");
				throw;
			}
			catch (ValidationException ex) {
				logger.LogError(ex, "Registration failed due to violating validation constraints.");
				throw;
			}
			catch (Exception ex) {
				logger.LogError(ex, "Registration failed due to unexpected error.");
				throw;
			}
		}

		/// <summary>
		/// Asynchronously registers the user with the given data and the given password for login in the backend database and initiates an initial session.
		/// </summary>
		/// <param name="userData">The user data for the registration, that is to be sent to the server.</param>
		/// <param name="password">The password to use for authenticating the user for future logins.</param>
		/// <param name="rememberCredentials">
		/// If true, the username and password are stored in the local root data store
		/// to allow login using <see cref="TryLoginWithStoredCredentialsAsync(CancellationToken)"/>.
		/// Otherweise, only <see cref="TryLoginWithPasswordAsync(string, string, bool, CancellationToken)"/> can be used.
		/// </param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the registration operation. Wait for it's completion before relying on logs being uploaded.
		/// Logs recorded on a client that hasn't completed registration are stored only locally until the registration is complete and the user id required for the upload is obtained.</returns>
		/// <remarks>
		/// Other state-changing operations (<c>StartNewLog</c>, <c>RegisterAsync</c>, <c>FinishAsync</c>, or the <c>Record</c>... operations) on the current object must not be called, between start and completion of this operation.
		/// </remarks>
		/// <exception cref="UsernameAlreadyTakenException">
		/// The username provided in <see cref="BaseUserData.Username"/> of <paramref name="userData"/> was already taken for this application.
		/// If this happens, the user needs to pick a different name.
		/// </exception>
		/// <exception cref="UserRegistrationResponseException">If the server didn't respond with the expected object in the expected format.</exception>
		/// <exception cref="ArgumentNullException">If no username was supplied in <paramref name="userData"/>.</exception>
		/// <exception cref="ValidationException">The user registration data failed local validation.</exception>
		/// <exception cref="HttpApiRequestFailedException">Indicates a network problem.</exception>
		/// <exception cref="HttpApiResponseException">Indicates a server-side error, see <see cref="HttpApiResponseException.StatusCode"/> for error code.</exception>
		public async Task RegisterUserWithPasswordAsync(BaseUserData userData, string password, bool rememberCredentials = false, CancellationToken ct = default) {
			if (userData.Username == null) {
				throw new ArgumentNullException($"{nameof(userData)}.{nameof(BaseUserData.Username)}");
			}
			// TODO: maybe: check password complexity
			var userId = await RegisterImplAsync(userData, password, rememberCredentials, ct: ct);
			var loginDto = new IdBasedLoginRequestDTO(appName, appAPIToken, userId, password);
			Func<CancellationToken, Task<LoginResponseDTO>> reloginDelegate = async ct2 => await loginAsync(loginDto, ct2);
			// login with newly registered credentials to obtain session token
			await reloginDelegate(ct);
			createUserLogStore(userId, userData.Username);
			disableLogWriting = false;
			lock (lockObject) {
				// hold on to re-login delegate for token refreshing, capturing needed credentials
				disableLogUploading = false;
				refreshLoginDelegate = reloginDelegate;
			}
			CurrentClientMode = SglAnalyticsClientMode.UsernamePasswordOnline;
		}

		private void createUserLogStore(Guid? userId, string? username) {
			var logStore = configurator.UserLogStorageFactory.Factory(new SglAnalyticsConfiguratorAuthenticatedFactoryArguments(appName, appAPIToken, httpClient,
				dataDirectory, LoggerFactory, randomGenerator, configurator.CustomArgumentFactories, SessionAuthorization, userId, username));
			lock (lockObject) {
				userLogStorage = logStore;
				currentLogStorage = userLogStorage;
			}
		}

		/// <summary>
		/// Asynchronously registers the user with the given data in the backend database and initiates an initial session.
		/// For authentication, a device token is generated and stored locally in the root data store.
		/// Use <see cref="TryLoginWithStoredCredentialsAsync(CancellationToken)"/> for future authentication.
		/// </summary>
		/// <param name="userData">The user data for the registration, that is to be sent to the server.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the registration operation. Wait for it's completion before relying on logs being uploaded.
		/// Logs recorded on a client that hasn't completed registration are stored only locally until the registration is complete and the user id required for the upload is obtained.</returns>
		/// <remarks>
		/// Other state-changing operations (<c>StartNewLog</c>, <c>RegisterAsync</c>, <c>FinishAsync</c>, or the <c>Record</c>... operations) on the current object must not be called, between start and completion of this operation.
		/// </remarks>
		/// <exception cref="UserRegistrationResponseException">If the server didn't respond with the expected object in the expected format.</exception>
		/// <exception cref="ValidationException">The user registration data failed local validation.</exception>
		/// <exception cref="HttpApiRequestFailedException">Indicates a network problem.</exception>
		/// <exception cref="HttpApiResponseException">Indicates a server-side error, see <see cref="HttpApiResponseException.StatusCode"/> for error code.</exception>
		public async Task RegisterUserWithDeviceSecretAsync(BaseUserData userData, CancellationToken ct = default) {
			// Generate random secret
			var secret = SecretGenerator.Instance.GenerateSecret(configurator.LegthOfGeneratedUserSecrets);
			var userId = await RegisterImplAsync(userData, secret, storeCredentials: true, ct: ct);
			var loginDto = new IdBasedLoginRequestDTO(appName, appAPIToken, userId, secret);
			Func<CancellationToken, Task<LoginResponseDTO>> reloginDelegate = async ct2 => await loginAsync(loginDto, ct2);
			// login with newly registered credentials to obtain session token
			await reloginDelegate(ct);
			createUserLogStore(userId, userData.Username);
			disableLogWriting = false;
			lock (lockObject) {
				// hold on to re-login delegate for token refreshing, capturing needed credentials
				refreshLoginDelegate = reloginDelegate;
				disableLogUploading = false;
			}
			CurrentClientMode = SglAnalyticsClientMode.DeviceTokenOnline;
		}
		/// <summary>
		/// Asynchronously registers the user with the given data in the backend database and initiates an initial session.
		/// The user account is created without its own password or device token.
		/// Instead, an authorization token for a trusted upstream backend (configured in the analytics backend for the application) is used
		/// to delegate authentication to the trusted upstream backend.
		/// The token is otained from <paramref name="getUpstreamAuthToken"/> and passed to the upstream backend.
		/// After after validation by the upstream backend, the analytics backend creates an account associated with the upstream user id.
		/// Future authentication is done by supplying a then current upstream authorization token to
		/// <see cref="TryLoginWithUpstreamDelegationAsync(Func{CancellationToken, Task{AuthorizationData}}, CancellationToken)"/>.
		/// As the account has no own credentials, it can't login using
		/// <see cref="TryLoginWithPasswordAsync(string, string, bool, CancellationToken)"/> or
		/// <see cref="TryLoginWithStoredCredentialsAsync(CancellationToken)"/>.
		/// </summary>
		/// <param name="userData">The user data for the registration, that is to be sent to the server.</param>
		/// <param name="getUpstreamAuthToken">
		/// A delegate used for obtaining the current authorization token for the trusted upstream system.
		/// This is initially invoked once for registration and once for establishing the initial session.
		/// Afterwards, it is invoked when the session is refreshed because it has expired or is close to expiring.
		/// </param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the registration operation. Wait for it's completion before relying on logs being uploaded.
		/// Logs recorded on a client that hasn't completed registration are stored only locally until the registration is complete and the user id required for the upload is obtained.</returns>
		/// <remarks>
		/// Other state-changing operations (<c>StartNewLog</c>, <c>RegisterAsync</c>, <c>FinishAsync</c>, or the <c>Record</c>... operations) on the current object must not be called, between start and completion of this operation.
		/// </remarks>
		/// <exception cref="UsernameAlreadyTakenException">
		/// The username provided in <see cref="BaseUserData.Username"/> of <paramref name="userData"/> was already taken for this application.
		/// If this happens, the user needs to pick a different name.
		/// </exception>
		/// <exception cref="UserRegistrationResponseException">If the server didn't respond with the expected object in the expected format.</exception>
		/// <exception cref="ValidationException">The user registration data failed local validation.</exception>
		/// <exception cref="HttpApiRequestFailedException">Indicates a network problem.</exception>
		/// <exception cref="HttpApiResponseException">Indicates a server-side error, see <see cref="HttpApiResponseException.StatusCode"/> for error code.</exception>
		public async Task RegisterWithUpstreamDelegationAsync(BaseUserData userData, Func<CancellationToken, Task<AuthorizationData>> getUpstreamAuthToken, CancellationToken ct = default) {
			var userId = await RegisterImplAsync(userData, null, storeCredentials: false, await getUpstreamAuthToken(ct), ct: ct);
			Func<CancellationToken, Task<DelegatedLoginResponseDTO>> reloginDelegate = async ct2 => {
				try {
					logger.LogInformation("Logging in user with upstream delegation ...");
					var response = await userRegistrationClient.OpenSessionFromUpstream(await getUpstreamAuthToken(ct2), ct2);
					logger.LogInformation("Login was successful.");
					return response;
				}
				catch (Exception ex) {
					logger.LogError(ex, "Login with upstream delegation failed with exception.");
					throw;
				}
			};
			var loginResponse = await reloginDelegate(ct);
			createUserLogStore(loginResponse.UpstreamUserId, null);
			disableLogWriting = false;
			lock (lockObject) {
				// hold on to re-login delegate for token refreshing, capturing needed credentials
				refreshLoginDelegate = async ct => await reloginDelegate(ct);
				disableLogUploading = false;
			}
			CurrentClientMode = SglAnalyticsClientMode.DelegatedOnline;
		}

		/// <summary>
		/// Asynchronously attempts to authenticate the user with credentials stored in the local root data store.
		/// For this to work, valid credentials username + password or user id + device token must be present in the data store.
		/// This should be the case after <see cref="RegisterUserWithDeviceSecretAsync(BaseUserData, CancellationToken)"/> or after
		/// <see cref="RegisterUserWithPasswordAsync(BaseUserData, string, bool, CancellationToken)"/> or <see cref="TryLoginWithPasswordAsync(string, string, bool, CancellationToken)"/>
		/// with <c>rememberCredentials</c> set to <see langword="true"/>.
		/// </summary>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the registration operation, providing the result status of the login attempt as its value.</returns>
		public async Task<LoginAttemptResult> TryLoginWithStoredCredentialsAsync(CancellationToken ct = default) {
			var credentials = readStoredCredentials();
			LoginRequestDTO loginDto;
			if (credentials.UserId.HasValue && !string.IsNullOrWhiteSpace(credentials.UserSecret)) {
				loginDto = new IdBasedLoginRequestDTO(appName, appAPIToken, credentials.UserId.Value, credentials.UserSecret);
			}
			else if (!string.IsNullOrWhiteSpace(credentials.Username) && !string.IsNullOrWhiteSpace(credentials.UserSecret)) {
				loginDto = new UsernameBasedLoginRequestDTO(appName, appAPIToken, credentials.Username, credentials.UserSecret);
			}
			else {
				return LoginAttemptResult.CredentialsNotAvailable;
			}
			Func<CancellationToken, Task<LoginResponseDTO>> reloginDelegate = async ct2 => await loginAsync(loginDto, ct2);
			try {
				await reloginDelegate(ct);
			}
			catch (LoginFailedException ex) {
				logger.LogError(ex, "Login with stored credentials failed.");
				return LoginAttemptResult.Failed;
			}
			catch (Exception ex) {
				logger.LogError(ex, "An error prevented logging in with stored credentials.");
				return LoginAttemptResult.NetworkProblem;
			}
			createUserLogStore(credentials.UserId, credentials.Username);
			disableLogWriting = false;
			lock (lockObject) {
				// hold on to re-login delegate for token refreshing, capturing needed credentials
				refreshLoginDelegate = reloginDelegate;
				disableLogUploading = false;
			}
			startUploadingExistingLogs();
			CurrentClientMode = credentials.Username != null ? SglAnalyticsClientMode.UsernamePasswordOnline : SglAnalyticsClientMode.DeviceTokenOnline;
			return LoginAttemptResult.Completed;
		}
		/// <summary>
		/// Asynchronously attempts to authenticate the user with the supplied username and password.
		/// </summary>
		/// <param name="loginName">The username as supplied in the user data during registration.</param>
		/// <param name="password">The password as supplied during registration.</param>
		/// <param name="rememberCredentials">
		/// If true, the username and password are stored in the local root data store
		/// to allow login using <see cref="TryLoginWithStoredCredentialsAsync(CancellationToken)"/> in the future.
		/// </param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the registration operation, providing the result status of the login attempt as its value.</returns>
		public async Task<LoginAttemptResult> TryLoginWithPasswordAsync(string loginName, string password, bool rememberCredentials = false, CancellationToken ct = default) {
			var loginDto = new UsernameBasedLoginRequestDTO(appName, appAPIToken, loginName, password);
			Func<CancellationToken, Task<LoginResponseDTO>> reloginDelegate = async ct2 => await loginAsync(loginDto, ct2);
			LoginResponseDTO responseDto;
			try {
				responseDto = await reloginDelegate(ct);
			}
			catch (LoginFailedException ex) {
				logger.LogError(ex, "Login with stored credentials failed.");
				return LoginAttemptResult.Failed;
			}
			catch (Exception ex) {
				logger.LogError(ex, "An error prevented logging in with stored credentials.");
				return LoginAttemptResult.NetworkProblem;
			}
			createUserLogStore(responseDto.UserId, loginName);
			disableLogWriting = false;
			lock (lockObject) {
				// hold on to re-login delegate for token refreshing, capturing needed credentials
				refreshLoginDelegate = reloginDelegate;
				disableLogUploading = false;
			}
			if (rememberCredentials) {
				await storeCredentialsAsync(loginName, password, LoggedInUserId);
			}
			startUploadingExistingLogs();
			CurrentClientMode = SglAnalyticsClientMode.UsernamePasswordOnline;
			return LoginAttemptResult.Completed;
		}
		/// <summary>
		/// Asynchronously attempts to authenticate the user using delegated authentication with the trusted upstream backend
		/// for the application and using the upstream authorization token provided by <paramref name="getUpstreamAuthToken"/>.
		/// </summary>
		/// <param name="getUpstreamAuthToken">
		/// A delegate used for obtaining the current authorization token for the trusted upstream system.
		/// This is initially invoked once for establishing the session.
		/// Afterwards, it is invoked when the session is refreshed because it has expired or is close to expiring.
		/// </param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the registration operation, providing the result status of the login attempt as its value.</returns>
		public async Task<LoginAttemptResult> TryLoginWithUpstreamDelegationAsync(Func<CancellationToken, Task<AuthorizationData>> getUpstreamAuthToken, CancellationToken ct = default) {
			Func<CancellationToken, Task<DelegatedLoginResponseDTO>> reloginDelegate = async ct2 => {
				try {
					logger.LogInformation("Logging in user with upstream delegation ...");
					var result = await userRegistrationClient.OpenSessionFromUpstream(await getUpstreamAuthToken(ct2), ct2);
					logger.LogInformation("Login was successful.");
					return result;
				}
				catch (Exception ex) {
					logger.LogError(ex, "Login with upstream delegation failed with exception.");
					throw;
				}
			};
			DelegatedLoginResponseDTO loginResponse;
			try {
				loginResponse = await reloginDelegate(ct);
			}
			catch (LoginFailedException ex) {
				logger.LogError(ex, "Login with upstream delegation failed.");
				return LoginAttemptResult.Failed;
			}
			catch (NoDelegatedUserException ex) {
				logger.LogError(ex, "Login with upstream delegation did not succeed because the upstream user is not registered yet.");
				return LoginAttemptResult.CredentialsNotAvailable;
			}
			catch (Exception ex) {
				logger.LogError(ex, "An error prevented logging in with upstream delegation.");
				return LoginAttemptResult.NetworkProblem;
			}
			createUserLogStore(loginResponse.UpstreamUserId, null);
			disableLogWriting = false;
			lock (lockObject) {
				// hold on to re-login delegate for token refreshing, capturing needed credentials
				refreshLoginDelegate = async ct => await reloginDelegate(ct);
				disableLogUploading = false;
			}
			startUploadingExistingLogs();
			CurrentClientMode = SglAnalyticsClientMode.DelegatedOnline;
			return LoginAttemptResult.Completed;
		}
		/// <summary>
		/// Puts the client in offline mode.
		/// If there are stored credentials, the recorded logs will be stored locally and associated with the user identified by the credentials.
		/// Otherwise, if <paramref name="allowAnonymous"/> is true, the recoded logs are stored as anonymous logs and can be adopted later using
		/// <see cref="CheckForAnonymousLogsAsync(CancellationToken)"/> and <see cref="InheritAnonymousLogsAsync(IEnumerable{Guid}, CancellationToken)"/>.
		/// </summary>
		/// <param name="allowAnonymous">
		/// Allow falling back to anonymous log recording when no credentials are present.
		/// As anonymous logs result in the problem of later having to manually adopt them, this defaults to false,
		/// which makes the method fail if no credentials are present.
		/// </param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the registration operation.</returns>
		/// <exception cref="InvalidOperationException">When no stored credentials were present to indicate the user and <paramref name="allowAnonymous"/> was false.</exception>
		/// <remarks>
		/// Currently, this operation isn't actually asynchronous as no operations need to be awaited.
		/// The asynchronous interface for other session management methods is however kept for consistency and
		/// to allow future changes that might need asynchronous operations.
		/// </remarks>
		public Task UseOfflineModeAsync(bool allowAnonymous = false, CancellationToken ct = default) {
			var credentials = readStoredCredentials();
			if (credentials.UserId.HasValue || credentials.Username != null) {
				createUserLogStore(credentials.UserId, credentials.Username);
				CurrentClientMode = credentials.Username != null ? SglAnalyticsClientMode.UsernamePasswordOnline : SglAnalyticsClientMode.DeviceTokenOnline;
			}
			else if (allowAnonymous) {
				lock (lockObject) {
					currentLogStorage = anonymousLogStorage;
				}
				CurrentClientMode = SglAnalyticsClientMode.AnonymousOffline;
			}
			else {
				disableLogWriting = true;
				lock (lockObject) {
					disableLogUploading = true;
				}
				throw new InvalidOperationException("No stored credentials were present and falling back to anonymous operation was not allowed.");
			}
			disableLogWriting = false;
			lock (lockObject) {
				disableLogUploading = true;
			}
			return Task.CompletedTask; // For consistency, make all session-state methods async, also to allow future expansions that might need async.
		}
		/// <summary>
		/// Puts the client in offline mode.
		/// A known upstream user id is supplied in <paramref name="upstreamUserId"/> to associate the recorded log files with that user.
		/// This can be used when the client of the upstream system doe also support an offline mode and
		/// therefore knows the id of the intended user.
		/// As the analytics system can't verify such an id while offline, the caller is trusted here to only supply valid and correct user ids.
		/// </summary>
		/// <param name="upstreamUserId">
		/// The user id of the current user in the trusted upstream system.
		/// The calling client application needs to make sure this is actually the id of the user,
		/// e.g. by obtaining it from the offline mode of the client of the upstream system.
		/// </param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the registration operation.</returns>
		/// <remarks>
		/// Currently, this operation isn't actually asynchronous as no operations need to be awaited.
		/// The asynchronous interface for other session management methods is however kept for consistency and
		/// to allow future changes that might need asynchronous operations.
		/// </remarks>
		public Task UseOfflineModeForDelegatedUserAsync(Guid upstreamUserId, CancellationToken ct = default) {
			createUserLogStore(upstreamUserId, null);
			disableLogWriting = false;
			lock (lockObject) {
				disableLogUploading = true;
			}
			CurrentClientMode = SglAnalyticsClientMode.DelegatedOffline;
			return Task.CompletedTask; // For consistency, make all session-state methods async, also to allow future expansions that might need async.
		}
		/// <summary>
		/// Deletes stored credentials from the root data store.
		/// By-default, only stored usernames and passwords are deleted, but removing a device token is refused to avoid
		/// losing access to the account. To also allow deleting device tokens, <paramref name="allowDeviceTokenDeletion"/>
		/// needs to be set to true.
		/// </summary>
		/// <param name="allowDeviceTokenDeletion">Also allow deleting device token credentials.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		/// <exception cref="InvalidOperationException">The stored credentials are for a device token and <paramref name="allowDeviceTokenDeletion"/> was false.</exception>
		public async Task DeleteStoredCredentialsAsync(bool allowDeviceTokenDeletion = false) {
			lock (lockObject) {
				if (rootDataStore.Username == null && !allowDeviceTokenDeletion) {
					throw new InvalidOperationException("The stored credentials are a device token and device token deletion was not allowed to not make the account inaccessible.");
				}
				rootDataStore.UserID = null;
				rootDataStore.UserSecret = null;
				rootDataStore.Username = null;
			}
			await rootDataStore.SaveAsync();
		}
		/// <summary>
		/// Deactivates this client object.
		/// This makes <see cref="StartNewLog"/>, <see cref="StartRetryUploads"/> and all recording methods No-Ops.
		/// This can be used to implement a temporary opt-out.
		/// Remembering the opt-out needs to be done externally.
		/// </summary>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the registration operation.</returns>
		/// <remarks>
		/// Currently, this operation isn't actually asynchronous as no operations need to be awaited.
		/// The asynchronous interface for other session management methods is however kept for consistency and
		/// to allow future changes that might need asynchronous operations.
		/// </remarks>
		public async Task DeactivateAsync(CancellationToken ct = default) {
			await FinishAsync();
			disableLogWriting = true;
			lock (lockObject) {
				disableLogUploading = true;
				currentLogStorage = null!;
			}
			CurrentClientMode = SglAnalyticsClientMode.Deactivated;
			await Task.CompletedTask; // For consistency, make all session-state methods async, also to allow future expansions that might need async.
		}
		/// <summary>
		/// Checks for and lists locally stored anonymous logs that were recorded in offline mode without a valid user identification
		/// (after using <see cref="UseOfflineModeAsync(bool, CancellationToken)"/> with <c>allowAnonymous</c> being false).
		/// When back online, these logs can be adopted by the current user using <see cref="InheritAnonymousLogsAsync(IEnumerable{Guid}, CancellationToken)"/>.
		/// If done, this should likely involve asking the user which of these play sessions are thiers.
		/// While this is an unfortunate requirement, but as we didn't know the user when these were recorded, they need to be manually claimed later.
		/// This can be checked after login or on a special settings sub page.
		/// </summary>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the registration operation, providing the obtained list upon success.</returns>
		/// <remarks>
		/// Currently, this operation isn't actually asynchronous as no operations need to be awaited.
		/// The asynchronous interface for other account management methods is however kept for consistency and
		/// to allow future changes that might need asynchronous operations.
		/// </remarks>
		public Task<IList<(Guid Id, DateTime Start, DateTime End)>> CheckForAnonymousLogsAsync(CancellationToken ct = default) {
			List<ILogStorage.ILogFile> existingLogs;
			lock (lockObject) {
				existingLogs = anonymousLogStorage.EnumerateLogs().ToList();
			}
			var result = existingLogs.Select(log => (log.ID, log.CreationTime, log.EndTime)).ToList();
			// For consistency, make all session-state methods async, also to allow future expansions that might need async.
			return Task.FromResult<IList<(Guid Id, DateTime Start, DateTime End)>>(result);
		}
		/// <summary>
		/// Adopts logs from a list provided by <see cref="CheckForAnonymousLogsAsync"/> to the currently authenticated user account.
		/// </summary>
		/// <param name="logIds">The log ids to adopt.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the registration operation.</returns>
		/// <remarks>
		/// Currently, this operation isn't actually asynchronous as no operations need to be awaited.
		/// The asynchronous interface for other account management methods is however kept for consistency and
		/// to allow future changes that might need asynchronous operations.
		/// </remarks>
		public Task InheritAnonymousLogsAsync(IEnumerable<Guid> logIds, CancellationToken ct = default) {
			if (!SessionAuthorizationValid) {
				throw new InvalidOperationException("Can't inherit anonymous logs for upload to user account without valid user session.");
			}
			if (disableLogUploading) {
				throw new InvalidOperationException("Can't inherit anonymous logs for upload to user account while uploading is disabled.");
			}
			var logIdSet = logIds.ToHashSet();
			List<ILogStorage.ILogFile> existingLogs;
			lock (lockObject) {
				existingLogs = anonymousLogStorage.EnumerateLogs().ToList();
			}
			var selectedLogs = existingLogs.Where(log => logIdSet.Contains(log.ID)).ToList();
			if (selectedLogs.Count == 0) return Task.CompletedTask;
			foreach (var logFile in selectedLogs) {
				uploadQueue.Enqueue(logFile);
			}
			startFileUploadingIfNotRunning();
			return Task.CompletedTask; // For consistency, make all session-state methods async, also to allow future expansions that might need async.
		}

		/// <summary>
		/// Ends the current analytics log file if there is one, and begin a new log file to which subsequent Record-operations write their data.
		/// Call this when starting a new session, e.g. a new game playthrough or a more short-term game session.
		/// </summary>
		/// <remarks>
		/// Other state-changing operations (<c>StartNewLog</c>, <c>RegisterAsync</c>, <c>FinishAsync</c>, or the <c>Record</c>... operations) on the current object must not be called concurrently with this.
		/// </remarks>
		public Guid StartNewLog() {
			if (CurrentClientMode == SglAnalyticsClientMode.Uninitialized) {
				throw new InvalidOperationException(
					"Client object is in uninitialized state. Select operation mode first by calling one of the " +
					"Register..., TryLogin..., or UseOfflineMode... or explicitly disable client object using DeactivateAsync.");
			}
			if (disableLogWriting) return Guid.Empty;
			LogQueue? oldLogQueue;
			LogQueue? newLogQueue;
			Guid logId;
			lock (lockObject) {
				oldLogQueue = currentLogQueue;
				currentLogQueue = newLogQueue = new LogQueue(currentLogStorage.CreateLogFile(out var logFile), logFile);
				logId = logFile.ID;
			}
			pendingLogQueues.Enqueue(newLogQueue);
			oldLogQueue?.entryQueue?.Finish();
			if (oldLogQueue is null) {
				logger.LogInformation("Started new data log file {newId}.", logId);
			}
			else {
				logger.LogInformation("Started new data log file {newId} and finished old data log file {oldId}.", logId, oldLogQueue.logFile.ID);
			}
			startLogWritingIfNotRunning();
			return logId;
		}

		/// <summary>
		/// Ends the current analytics log file if there is one.
		/// Call this when a session is finished, e.g. a game playthrough a more short-term game session is over or the user aborted it.
		/// After this, there is no current log file and <see cref="StartNewLog"/> must be called before further Record-operations, otherwise those will fail.
		/// </summary>
		/// <returns>The id of the ended log, or null if there was no current log.</returns>
		/// <remarks>
		/// The actual ending of the log (flushing to disk, writing the end marker, and closing the file) and the subsequent upload of the file happen asynchronously in the background.
		/// To wait for those processes to finish, <see cref="FinishAsync"/> can be used to close all current log state,
		/// wait for all background processes to finish, and rebuild the internal state.
		/// </remarks>
		public Guid? EndLog() {
			if (disableLogWriting) return Guid.Empty;
			LogQueue? oldLogQueue;
			lock (lockObject) {
				oldLogQueue = currentLogQueue;
				currentLogQueue = null;
			}
			oldLogQueue?.entryQueue?.Finish();
			if (oldLogQueue != null) {
				logger.LogInformation("Finished data log file {oldId}.", oldLogQueue.logFile.ID);
			}
			else {
				logger.LogDebug(nameof(EndLog) + " was called while there was no active log file.");
			}
			startLogWritingIfNotRunning();
			return oldLogQueue?.logFile?.ID;
		}

		/// <summary>
		/// This method needs to be called before the exiting the application, waiting for the returned Task object, to ensure all log entries are written to disk and to attempt to upload the pending log files.
		/// </summary>
		/// <returns>A Task object that represents the asynchronous finishing operations.</returns>
		/// <remarks>
		/// Uploading may fail for various reasons:
		/// <list type="bullet">
		///		<item><description>The client is not yet fully registered and has thus not obtained a valid user id yet. In this case, the upload is not attempted in the first place and this method only flushed in-memory queues to the log files. Those are only kept locally.</description></item>
		///		<item><description>The client has no connection to the internet. The upload will be retried later, when the application is used again.</description></item>
		///		<item><description>The backend server is not operating correctly. The upload will be retried later, when the application is used again.</description></item>
		///		<item><description>The server rejects the upload due to an invalid user id or application id. In case of a transient configuration error, the upload will be retried later, when the application is used again. The server should also log this problem for investigation.</description></item>
		///		<item><description>The server rejects the upload due to exceeding the maximum file size. In this case, the file is moved to a special directory for further investigation to not waste the users bandwidth with further attempts. The server should also log this problem to indicate, that an application generates larger than expected log files.</description></item>
		/// </list>
		///
		/// Other state-changing operations (<c>StartNewLog</c>, <c>RegisterAsync</c>, <c>FinishAsync</c>, or the <c>Record</c>... operations) on the current object must not be called, between start and completion of this operation.
		/// </remarks>
		public async Task FinishAsync() {
			logger.LogDebug("Finishing asynchronous data log writing and uploading...");
			Task? logWriter;
			lock (lockObject) {
				logWriter = this.logWriter;
			}

			currentLogQueue?.entryQueue?.Finish();
			pendingLogQueues.Finish();

			if (logWriter is not null) {
				await logWriter;
			}
			else {
				uploadQueue.Finish();
			}
			Task? logUploader;
			lock (lockObject) {
				logUploader = this.logUploader;
			}
			if (logUploader is not null) {
				await logUploader;
			}
			// At this point, logWriter and logUploader are completed or were never started.
			// We can therefore restore the initial state before the first StartNewLog call safely without lock-based coordination.
			this.logWriter = null;
			this.logUploader = null;
			currentLogQueue = null;
			// As a completed channel can not be reopened, we need to replace the queue object (containing the channel) itself.
			pendingLogQueues = new AsyncConsumerQueue<LogQueue>();
			uploadQueue = new AsyncConsumerQueue<ILogStorage.ILogFile>();
			logger.LogInformation("Finished asynchronous data log writing and uploading.");
		}

		/// <summary>
		/// Retries the upload of analytics log files that are stored locally because their upload previously failed, e.g. because no internet connectivity was available or a server error prevented the upload.
		/// </summary>
		/// <remarks>
		/// This operation only enqueues the existing files for upload in the background and starts the asynchronous upload worker process if the requirements are met, i.e. if the user is registered and there are files to upload.
		/// As the previously failed files are enqueued in the same queue as the freshly written ones from <see cref="StartNewLog"/>, there is no separate mechanism to wait for the completion of the upload of only the retried files.
		/// Instead, waiting for <see cref="FinishAsync"/> finishes the current log, eneuques it and then waits for all enqueued uploads to finish (or fail).
		/// </remarks>
		public void StartRetryUploads() {
			if (disableLogUploading) return;
			startUploadingExistingLogs();
		}

		/// <summary>
		/// Record the given event object to the current analytics log file, tagged with the given channel for categorization and with the current time according to the client's local clock.
		/// </summary>
		/// <param name="channel">A channel name that is used to categorize analytics log entries into multiple logical data streams.</param>
		/// <param name="eventObject">The event payload data to write to the log in JSON form. The object needs to be clonable to obtain an unshared copy because the log recording to disk is done asynchronously and the object content otherwise might have changed when it is read leater. If the object is created specifically for this call, or will not be modified after the call, call RecordEventUnshared instead to avoid this copy.</param>
		/// <remarks>
		/// The recorded entry has a field containing the event type as a string. If the dynamic type of eventObject has an <c>[EventType(name)]</c> attribute (<see cref="EventTypeAttribute"/>), the name given there ist used. Otherwise the name of the class itself is used.
		///
		/// This operation can be invoked concurrently with other <c>Record</c>... methods, but NOT concurrently with <c>StartNewLog</c> and <c>FinishAsync</c>.
		/// </remarks>
		public void RecordEvent(string channel, ICloneable eventObject) {
			if (disableLogWriting) return;
			if (currentLogQueue is null) { throw new InvalidOperationException("Can't record entries to current event log, because no log was started. Call StartNewLog() before attempting to record entries."); }
			RecordEventUnshared(channel, eventObject.Clone());
		}

		/// <summary>
		/// Record the given event object to the current analytics log file, tagged with the given channel for categorization and with the current time according to the client's local clock.
		/// </summary>
		/// <param name="channel">A channel name that is used to categorize analytics log entries into multiple logical data streams.</param>
		/// <param name="eventObject">The event payload data to write to the log in JSON form. The object needs to be clonable to obtain an unshared copy because the log recording to disk is done asynchronously and the object content otherwise might have changed when it is read leater. If the object is created specifically for this call, or will not be modified after the call, call RecordEventUnshared instead to avoid this copy.</param>
		/// <param name="eventType">The name to use for the event type field of the recorded log entry.</param>
		/// <remarks>
		/// The recorded entry has a field containing the event type as a string.
		/// This overload simply sets the value given in <paramref name="eventType"/> as the entry's event type field,
		/// which allows common types such as collection objects to be used for <paramref name="eventObject"/>,
		/// as they don't have an <see cref="EventTypeAttribute"/> and their type name is also not suitable for usage in the log entry.
		/// For custom event object types, it is usually recommended to use <see cref="RecordEvent(string, ICloneable)"/> instead.
		///
		/// This operation can be invoked concurrently with other <c>Record</c>... methods, but NOT concurrently with <c>StartNewLog</c> and <c>FinishAsync</c>.
		/// </remarks>
		public void RecordEvent(string channel, ICloneable eventObject, string eventType) {
			if (disableLogWriting) return;
			if (currentLogQueue is null) { throw new InvalidOperationException("Can't record entries to current event log, because no log was started. Call StartNewLog() before attempting to record entries."); }
			RecordEventUnshared(channel, eventObject.Clone(), eventType);
		}

		/// <summary>
		/// Record the given event object to the current analytics log file, tagged with the given channel for categorization and with the current time according to the client's local clock.
		/// </summary>
		/// <param name="channel">A channel name that is used to categorize analytics log entries into multiple logical data streams.</param>
		/// <param name="eventObject">The event payload data to write to the log in JSON form. As the log recording to disk is done asynchronously, the ownership of the given object is transferred to the analytics client and must not be changed by the caller afterwards. The easiest way to ensure this is by creating the event object inside the call and not holding other references to it.</param>
		/// <param name="eventType">The name to use for the event type field of the recorded log entry.</param>
		/// <remarks>
		/// The recorded entry has a field containing the event type as a string.
		/// This overload simply sets the value given in <paramref name="eventType"/> as the entry's event type field,
		/// which allows common types such as collection objects to be used for <paramref name="eventObject"/>,
		/// as they don't have an <see cref="EventTypeAttribute"/> and their type name is also not suitable for usage in the log entry.
		/// For custom event object types, it is usually recommended to use <see cref="RecordEventUnshared(string, object)"/> instead.
		///
		/// This operation can be invoked concurrently with other <c>Record</c>... methods, but NOT concurrently with <c>StartNewLog</c> and <c>FinishAsync</c>.
		/// </remarks>
		public void RecordEventUnshared(string channel, object eventObject, string eventType) {
			if (disableLogWriting) return;
			if (currentLogQueue is null) { throw new InvalidOperationException("Can't record entries to current event log, because no log was started. Call StartNewLog() before attempting to record entries."); }
			currentLogQueue.entryQueue.Enqueue(new LogEntry(LogEntry.EntryMetadata.NewEventEntry(channel, DateTimeOffset.Now, eventType), eventObject));
		}

		/// <summary>
		/// Record the given event object to the current analytics log file, tagged with the given channel for categorization and with the current time according to the client's local clock.
		/// </summary>
		/// <param name="channel">A channel name that is used to categorize analytics log entries into multiple logical data streams.</param>
		/// <param name="eventObject">The event payload data to write to the log in JSON form. As the log recording to disk is done asynchronously, the ownership of the given object is transferred to the analytics client and must not be changed by the caller afterwards. The easiest way to ensure this is by creating the event object inside the call and not holding other references to it.</param>
		/// <remarks>
		/// The recorded entry has a field containing the event type as a string. If the dynamic type of eventObject has an <c>[EventType(name)]</c> attribute (<see cref="EventTypeAttribute"/>), the name given there ist used. Otherwise the name of the class itself is used.
		///
		/// This operation can be invoked concurrently with other <c>Record</c>... methods, but NOT concurrently with <c>StartNewLog</c> and <c>FinishAsync</c>.
		/// </remarks>
		public void RecordEventUnshared(string channel, object eventObject) {
			if (disableLogWriting) return;
			if (currentLogQueue is null) { throw new InvalidOperationException("Can't record entries to current event log, because no log was started. Call StartNewLog() before attempting to record entries."); }
			var eventType = eventObject.GetType();
			var attributes = eventType.GetCustomAttributes(typeof(EventTypeAttribute), false);
			var eventTypeName = attributes.Cast<EventTypeAttribute>().SingleOrDefault()?.EventTypeName ?? eventType.Name;
			RecordEventUnshared(channel, eventObject, eventTypeName);
		}

		/// <summary>
		/// Record the given snapshot data for an application object to the current analytics log file, tagged with the given channel for categorization, with the id of the object, and with the current time according to the client's local clock.
		/// </summary>
		/// <param name="channel">A channel name that is used to categorize analytics log entries into multiple logical data streams.</param>
		/// <param name="objectId">An ID of the snapshotted object.</param>
		/// <param name="snapshotPayloadData">An object encapsulating the snapshotted object state to write to the log in JSON form. As the log recording to disk is done asynchronously, the ownership of the given object is transferred to the analytics client and must not be changed by the caller afterwards. The easiest way to ensure this is by creating the snapshot state object inside the call and not holding other references to it.</param>
		/// <remarks>This operation can be invoked concurrently with other <c>Record</c>... methods, but NOT concurrently with <c>StartNewLog</c> and <c>FinishAsync</c>.</remarks>
		public void RecordSnapshotUnshared(string channel, object objectId, object snapshotPayloadData) {
			if (disableLogWriting) return;
			if (currentLogQueue is null) { throw new InvalidOperationException("Can't record entries to current event log, because no log was started. Call StartNewLog() before attempting to record entries."); }
			currentLogQueue.entryQueue.Enqueue(new LogEntry(LogEntry.EntryMetadata.NewSnapshotEntry(channel, DateTimeOffset.Now, objectId), snapshotPayloadData));
		}
	}
}
