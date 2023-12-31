using SGL.Utilities.Backend.Applications;
using System;
using System.Collections.Generic;

namespace SGL.Analytics.Backend.Domain.Entity {
	/// <summary>
	/// Represents a registered application that uses SGL Analytics.
	/// </summary>
	public class Application : IApplication {
		/// <summary>
		/// A unique id of the application in the database.
		/// This is usually different between services, as they use different databases.
		/// </summary>
		public Guid Id { get; set; }
		/// <summary>
		/// A unique technical name of the application that the client uses to identify the application when communicating with the backend services.
		/// </summary>
		public string Name { get; set; }
		/// <summary>
		/// An API token that a client claiming to be working for the application has to include in its request to authenticate the application.
		/// </summary>
		public string ApiToken { get; set; }

		/// <summary>
		/// The collection of the recipients that are authorized to receive the collected data.
		/// This is used by clients to determine for whom to encrypt data keys for the files under end-to-end encryption.
		/// </summary>
		public ICollection<Recipient> DataRecipients { get; set; } = null!;

		/// <summary>
		/// Constructs an <see cref="Application"/> object with the given data values.
		/// </summary>
		public Application(Guid id, string name, string apiToken) {
			Id = id;
			Name = name;
			ApiToken = apiToken;
		}

		/// <summary>
		/// Creates an <see cref="Application"/> object with the given data values, generating a new id.
		/// Note: This only creates the application in memory. For persistence, an application repository needs to be used.
		/// </summary>
		/// <returns>The created object.</returns>
		public static Application Create(string name, string apiToken) {
			var app = new Application(Guid.NewGuid(), name, apiToken);
			app.DataRecipients = new List<Recipient>();
			return app;
		}

		/// <summary>
		/// Adds a certificate for a recipient key pair to the list of authorized recipients for this application.
		/// </summary>
		/// <param name="label">A descriptive label as a human-readable identifier for the key.</param>
		/// <param name="certificatePem">The certificate for the key pair, encoded in PEM format.</param>
		/// <returns>The created certificate entry object.</returns>
		public Recipient AddRecipient(string label, string certificatePem) {
			var recipient = Recipient.Create(this, label, certificatePem);
			DataRecipients.Add(recipient);
			return recipient;
		}
	}
}
