using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Domain.Entity {
	public class ApplicationUserPropertyDefinition {
		public int Id { get; set; }
		public int AppId { get; set; }
		public ApplicationWithUserProperties App { get; set; } = null!;
		public string Name { get; set; }
		public UserPropertyType Type { get; set; }

		public ApplicationUserPropertyDefinition(int id, int appId, string name, UserPropertyType type) {
			Id = id;
			AppId = appId;
			Name = name;
			Type = type;
		}
	}
}
