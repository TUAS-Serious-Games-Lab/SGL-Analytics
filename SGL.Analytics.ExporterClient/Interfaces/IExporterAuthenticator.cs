using SGL.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	public interface IExporterAuthenticator {
		Task<AuthorizationData> AuthenticateAsync(string appName, CancellationToken ct = default);
	}
}
