using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace SGL.Analytics.Client.Tests {

	public class XUnitLoggingProvider : ILoggerProvider {
		private ITestOutputHelper output;
		private static ThreadLocal<StringBuilder> cachedStringBuilder = new(() => new StringBuilder());

		public class XUnitLogger : ILogger {
			private ITestOutputHelper output;
			private string categoryName;
			private LoggerExternalScopeProvider scopes = new LoggerExternalScopeProvider();

			public XUnitLogger(ITestOutputHelper output, string categoryName) {
				this.output = output;
				this.categoryName = categoryName;
			}

			public IDisposable BeginScope<TState>(TState state) {
				return scopes.Push(state);
			}

			public bool IsEnabled(LogLevel logLevel) {
				return logLevel < LogLevel.None;
			}

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
				StringBuilder builder = (cachedStringBuilder.Value ??= new StringBuilder());
				builder.Clear();
				builder.AppendFormat("{0} [{1}] {2}", logLevel.ToString(), categoryName, formatter(state, exception));
				if (exception != null) {
					builder.Append(exception);
				}
				builder.Append(" ");
				scopes.ForEachScope((scope, sb) => sb.AppendFormat("<{0}>", scopes), builder);
				var str = builder.ToString();
				lock (output) {
					output.WriteLine(str);
				}
			}
		}

		public XUnitLoggingProvider(ITestOutputHelper output) {
			this.output = output;
		}

		public ILogger CreateLogger(string categoryName) {
			return new XUnitLogger(output, categoryName);
		}

		public void Dispose() { }
	}

	public static class XUnitLoggingExtensions {
		public static ILoggingBuilder AddXUnit(this ILoggingBuilder builder, ITestOutputHelper output) {
			builder.AddProvider(new XUnitLoggingProvider(output));
			return builder;
		}
	}
}
