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

		public static ApplicationWithUserProperties Create(string name, string apiToken) {
			return Create(Guid.NewGuid(), name, apiToken);
		}
	}
}
