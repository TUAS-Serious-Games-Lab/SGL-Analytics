using SGL.Analytics.Backend.Domain.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Interfaces {
	public class ApplicationDoesNotExistException : Exception {
		public ApplicationDoesNotExistException(string appName, Exception? innerException = null) : base($"The given application with name '{appName}' does not exist.", innerException) {
			AppName = appName;
		}

		public string AppName { get; set; }
	}

	public interface IApplicationRepository {
		Task<ApplicationWithUserProperties?> GetApplicationByNameAsync(string appName);
		Task<ApplicationWithUserProperties> AddApplicationAsync(ApplicationWithUserProperties app);
	}
}
