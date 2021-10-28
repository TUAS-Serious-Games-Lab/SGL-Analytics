using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Specifies the data transferred from the client to the server when the client attempts to login a user.
	/// </summary>
	public record LoginRequestDTO(string AppName, string AppApiToken, Guid UserId, string UserSecret);
}
