using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Model {
	public class ApplicationWithUserProperties : Application {
		public ICollection<ApplicationUserPropertyDefinition> UserProperties { get; set; } = null!;
		public ICollection<UserRegistration> UserRegistrations { get; set; } = null!;

		public ApplicationWithUserProperties(int id, string name, string apiToken) :
			base(id, name, apiToken) { }
	}
}
