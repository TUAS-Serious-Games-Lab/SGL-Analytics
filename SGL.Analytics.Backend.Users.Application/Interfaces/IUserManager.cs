﻿using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Interfaces {
	public interface IUserManager {
		Task<UserRegistrationResultDTO> RegisterUserAsync(UserRegistrationDTO userRegistration);
	}
}