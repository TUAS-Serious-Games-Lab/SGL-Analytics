using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Domain.Entity {
	[Index(nameof(Name), IsUnique = true)]
	public class Application {
		public int Id { get; set; }
		public string Name { get; set; }
		public string ApiToken { get; set; }

		public Application(int id, string name, string apiToken) {
			Id = id;
			Name = name;
			ApiToken = apiToken;
		}
	}
}
