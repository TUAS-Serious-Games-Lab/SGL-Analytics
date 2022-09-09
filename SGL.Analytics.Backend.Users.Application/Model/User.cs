using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.DTO;
using SGL.Utilities.Crypto.EndToEnd;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Model {
	/// <summary>
	/// Defines mechanisms for <see cref="Interfaces.IUserManager"/> implementations to synchronize application-specific property data between an
	/// <see cref="User"/> object and the underlying <see cref="UserRegistration"/>, <see cref="ApplicationUserPropertyDefinition"/>, and <see cref="ApplicationUserPropertyInstance"/> objects.
	/// </summary>
	public interface IUserRegistrationWrapper {
		/// <summary>
		/// The underlying <see cref="UserRegistration"/> object.
		/// </summary>
		public UserRegistration Underlying { get; set; }

		/// <summary>
		/// Loads the application-specific user properties from the underlying entity objects.
		/// </summary>
		public void LoadAppPropertiesFromUnderlying();
		/// <summary>
		/// Materializes the application-specific user properties as underlying entity objects.
		/// </summary>
		public void StoreAppPropertiesToUnderlying();
	}

	/// <summary>
	/// Models a high-level representation of a registered user, where the application-specific properties are represented by a <see cref="Dictionary{TKey, TValue}"/>, instead of as a graph of entity objects as which they are stored in the lower level.
	/// </summary>
	public class User : IUserRegistrationWrapper {
		private UserRegistration userReg;

		/// <summary>
		/// The unique id of the user.
		/// </summary>
		public Guid Id => userReg.Id;
		/// <summary>
		/// The app for which the user is registered.
		/// </summary>
		public ApplicationWithUserProperties App { get => userReg.App; set => userReg.App = value; }
		/// <summary>
		/// The username of the user.
		/// </summary>
		public string Username { get => userReg.Username; set => userReg.Username = value; }
		/// <summary>
		/// The login secret of the user in hashed and salted form.
		/// </summary>
		public string HashedSecret { get => userReg.HashedSecret; set => userReg.HashedSecret = value; }

		/// <summary>
		/// The application-specific properties for this user mapped as a (potentially complex) dictionary.
		/// </summary>
		public Dictionary<string, object?> AppSpecificProperties { get; private set; }

		public byte[] EncryptedProperties { get; set; }
		public EncryptionInfo PropertyEncryptionInfo { get; set; }

		UserRegistration IUserRegistrationWrapper.Underlying { get => userReg; set => userReg = value; }

		/// <summary>
		/// Creates a user object based on the given user registration.
		/// </summary>
		/// <param name="userReg">The user registration object containing the user's data.</param>
		public User(UserRegistration userReg) {
			this.userReg = userReg;
			(this as IUserRegistrationWrapper).LoadAppPropertiesFromUnderlying();
		}

		private Dictionary<string, object?> loadAppProperties() {
			return userReg.AppSpecificProperties.ToDictionary(p => p.Definition.Name, p => p.Value);
		}

		/// <summary>
		/// Generates a user registration result represented as an <see cref="UserRegistrationResultDTO"/> for the user.
		/// </summary>
		/// <returns>A DTO to send to the client upon successful registration.</returns>
		public UserRegistrationResultDTO AsRegistrationResult() => new UserRegistrationResultDTO(Id);

		void IUserRegistrationWrapper.LoadAppPropertiesFromUnderlying() {
			AppSpecificProperties = loadAppProperties();
			EncryptedProperties = userReg.EncryptedProperties;
			PropertyEncryptionInfo = userReg.PropertyEncryptionInfo;
		}

		void IUserRegistrationWrapper.StoreAppPropertiesToUnderlying() {
			userReg.EncryptedProperties = EncryptedProperties;
			userReg.PropertyEncryptionInfo = PropertyEncryptionInfo;
			foreach (var dictProp in AppSpecificProperties) {
				userReg.SetAppSpecificProperty(dictProp.Key, dictProp.Value);
			}
		}
	}
}
