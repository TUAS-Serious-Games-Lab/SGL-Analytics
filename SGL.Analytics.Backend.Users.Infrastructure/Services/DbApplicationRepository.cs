using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using SGL.Utilities.Backend.Applications;
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

		protected override IQueryable<ApplicationWithUserProperties> OnPrepareQuery(IQueryable<ApplicationWithUserProperties> query, ApplicationQueryOptions? options) {
			if (options?.FetchUserProperties ?? true) {
				query = query.Include(a => a.UserProperties);
			}
			if (options?.FetchRecipients ?? false) {
				query = query.Include(a => a.DataRecipients);
			}
			return query;
		}
	}
}
