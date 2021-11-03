using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Domain.Entity {
	/// <summary>
	/// Represents the data type of the <see cref="ApplicationUserPropertyInstance.Value"/> for a property in its <see cref="ApplicationUserPropertyDefinition.Type"/>.
	/// </summary>
	public enum UserPropertyType {
		/// <summary>
		/// The value has integer type.
		/// </summary>
		Integer,
		/// <summary>
		/// The value has floating point type.
		/// </summary>
		FloatingPoint,
		/// <summary>
		/// The value has string type.
		/// </summary>
		String,
		/// <summary>
		/// The value is a date and time.
		/// </summary>
		DateTime,
		/// <summary>
		/// The value is a Globally Unique ID.
		/// </summary>
		Guid,
		/// <summary>
		/// The value is a complex object that will be stored as a JSON string representation.
		/// </summary>
		Json
	}

}
