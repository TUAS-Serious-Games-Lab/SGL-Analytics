using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace SGL.Analytics.TestUtilities {

	public class XUnitLoggingProvider : ILoggerProvider {
		private Func<ITestOutputHelper?> outputObtainer;
		private static ThreadLocal<StringBuilder> cachedStringBuilder = new(() => new StringBuilder());

		public class XUnitLogger : ILogger {
			private Func<ITestOutputHelper?> outputObtainer;
			private string categoryName;
			private LoggerExternalScopeProvider scopes = new LoggerExternalScopeProvider();

			public XUnitLogger(Func<ITestOutputHelper?> outputObtainer, string categoryName) {
				this.outputObtainer = outputObtainer;
				this.categoryName = categoryName;
			}

			public IDisposable BeginScope<TState>(TState state) {
				return scopes.Push(state);
			}

			public bool IsEnabled(LogLevel logLevel) {
				return logLevel < LogLevel.None;
			}

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
				var output = outputObtainer();
				if (output is null) return;
				StringBuilder builder = cachedStringBuilder.Value ??= new StringBuilder();
				builder.Clear();
				builder.AppendFormat("{0} [{1}] {2}", logLevel.ToString(), categoryName, formatter(state, exception));
				if (exception != null) {
					builder.Append(" Exception: ");
					builder.Append(exception);
				}
				builder.Append(" ");
				scopes.ForEachScope((scope, sb) => sb.AppendFormat("<{0}>", scope), builder);
				var str = builder.ToString();
				lock (output) {
					output.WriteLine(str);
				}
			}
		}

		public XUnitLoggingProvider(Func<ITestOutputHelper?> outputObtainer) {
			this.outputObtainer = outputObtainer;
		}

		public ILogger CreateLogger(string categoryName) {
			return new XUnitLogger(outputObtainer, categoryName);
		}

		public void Dispose() { }
	}

	public static class XUnitLoggingExtensions {
		public static ILoggingBuilder AddXUnit(this ILoggingBuilder builder, ITestOutputHelper output) {
			builder.AddProvider(new XUnitLoggingProvider(() => output));
			return builder;
		}
		public static ILoggingBuilder AddXUnit(this ILoggingBuilder builder, Func<ITestOutputHelper?> outputObtainer) {
			builder.AddProvider(new XUnitLoggingProvider(outputObtainer));
			return builder;
		}
	}
}
