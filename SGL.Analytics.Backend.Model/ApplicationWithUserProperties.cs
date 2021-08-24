using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Model {
	public class ApplicationWithUserProperties : Application {
		public ApplicationWithUserProperties(int id, string name, string apiToken) : base(id, name, apiToken) { }

		public ApplicationWithUserProperties(int id, string name, string apiToken, List<ApplicationUserPropertyDefinition> userProperties) : base(id, name, apiToken) {
			UserProperties = userProperties;
		}

		public List<ApplicationUserPropertyDefinition> UserProperties { get; set; } = new();
		public List<UserRegistration> UserRegistrations { get; set; } = new();
	}
}
