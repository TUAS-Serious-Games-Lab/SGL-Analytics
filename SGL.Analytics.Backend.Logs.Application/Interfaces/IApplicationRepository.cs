using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Interfaces {

	public interface IApplicationRepository {
		Task<Domain.Entity.Application?> GetApplicationByNameAsync(string appName, CancellationToken ct = default);
		Task<Domain.Entity.Application> AddApplicationAsync(Domain.Entity.Application app, CancellationToken ct = default);
	}
}
