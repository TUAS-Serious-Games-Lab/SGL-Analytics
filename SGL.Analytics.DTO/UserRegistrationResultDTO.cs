using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Specifies the data transferred from the server to the client after a successful user registration.
	/// </summary>
	public record UserRegistrationResultDTO(Guid UserId);
}
