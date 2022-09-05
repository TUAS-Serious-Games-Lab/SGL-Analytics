using System;
using System.Collections.Generic;
using System.Text;

namespace SGL.Analytics.Client {
	/// <summary>
	/// The exception thrown when a required setting in <see cref="ISglAnalyticsConfigurator"/> was not set.
	/// </summary>
	public class MissingSglAnalyticsConfigurationException : Exception {
		/// <summary>
		/// Indicates the method on <see cref="ISglAnalyticsConfigurator"/> that needs to be invoked to set the setting value.
		/// </summary>
		public string ConfigurationMethod { get; }

		/// <summary>
		/// Constructs a new exception object with the given data.
		/// </summary>
		public MissingSglAnalyticsConfigurationException(string configurationMethod) :
			base($"The {nameof(SglAnalytics)} instance is missing some required configuration. " +
				$"Call {configurationMethod} (or extension methods that use it) in the configurator delegate passed to the constructor.") {
			ConfigurationMethod = configurationMethod;
		}
	}
}
