using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace SGL.Analytics.TestUtilities {

	/// <summary>
	/// An <see cref="ILoggerProvider"/> implementation that outputs the log messages to a <see cref="ITestOutputHelper"/> to provide log output for test cases.
	/// </summary>
	public class XUnitLoggingProvider : ILoggerProvider {
		private Func<ITestOutputHelper?> outputObtainer;
		private static ThreadLocal<StringBuilder> cachedStringBuilder = new(() => new StringBuilder());
		private LoggerExternalScopeProvider scopes = new LoggerExternalScopeProvider();

		internal class XUnitLogger : ILogger {
			private Func<ITestOutputHelper?> outputObtainer;
			private string categoryName;
			private LoggerExternalScopeProvider scopes;

			public XUnitLogger(Func<ITestOutputHelper?> outputObtainer, string categoryName, LoggerExternalScopeProvider scopes) {
				this.outputObtainer = outputObtainer;
				this.categoryName = categoryName;
				this.scopes = scopes;
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
				builder.AppendFormat("{0:G} [{1}] {2}", logLevel, categoryName, formatter(state, exception));
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

		/// <summary>
		/// Constructs a <see cref="XUnitLoggingProvider"/> that uses the given delegate to obtain the <see cref="ITestOutputHelper"/> for the current test case.
		/// </summary>
		/// <param name="outputObtainer">A delegate to get the output helper of the test case.</param>
		/// <remarks>
		/// In many cases, the delegate can simply return the appropriate dependency-injected variable,
		/// it is however necessary to use a delegate instead of just passing a reference to the output helper to support having the logging provider in a fixture object.
		/// There, the current test case needs to set a variable in the fixture to its output helper and the logging provider needs to look for the current value of that variable.
		/// </remarks>
		public XUnitLoggingProvider(Func<ITestOutputHelper?> outputObtainer) {
			this.outputObtainer = outputObtainer;
		}

		/// <inheritdoc/>
		public ILogger CreateLogger(string categoryName) {
			return new XUnitLogger(outputObtainer, categoryName, scopes);
		}

		void IDisposable.Dispose() { }
	}

	/// <summary>
	/// Provides extension methods to add the <see cref="XUnitLoggingProvider"/> to an <see cref="ILoggingBuilder"/>.
	/// </summary>
	public static class XUnitLoggingExtensions {
		/// <summary>
		/// Adds <see cref="XUnitLoggingProvider"/> to the <see cref="ILoggingBuilder"/> using a fixed <see cref="ITestOutputHelper"/>.
		/// This is suitable for simple test cases, where the logging provider doesn't outlive the test case associated with the output helper.
		/// </summary>
		/// <param name="builder">A logging framework builder to which the XUnit logging bridge provider shall be added.</param>
		/// <param name="output">The test case output to which it shall log.</param>
		/// <returns></returns>
		public static ILoggingBuilder AddXUnit(this ILoggingBuilder builder, ITestOutputHelper output) {
			builder.AddProvider(new XUnitLoggingProvider(() => output));
			return builder;
		}

		/// <summary>
		/// Adds <see cref="XUnitLoggingProvider"/> to the <see cref="ILoggingBuilder"/> using a delegate to obtain the current <see cref="ITestOutputHelper"/> to log to.
		/// This is needed, when the logging provider is placed in a fixture object that is used by multiple test cases and thus needs to be able to switch between output helpers when a new test case is started.
		/// Typically, the fixture calss should have a property / field that is set by each test case and the provided delegate should just return the current value of that variable.
		/// </summary>
		/// <param name="builder">A logging framework builder to which the XUnit logging bridge provider shall be added.</param>
		/// <param name="outputObtainer">A delegate to obtain the <see cref="ITestOutputHelper"/> of the current test case.</param>
		/// <returns></returns>
		public static ILoggingBuilder AddXUnit(this ILoggingBuilder builder, Func<ITestOutputHelper?> outputObtainer) {
			builder.AddProvider(new XUnitLoggingProvider(outputObtainer));
			return builder;
		}
	}
}
