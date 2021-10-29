using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Utilities {

	/// <summary>
	/// A simple wrapper class to hold a result value for a service in the DI container.
	/// To identify the service for which the value is held, use <see cref="ServiceResultWrapper{TService, TValue}"/> (derived from this) to add the service class to the type signature.
	/// This allows the the DI container to inject the right wrapper into each service if multiple services need a result wrapped.
	/// </summary>
	/// <typeparam name="TValue">The type of the value to hold.</typeparam>
	public class ServiceResultWrapper<TValue> {
		/// <summary>
		/// The wrapped value.
		/// </summary>
		public TValue Result { get; set; }

		/// <summary>
		/// Initializes the wrapper with the given initial value.
		/// </summary>
		/// <param name="initial">The initial value for the result.</param>
		protected ServiceResultWrapper(TValue initial) {
			Result = initial;
		}
	}

	/// <summary>
	/// A service-specifically typed version of <see cref="ServiceResultWrapper{TValue}"/>.
	/// The added type parameter enables association to a specific service type.
	/// This allows the the DI container to inject the right result wrapper into each service if multiple services need a result wrapped.
	/// </summary>
	/// <typeparam name="TService">The service class for which this wrapper should contain the result.</typeparam>
	/// <typeparam name="TValue">The type of the value to hold.</typeparam>
	public class ServiceResultWrapper<TService, TValue> : ServiceResultWrapper<TValue> {
		/// <inheritdoc/>
		public ServiceResultWrapper(TValue result) : base(result) { }
	}
}
