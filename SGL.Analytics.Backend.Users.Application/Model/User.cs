using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Model {
	public interface UserRegistrationWrapper {
		public UserRegistration Underlying { get; set; }

		public void LoadAppPropertiesFromUnderlying();
		public void StoreAppPropertiesToUnderlying();
	}

	public class User : UserRegistrationWrapper {
		private UserRegistration userReg;

		public Guid Id => userReg.Id;
		public ApplicationWithUserProperties App { get => userReg.App; set => userReg.App = value; }
		public string Username { get => userReg.Username; set => userReg.Username = value; }

		public Dictionary<string, object?> AppSpecificProperties { get; private set; }
		UserRegistration UserRegistrationWrapper.Underlying { get => userReg; set => userReg = value; }

		public User(UserRegistration userReg) {
			this.userReg = userReg;
			AppSpecificProperties = loadAppProperties();
		}

		private Dictionary<string, object?> loadAppProperties() {
			return userReg.AppSpecificProperties.ToDictionary(p => p.Definition.Name, p => p.Value);
		}

		public UserRegistrationResultDTO AsRegistrationResult() => new UserRegistrationResultDTO(Id);

		void UserRegistrationWrapper.LoadAppPropertiesFromUnderlying() {
			AppSpecificProperties = loadAppProperties();
		}

		void UserRegistrationWrapper.StoreAppPropertiesToUnderlying() {
			foreach (var dictProp in AppSpecificProperties) {
				userReg.SetAppSpecificProperty(dictProp.Key, dictProp.Value);
			}
		}
	}
}
