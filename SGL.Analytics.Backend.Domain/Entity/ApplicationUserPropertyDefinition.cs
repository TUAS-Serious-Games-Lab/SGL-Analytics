using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Domain.Entity {
	public class ApplicationUserPropertyDefinition {
		public int Id { get; set; }
		public Guid AppId { get; set; }
		public ApplicationWithUserProperties App { get; set; } = null!;
		public string Name { get; set; }
		public UserPropertyType Type { get; set; }
		public bool Required { get; set; }

		public ApplicationUserPropertyDefinition(int id, Guid appId, string name, UserPropertyType type, bool required) {
			Id = id;
			AppId = appId;
			Name = name;
			Type = type;
			Required = required;
		}
	}
}
