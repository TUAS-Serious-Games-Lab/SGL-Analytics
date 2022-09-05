using System;
using System.Collections.Generic;
using System.Text;

namespace SGL.Analytics.Client {
	public class MissingSglAnalyticsConfigurationException : Exception {
		public string ConfigurationMethod { get; }

		public MissingSglAnalyticsConfigurationException(string configurationMethod) :
			base($"The {nameof(SglAnalytics)} instance is missing some required configuration. " +
				$"Call {configurationMethod} (or extension methods that use it) in the configurator delegate passed to the constructor.") {
			ConfigurationMethod = configurationMethod;
		}
	}
}
