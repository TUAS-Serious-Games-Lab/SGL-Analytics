using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Domain.Entity {
	public class Application {
		public Guid Id { get; set; }
		public string Name { get; set; }
		public string ApiToken { get; set; }

		public Application(Guid id, string name, string apiToken) {
			Id = id;
			Name = name;
			ApiToken = apiToken;
		}
	}
}
