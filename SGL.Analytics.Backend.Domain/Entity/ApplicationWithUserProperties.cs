using SGL.Analytics.Backend.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Domain.Entity {
	public class ApplicationWithUserProperties : Application {
		public ICollection<ApplicationUserPropertyDefinition> UserProperties { get; set; } = null!;
		public ICollection<UserRegistration> UserRegistrations { get; set; } = null!;

		public ApplicationWithUserProperties(Guid id, string name, string apiToken) :
			base(id, name, apiToken) { }

		public static ApplicationWithUserProperties Create(Guid id, string name, string apiToken) {
			var app = new ApplicationWithUserProperties(id, name, apiToken);
			app.UserProperties = new List<ApplicationUserPropertyDefinition>();
			return app;
		}

		public static new ApplicationWithUserProperties Create(string name, string apiToken) {
			return Create(Guid.NewGuid(), name, apiToken);
		}

		public ApplicationUserPropertyDefinition AddProperty(string name, UserPropertyType type, bool required) {
			if (UserProperties.Count(p => p.Name == name) > 0) {
				throw new ConflictingPropertyNameException(name);
			}
			var prop = ApplicationUserPropertyDefinition.Create(this, name, type, required);
			UserProperties.Add(prop);
			return prop;
		}
	}
}
