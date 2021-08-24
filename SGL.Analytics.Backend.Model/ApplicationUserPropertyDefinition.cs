using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Model {
	[Index(nameof(AppId), nameof(Name), IsUnique = true)]
	public class ApplicationUserPropertyDefinition {
		public int Id { get; set; }
		public int AppId { get; set; }
		public ApplicationWithUserProperties App { get; set; }
		public string Name { get; set; }
		public UserPropertyType Type { get; set; }

		public ApplicationUserPropertyDefinition(int id, int appId, ApplicationWithUserProperties app,
			string name, UserPropertyType type) {
			Id = id;
			AppId = appId;
			App = app;
			Name = name;
			Type = type;
		}
	}
}
