using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Infrastructure.Services {
	/// <summary>
	/// Adapts <see cref="Utilities.Backend.Applications.DbApplicationRepository{TApp, TQueryOptions, TContext}"/> for the log collector backend
	/// to implement <see cref="Utilities.Backend.Applications.IApplicationRepository{TApp, TQueryOptions}"/> for <see cref="ApplicationWithUserProperties"/>.
	/// </summary>
	public class DbApplicationRepository : Utilities.Backend.Applications.DbApplicationRepository<ApplicationWithUserProperties, ApplicationQueryOptions, UsersContext> {

		/// <summary>
		/// Creates a repository object using the given database context object for data access.
		/// </summary>
		/// <param name="context">The <see cref="DbContext"/> implementation for the database.</param>
		public DbApplicationRepository(UsersContext context) : base(context) { }

		protected override IQueryable<ApplicationWithUserProperties> OnPrepareQuery(IQueryable<ApplicationWithUserProperties> query, ApplicationQueryOptions? options) {
			query = query.Include(a => a.UserProperties);
			if (options?.FetchRecipients ?? false) {
				query = query.Include(a => a.DataRecipients);
			}
			return query;
		}
	}
}
