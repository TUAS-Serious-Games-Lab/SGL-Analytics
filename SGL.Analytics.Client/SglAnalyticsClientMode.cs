using System;
using System.Collections.Generic;
using System.Text;

namespace SGL.Analytics.Client {
	/// <summary>
	/// Defined the possible modes of operation of a <see cref="SglAnalytics"/> client object.
	/// </summary>
	public enum SglAnalyticsClientMode {
		/// <summary>
		/// The client is newly created and no mode was chosen yet.
		/// </summary>
		Uninitialized = 0,
		/// <summary>
		/// The client object was already disposed of and is no longer usable.
		/// </summary>
		Disposed = 1,
		/// <summary>
		/// The client object was deactivated and will not collect data, making Record* methods a no-op.
		/// </summary>
		Deactivated = 0x10,
		/// <summary>
		/// The client operates in anonymous offline mode.
		/// </summary>
		AnonymousOffline = 0x11,
		/// <summary>
		/// The client uses a stored device token and operates in offline mode.
		/// </summary>
		DeviceTokenOffline = 0x20,
		/// <summary>
		/// The client uses a stored device token and operates in online mode.
		/// </summary>
		DeviceTokenOnline = 0x21,
		/// <summary>
		/// The client uses saved username + password credentials and operates in offline mode.
		/// </summary>
		UsernamePasswordOffline = 0x30,
		/// <summary>
		/// The client uses username + password authentication and operates in online mode.
		/// </summary>
		UsernamePasswordOnline = 0x31,
		/// <summary>
		/// The client uses a user id from an upstream system from delegated authentication and operates in offline mode.
		/// </summary>
		DelegatedOffline = 0x40,
		/// <summary>
		/// The client uses delegated authentication and operates in offline mode.
		/// </summary>
		DelegatedOnline = 0x41,
	}
}
