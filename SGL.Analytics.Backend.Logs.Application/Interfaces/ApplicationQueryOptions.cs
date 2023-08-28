using SGL.Analytics.Backend.Domain.Entity;
using SGL.Utilities.Backend.Applications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Interfaces {
	/// <summary>
	/// Encapsulates options for queries on <see cref="IApplicationRepository{Application, ApplicationQueryOptions}"/>.
	/// </summary>
	public class ApplicationQueryOptions {
		/// <summary>
		/// If true, indicates to fetch associated recipient key entries for each fetched application.
		/// </summary>
		public bool FetchRecipients { get; set; } = false;
	}
}
