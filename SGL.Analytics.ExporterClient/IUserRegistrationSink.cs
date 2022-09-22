using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	public interface IUserRegistrationSink {
		Task ProcessUserRegistrationAsync(UserRegistrationData userRegistrationData, CancellationToken ct);
	}
}
