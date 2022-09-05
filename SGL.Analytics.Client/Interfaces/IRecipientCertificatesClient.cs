using SGL.Utilities.Crypto.Certificates;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	/// <summary>
	/// A base interface for all client components that provide a list of associated authorized data recipients to allow common code for working with these lists.
	/// </summary>
	public interface IRecipientCertificatesClient {
		/// <summary>
		/// Asynchronously obtains the certificates for the authorized recipients from the backend and adds them to <paramref name="targetCertificateStore"/>.
		/// Note that addition methods of <see cref="CertificateStore"/> filter out certificates that don't pass validation.
		/// </summary>
		/// <param name="appName">The technical name of the application used to identify it in the backend.</param>
		/// <param name="appAPIToken">The API token for the application to authenticate it with the backend.</param>
		/// <param name="targetCertificateStore">The certificate store to which the downloaded certificate shall be added.</param>
		/// <returns>A task representing the operation.</returns>
		Task LoadRecipientCertificatesAsync(string appName, string appAPIToken, CertificateStore targetCertificateStore);
	}
}
