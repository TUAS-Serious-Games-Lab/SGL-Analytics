using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Interfaces {
	public class ApplicationDoesNotExistException : Exception {
		public ApplicationDoesNotExistException(string appName, Exception? innerException = null) : base($"The given application with name '{appName}' does not exist.", innerException) {
			AppName = appName;
		}

		public string AppName { get; set; }
	}

	public interface IApplicationRepository {
		Task<Domain.Entity.Application?> GetApplicationByNameAsync(string appName);
		Task<Domain.Entity.Application> AddApplicationAsync(Domain.Entity.Application app);
	}
}
