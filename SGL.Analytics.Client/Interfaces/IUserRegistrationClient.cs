using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	public interface IUserRegistrationClient {
		Task<UserRegistrationResultDTO> RegisterUserAsync(UserRegistrationDTO userDTO, string appAPIToken);
	}
}
