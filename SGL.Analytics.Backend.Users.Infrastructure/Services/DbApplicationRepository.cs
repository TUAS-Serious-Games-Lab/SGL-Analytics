using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using SGL.Utilities.Backend.Applications;
using SGL.Utilities.Crypto.Keys;
using System.Linq;

namespace SGL.Analytics.Backend.Users.Infrastructure.Services {
	/// <summary>
	/// Adapts <see cref="DbApplicationRepository{TApp, TQueryOptions, TContext}"/> for the log collector backend
	/// to implement <see cref="IApplicationRepository{TApp, TQueryOptions}"/> for <see cref="ApplicationWithUserProperties"/>.
	/// </summary>
	public class DbApplicationRepository : DbApplicationRepository<ApplicationWithUserProperties, ApplicationQueryOptions, UsersContext> {

		/// <summary>
		/// Creates a repository object using the given database context object for data access.
		/// </summary>
		/// <param name="context">The <see cref="DbContext"/> implementation for the database.</param>
		public DbApplicationRepository(UsersContext context) : base(context) { }

		/// <summary>
		/// Includes populating the foreign key collection to <paramref name="query"/> where instructed to do so by <paramref name="options"/>.
		/// If more than one of them is added, make the query a split query.
		/// </summary>
		protected override IQueryable<ApplicationWithUserProperties> OnPrepareQuery(IQueryable<ApplicationWithUserProperties> query, ApplicationQueryOptions? options) {
			int includeCounter = 0;
			if (options?.FetchUserProperties ?? true) {
				query = query.Include(a => a.UserProperties);
				includeCounter++;
			}
			if (options?.FetchRecipients ?? false) {
				query = query.Include(a => a.DataRecipients);
				includeCounter++;
			}
			if (options?.FetchExporterCertificates ?? false) {
				query = query.Include(a => a.AuthorizedExporters);
				includeCounter++;
			}
			else if (options?.FetchExporterCertificate != null) {
				KeyId idToFetch = options.FetchExporterCertificate;
				query = query.Include(a => a.AuthorizedExporters.Where(e => e.PublicKeyId == idToFetch));
			}
			if (includeCounter > 1) {
				query = query.AsSplitQuery();
			}
			return query;
		}
	}
}
