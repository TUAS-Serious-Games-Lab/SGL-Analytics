using SGL.Analytics.Backend.Domain.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Interfaces {
	public interface IApplicationRepository {
		Task<ApplicationWithUserProperties?> GetApplicationByNameAsync(string appName);
		Task<ApplicationWithUserProperties> AddApplicationAsync(ApplicationWithUserProperties app);
	}
}
