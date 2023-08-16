using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Utilities.Crypto.EndToEnd;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;

namespace SGL.Analytics.Backend.Domain.Entity {

	/// <summary>
	/// Models a registration of a user of a specific application.
	/// </summary>
	public class UserRegistration {
		/// <summary>
		/// The unique id of the user.
		/// </summary>
		public Guid Id { get; set; }
		/// <summary>
		/// The id of the app for which the user is registered.
		/// </summary>
		public Guid AppId { get; set; }
		/// <summary>
		/// The app for which the user is registered.
		/// </summary>
		public ApplicationWithUserProperties App { get; set; } = null!;
		/// <summary>
		/// The username of the user.
		/// </summary>
		public string Username { get; set; }
		/// <summary>
		/// The login secret of the user in hashed and salted form.
		/// </summary>
		public string? HashedSecret { get; set; }

		/// <summary>
		/// The application-specific property instances containing the values of the properties for this user.
		/// </summary>
		public ICollection<ApplicationUserPropertyInstance> AppSpecificProperties { get; set; } = null!;

		/// <summary>
		/// Stores end-to-end encrypted bytes containing sensitive application-specific properties about the user.
		///	As the encryption is end-to-end this property in itself is opaque, but the usual use-case is for the client to store an encrypted JSON document here.
		///	The metadata needed for decryption is stored in <see cref="PropertyInitializationVector"/>, <see cref="PropertyEncryptionMode"/>,
		///	<see cref="PropertySharedPublicKey"/>, and <see cref="PropertyRecipientKeys"/>, with <see cref="PropertyEncryptionInfo"/> as a bundled object for convenience.
		/// </summary>
		public byte[] EncryptedProperties { get; set; }
		/// <summary>
		/// Stores the initialization vector for the encryption of <see cref="EncryptedProperties"/>.
		/// </summary>
		public byte[] PropertyInitializationVector { get; set; }
		/// <summary>
		/// Stores the encryption mode used for the encryption of <see cref="EncryptedProperties"/>.
		/// </summary>
		public DataEncryptionMode PropertyEncryptionMode { get; set; }
		/// <summary>
		/// Stores the shared EC per-registration public key for ECDH if it is used for the encryption of <see cref="EncryptedProperties"/>.
		/// </summary>
		public byte[]? PropertySharedPublicKey { get; set; }
		/// <summary>
		/// Stores the data key for the encryption of <see cref="EncryptedProperties"/>, encrypted for each authorized recipient as a separate copy.
		/// </summary>
		public ICollection<UserRegistrationPropertyRecipientKey> PropertyRecipientKeys { get; set; } = null!;

		public Guid? BasicFederationUpstreamUserId { get; set; } = null;

		/// <summary>
		/// Provides access to the encryption metadata for <see cref="EncryptedProperties"/> in bundled form.
		/// The object is packed / unpacked into separate properties on-the-fly, thus the property should not be accessed in a tight loop, but the value should be copied locally.
		/// </summary>
		public EncryptionInfo PropertyEncryptionInfo {
			get {
				if (PropertyRecipientKeys == null) {
					return null!;
				}
				return new EncryptionInfo {
					DataMode = PropertyEncryptionMode,
					IVs = new List<byte[]> { PropertyInitializationVector },
					MessagePublicKey = PropertySharedPublicKey,
					DataKeys = PropertyRecipientKeys.ToDictionary(lrk => lrk.RecipientKeyId, lrk => lrk.ToDataKeyInfo())
				};
			}
			set {
				PropertyEncryptionMode = value.DataMode;
				PropertyInitializationVector = value.IVs.Single();
				PropertySharedPublicKey = value.MessagePublicKey;
				var currentKeys = PropertyRecipientKeys.ToDictionary(lrk => lrk.RecipientKeyId);

				PropertyRecipientKeys.Where(rk => !value.DataKeys.ContainsKey(rk.RecipientKeyId)).ToList().ForEach(rk => PropertyRecipientKeys.Remove(rk));
				foreach (var rk in PropertyRecipientKeys) {
					var newValues = value.DataKeys[rk.RecipientKeyId];
					rk.EncryptedKey = newValues.EncryptedKey;
					rk.EncryptionMode = newValues.Mode;
					rk.UserPropertiesPublicKey = newValues.MessagePublicKey;
				}
				value.DataKeys.Where(pair => !currentKeys.ContainsKey(pair.Key)).Select(pair => new UserRegistrationPropertyRecipientKey {
					UserId = Id,
					RecipientKeyId = pair.Key,
					EncryptionMode = pair.Value.Mode,
					EncryptedKey = pair.Value.EncryptedKey,
					UserPropertiesPublicKey = pair.Value.MessagePublicKey
				}).ToList().ForEach(k => PropertyRecipientKeys.Add(k));
			}

		}

		/// <summary>
		/// Creates a user registration object with the given data values.
		/// This constructor is intended to be used by the OR mapper. To create a new application,
		/// see <see cref="Create(ApplicationWithUserProperties, string, string?, byte[], EncryptionInfo, Guid?)"/> or its overloads.
		/// </summary>
		public UserRegistration(Guid id, Guid appId, string username, string? hashedSecret, byte[] encryptedProperties,
			byte[] propertyInitializationVector, DataEncryptionMode propertyEncryptionMode, byte[]? propertySharedPublicKey,
			Guid? basicFederationUpstreamUserId = null) {
			Id = id;
			AppId = appId;
			Username = username;
			HashedSecret = hashedSecret;
			EncryptedProperties = encryptedProperties;
			PropertyInitializationVector = propertyInitializationVector;
			PropertyEncryptionMode = propertyEncryptionMode;
			PropertySharedPublicKey = propertySharedPublicKey;
			BasicFederationUpstreamUserId = basicFederationUpstreamUserId;
		}

		/// <summary>
		/// Creates an user registration object with the given id for the given application and with the given data values.
		/// </summary>
		/// <param name="id">The id of the user.</param>
		/// <param name="app">The application for which the user is registered.</param>
		/// <param name="username">The username of the user.</param>
		/// <param name="hashedSecret">The hashed and salted login secret of the user.</param>
		/// <returns>The created object.</returns>
		public static UserRegistration Create(Guid id, ApplicationWithUserProperties app, string username, string? hashedSecret,
			Guid? basicFederationUpstreamUserId = null) =>
			Create(id, app, username, hashedSecret, new byte[0], EncryptionInfo.CreateUnencrypted(), basicFederationUpstreamUserId);
		public static UserRegistration Create(Guid id, ApplicationWithUserProperties app, string username, string? hashedSecret,
				byte[] encryptedProperties, EncryptionInfo propertyEncryptionInfo, Guid? basicFederationUpstreamUserId = null) {
			var userReg = new UserRegistration(id, app.Id, username, hashedSecret, encryptedProperties,
				propertyEncryptionInfo.IVs.Single(), propertyEncryptionInfo.DataMode, propertyEncryptionInfo.MessagePublicKey,
				basicFederationUpstreamUserId);
			userReg.App = app;
			userReg.AppSpecificProperties = new List<ApplicationUserPropertyInstance>();
			userReg.PropertyRecipientKeys = new List<UserRegistrationPropertyRecipientKey>();
			userReg.PropertyEncryptionInfo = propertyEncryptionInfo;
			return userReg;
		}



		/// <summary>
		/// Creates an user registration object with a generated id for the given application and with the given data values.
		/// </summary>
		/// <param name="app">The application for which the user is registered.</param>
		/// <param name="username">The username of the user.</param>
		/// <param name="hashedSecret">The hashed and salted login secret of the user.</param>
		/// <returns>The created object.</returns>
		public static UserRegistration Create(ApplicationWithUserProperties app, string username, string? hashedSecret, Guid? basicFederationUpstreamUserId = null) =>
			Create(app, username, hashedSecret, new byte[0], EncryptionInfo.CreateUnencrypted(), basicFederationUpstreamUserId);
		public static UserRegistration Create(ApplicationWithUserProperties app, string username, string? hashedSecret,
				byte[] encryptedProperties, EncryptionInfo propertyEncryptionInfo, Guid? basicFederationUpstreamUserId = null) {
			return Create(Guid.NewGuid(), app, username, hashedSecret, encryptedProperties, propertyEncryptionInfo, basicFederationUpstreamUserId);
		}

		/// <summary>
		/// Creates an user registration object with a generated id for the given application and with the given data values, using the id as the username.
		/// </summary>
		/// <param name="app">The application for which the user is registered.</param>
		/// <param name="hashedSecret">The hashed and salted login secret of the user.</param>
		/// <returns>The created object.</returns>
		public static UserRegistration Create(ApplicationWithUserProperties app, string? hashedSecret, Guid? basicFederationUpstreamUserId = null) =>
			Create(app, hashedSecret, new byte[0], EncryptionInfo.CreateUnencrypted(), basicFederationUpstreamUserId);

		public static UserRegistration Create(ApplicationWithUserProperties app, string? hashedSecret,
				byte[] encryptedProperties, EncryptionInfo propertyEncryptionInfo, Guid? basicFederationUpstreamUserId = null) {
			var id = Guid.NewGuid();
			return Create(id, app, id.ToString(), hashedSecret, encryptedProperties, propertyEncryptionInfo, basicFederationUpstreamUserId);
		}

		private ApplicationUserPropertyInstance setAppSpecificPropertyImpl(string name, object? value, Func<string, ApplicationUserPropertyDefinition> getPropDef) {
			var propInst = AppSpecificProperties.Where(p => p.Definition.Name == name).SingleOrDefault();
			if (propInst is null) {
				propInst = ApplicationUserPropertyInstance.Create(getPropDef(name), this);
				AppSpecificProperties.Add(propInst);
			}
			propInst.Value = value;
			return propInst;
		}

		/// <summary>
		/// Sets the property with the given name for this user to the given value.
		/// This either updates the value of an existing instance object, or, if no instance exists for this property for the user, creates such an instance with the given value.
		/// </summary>
		/// <param name="name">The name of the property to set.</param>
		/// <param name="value">The value to which the property shall be set.</param>
		/// <returns>The property instance object with the given name for the user.</returns>
		/// <exception cref="UndefinedPropertyException">A property with the given name is not defined for the application with which the user is associated.</exception>
		/// <exception cref="RequiredPropertyNullException">A <see langword="null"/> value was given for a property instance of a property that is defined as <see cref="ApplicationUserPropertyDefinition.Required"/>.</exception>
		/// <exception cref="PropertyTypeDoesntMatchDefinitionException">The type of the value given doesn't match the data type specified in the property definition.</exception>
		public ApplicationUserPropertyInstance SetAppSpecificProperty(string name, object? value) {
			return setAppSpecificPropertyImpl(name, value, name => {
				var propDef = App.UserProperties.Where(p => p.Name == name).SingleOrDefault();
				if (propDef is null) {
					throw new UndefinedPropertyException(name);
				}
				return propDef;
			});
		}

		/// <summary>
		/// Returns the value of the application-specific property with the given name for the user.
		/// </summary>
		/// <param name="name">The name of the property to get the value.</param>
		/// <returns>The value of the property.</returns>
		/// <exception cref="PropertyNotFoundException">No property definition with the given name was found for the user.</exception>
		/// <exception cref="RequiredPropertyNullException">A <see langword="null"/> value was encountered for a property instance of a property that is defined as <see cref="ApplicationUserPropertyDefinition.Required"/>.</exception>
		/// <exception cref="PropertyWithUnknownTypeException">An unknown property type was encountered.</exception>
		public object? GetAppSpecificProperty(string name) {
			var propInst = AppSpecificProperties.Where(p => p.Definition.Name == name).SingleOrDefault();
			if (propInst is null) {
				throw new PropertyNotFoundException(name);
			}
			return propInst.Value;
		}

		/// <summary>
		/// Sets the property represented by the given property definition for this user to the given value.
		/// This either updates the value of an existing instance object, or, if no instance exists for this property for the user, creates such an instance with the given value.
		/// Compared to <see cref="SetAppSpecificProperty(string, object?)"/>, this overload can avoid the cost of looking up the property definition and
		/// avoids <see cref="UndefinedPropertyException"/> because the property definition is already given.
		/// </summary>
		/// <param name="propDef">The definition of the property to set.</param>
		/// <param name="value">The value to which the property shall be set.</param>
		/// <returns>The relevant property instance for the user.</returns>
		/// <exception cref="RequiredPropertyNullException">A <see langword="null"/> value was given for a property instance of a property that is defined as <see cref="ApplicationUserPropertyDefinition.Required"/>.</exception>
		/// <exception cref="PropertyTypeDoesntMatchDefinitionException">The type of the value given doesn't match the data type specified in the property definition.</exception>
		public ApplicationUserPropertyInstance SetAppSpecificProperty(ApplicationUserPropertyDefinition propDef, object? value) {
			return setAppSpecificPropertyImpl(propDef.Name, value, name => propDef);
		}

		/// <summary>
		/// Returns the value of the application-specific property represented by the given definition for the user.
		/// Compared to <see cref="SetAppSpecificProperty(string, object?)"/>, this overload can avoid the cost of looking up the property definition.
		/// </summary>
		/// <param name="propDef">The definition of the property to get.</param>
		/// <returns>The value of the property for the user.</returns>
		/// <exception cref="PropertyNotFoundException">No property definition with the given name was found for the user.</exception>
		/// <exception cref="RequiredPropertyNullException">A <see langword="null"/> value was encountered for a property instance of a property that is defined as <see cref="ApplicationUserPropertyDefinition.Required"/>.</exception>
		/// <exception cref="PropertyWithUnknownTypeException">An unknown property type was encountered.</exception>
		public object? GetAppSpecificProperty(ApplicationUserPropertyDefinition propDef) {
			return GetAppSpecificProperty(propDef.Name);
		}

		/// <summary>
		/// Validates the app-specific property instances for the user against the defined property definitions of the application with which the user is associated.
		/// </summary>
		/// <exception cref="RequiredPropertyMissingException">If no instance was present for a required property.</exception>
		/// <exception cref="RequiredPropertyNullException">If the instance for a required property contained an empty value.</exception>
		/// <exception cref="UndefinedPropertyException">If a property instance references a property instance that is not correctly associated with the application of the user.</exception>
		/// <exception cref="ConflictingPropertyInstanceException">If multiple instances for the same property are present.</exception>
		public void ValidateProperties() {
			Debug.Assert(AppSpecificProperties is not null, "AppSpecificProperties navigation property is not present.");
			Debug.Assert(App is not null, "App navigation property is not present.");
			Debug.Assert(App.UserProperties is not null, "UserProperties navigation property of associated App is not present.");
			// TODO: If this method becomes a bottleneck, maybe use temporary Dictionaries / Sets to avoid O(n^2) runtime.
			// However, the involved 'n's should be quite low and this happens in-memory, just before we access the database, which should dwarf this overhead.
			foreach (var propDef in App.UserProperties.Where(p => p.Required)) {
				var propInst = AppSpecificProperties.Where(pi => pi.DefinitionId == propDef.Id).ToList();
				if (propInst.Count == 0) {
					throw new RequiredPropertyMissingException(propDef.Name);
				}
				else if (propInst.Single().IsNull()) {
					throw new RequiredPropertyNullException(propDef.Name);
				}
			}
			foreach (var propInst in AppSpecificProperties) {
				if (!App.UserProperties.Any(pd => pd.Id == propInst.DefinitionId)) {
					throw new UndefinedPropertyException(propInst.Definition.Name);
				}
				if (AppSpecificProperties.Count(p => p.Definition.Name == propInst.Definition.Name) > 1) {
					throw new ConflictingPropertyInstanceException(propInst.Definition.Name);
				}
			}
		}
	}
}
