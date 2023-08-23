using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Specifies the data transferred from the server to the client after a successful user registration.
	/// </summary>
	public class UserRegistrationResultDTO {
		/// <summary>
		/// The id that the backend assigned to the newly registered user.
		/// </summary>
		public Guid UserId { get; private set; }

		/// <summary>
		/// Creates a new DTO with the given data.
		/// </summary>
		public UserRegistrationResultDTO(Guid userId) {
			UserId = userId;
		}
	}
}
