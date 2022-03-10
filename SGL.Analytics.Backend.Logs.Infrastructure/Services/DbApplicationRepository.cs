using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Services {
	/// <summary>
	/// Adapts <see cref="Utilities.Backend.Applications.DbApplicationRepository{TApp, TQueryOptions, TContext}"/> for the log collector backend
	/// to implement <see cref="Utilities.Backend.Applications.IApplicationRepository{TApp, TQueryOptions}"/> for <see cref="Domain.Entity.Application"/>.
	/// </summary>
	public class DbApplicationRepository : Utilities.Backend.Applications.DbApplicationRepository<Domain.Entity.Application, ApplicationQueryOptions, LogsContext> {
		/// <summary>
		/// Creates a repository object using the given database context object for data access.
		/// </summary>
		/// <param name="context">The <see cref="DbContext"/> implementation for the database.</param>
		public DbApplicationRepository(LogsContext context) : base(context) { }

		protected override IQueryable<Domain.Entity.Application> OnPrepareQuery(IQueryable<Domain.Entity.Application> query, ApplicationQueryOptions? options) {
			if (options?.FetchRecipients ?? false) {
				query = query.Include(a => a.DataRecipients);
			}
			return query;
		}
	}
}
