using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Model {
	public class ApplicationWithUserProperties : Application {
		public ICollection<ApplicationUserPropertyDefinition> UserProperties { get; set; }
		public ICollection<UserRegistration> UserRegistrations { get; set; }

		public ApplicationWithUserProperties(int id, string name, string apiToken,
			ICollection<ApplicationUserPropertyDefinition> userProperties,
			ICollection<UserRegistration> userRegistrations) :
			base(id, name, apiToken) {
			UserProperties = userProperties;
			UserRegistrations = userRegistrations;
		}
	}
}
