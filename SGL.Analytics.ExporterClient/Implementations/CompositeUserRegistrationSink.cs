using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient.Implementations {
	/// <summary>
	/// Provides an <see cref="IUserRegistrationSink"/> implementation that delegates to arbitrarily many other sink objects
	/// to allow using multiple sink objects on a <see cref="SglAnalyticsExporter.GetDecryptedUserRegistrationsAsync(IUserRegistrationSink, Func{IUserRegistrationQuery, IUserRegistrationQuery}, CancellationToken)"/>
	/// call, that should all receive the decrypted data.
	/// The <see cref="IUserRegistrationSink.ProcessUserRegistrationAsync(UserRegistrationData, CancellationToken)"/>
	/// methods on constituent sinks can be invoked sequentially or concurrently, controlled by <see cref="RunConcurrently"/>.
	/// </summary>
	public class CompositeUserRegistrationSink : IUserRegistrationSink {
		/// <summary>
		/// Constructs a composite sink with the list of constituent sinks given in <paramref name="innerSinks"/>.
		/// </summary>
		/// <param name="innerSinks">The list of sinks to which calls shall be delegated.</param>
		/// <param name="runConcurrently">True if the sink shall invoke the <paramref name="innerSinks"/> concurrently on the thread pool.
		/// False if they shall be invoked sequentially.</param>
		public CompositeUserRegistrationSink(IList<IUserRegistrationSink> innerSinks, bool runConcurrently = false) {
			InnerSinks = innerSinks;
			RunConcurrently = runConcurrently;
		}

		/// <summary>
		/// True if the sink invokes the <see cref="InnerSinks"/> concurrently on the thread pool.
		/// False if they are invoked sequentially.
		/// </summary>
		public bool RunConcurrently { get; }
		/// <summary>
		/// The list of sinks to which calls shall be delegated.
		/// </summary>
		public IList<IUserRegistrationSink> InnerSinks { get; }

		/// <summary>
		/// Implements <see cref="IUserRegistrationSink.ProcessUserRegistrationAsync(UserRegistrationData, CancellationToken)"/> 
		/// by delegating each call to all sinks in <see cref="InnerSinks"/>.
		/// </summary>
		public async Task ProcessUserRegistrationAsync(UserRegistrationData userRegistrationData, CancellationToken ct) {
			if (RunConcurrently) {
				await Task.WhenAll(InnerSinks.Select(innerSink =>
					Task.Run(() => innerSink.ProcessUserRegistrationAsync(userRegistrationData, ct), ct)));
			}
			else {
				foreach (var innerSink in InnerSinks) {
					await innerSink.ProcessUserRegistrationAsync(userRegistrationData, ct);
				}
			}
		}
	}
}
