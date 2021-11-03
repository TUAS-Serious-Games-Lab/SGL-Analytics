using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Domain.Entity {
	/// <summary>
	/// Models a definition for a per-user property of an application.
	/// </summary>
	public class ApplicationUserPropertyDefinition {
		/// <summary>
		/// The unique database id of the  property definition.
		/// </summary>
		public int Id { get; set; }
		/// <summary>
		/// The id of the application for which this property is defined.
		/// </summary>
		public Guid AppId { get; set; }
		/// <summary>
		/// The application for which this property is defined.
		/// </summary>
		public ApplicationWithUserProperties App { get; set; } = null!;
		/// <summary>
		/// The name of the property, must be unique within the application.
		/// </summary>
		public string Name { get; set; }
		/// <summary>
		/// The data type of the property.
		/// </summary>
		public UserPropertyType Type { get; set; }
		/// <summary>
		/// Whether the property is required, otherwise it is optional.
		/// </summary>
		public bool Required { get; set; }
		/// <summary>
		/// Creates a property definition object with the given data values.
		/// </summary>
		public ApplicationUserPropertyDefinition(int id, Guid appId, string name, UserPropertyType type, bool required) {
			Id = id;
			AppId = appId;
			Name = name;
			Type = type;
			Required = required;
		}
		/// <summary>
		/// Creates a property definition for with the given name, type, and required flag for the given application.
		/// </summary>
		/// <returns>The property definition object.</returns>
		public static ApplicationUserPropertyDefinition Create(ApplicationWithUserProperties app, string name, UserPropertyType type, bool required) {
			var pd = new ApplicationUserPropertyDefinition(0, app.Id, name, type, required);
			pd.App = app;
			return pd;
		}
	}
}
