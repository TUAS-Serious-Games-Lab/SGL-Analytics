using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Configuration;

namespace SGL.Analytics.Utilities.Logging.FileLogging {
	public static class FileLoggingExtensions {
		public static ILoggingBuilder AddFile(this ILoggingBuilder builder) {
			return builder.AddFile(builder => { });
		}

		public static ILoggingBuilder AddFile(this ILoggingBuilder builder, Action<IFileLoggingProviderBuilder> providerBuilder) {
			builder.AddConfiguration();
			builder.Services.AddSingleton(providerBuilder);
			builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, FileLoggingProvider>());
			LoggerProviderOptions.RegisterProviderOptions<FileLoggingProviderOptions, FileLoggingProvider>(builder.Services);
			return builder;
		}
	}
}
