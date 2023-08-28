using SGL.Analytics.Backend.Domain.Entity;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Interfaces {
	/// <summary>
	/// Encapsulates options for queries on <see cref="ILogMetadataRepository"/>.
	/// </summary>
	public class LogMetadataQueryOptions {
		/// <summary>
		/// If true, indicates that all recipient data keys for each log metadata shall be fetched.
		/// </summary>
		public bool FetchRecipientKeys { get; set; } = false;
		/// <summary>
		/// If set, indicates that recipient data keys for the given recipient key id shall be fetched for each log metadata.
		/// </summary>
		public KeyId? FetchRecipientKey { get; set; } = null;
		/// <summary>
		/// If set, limits the number of results to return.
		/// </summary>
		public int Limit { get; set; } = 0;
		/// <summary>
		/// If set, indicates that the given number of results shall be skipped at the start.
		/// </summary>
		public int Offset { get; set; } = 0;
		/// <summary>
		/// Indicates the sorting order for the results.
		/// </summary>
		public LogMetadataQuerySortCriteria Ordering { get; set; } = LogMetadataQuerySortCriteria.Unordered;
		/// <summary>
		/// If true, indicates that the objects are fetched for updating and
		/// need to be tracked internally to support calling an update method on them to save changes.
		/// </summary>
		public bool ForUpdating { get; set; } = false;
	}

	/// <summary>
	/// Describes sorting criteria for query results in <see cref="ILogMetadataRepository"/>.
	/// </summary>
	public enum LogMetadataQuerySortCriteria {
		/// <summary>
		/// Specify no ordering criteria, the ordering is implementation-defined or unspecified.
		/// </summary>
		Unordered,
		/// <summary>
		/// Order the results first by user id and within the same user id, order logs by creation time.
		/// </summary>
		UserIdThenCreateTime
	}

	/// <summary>
	/// Specifies the interface for a repository to store <see cref="LogMetadata"/> objects for analytics logs.
	/// </summary>
	public interface ILogMetadataRepository {
		/// <summary>
		/// Asynchronously obtains the log metadata entry with the given id if it exists.
		/// </summary>
		/// <param name="logId">The unique id of the log.</param>
		/// <param name="queryOptions">An object that encapsulates options for querying methods, e.g. whether related entities should be fetched.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the following result: The log metadata object if the log exists, or <see langword="null"/> otherwise.</returns>
		Task<LogMetadata?> GetLogMetadataByIdAsync(Guid logId, LogMetadataQueryOptions? queryOptions = null, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously obtains the log metadata entries for multiple given ids.
		/// </summary>
		/// <param name="logIds">The unique ids of the logs.</param>
		/// <param name="queryOptions">An object that encapsulates options for querying methods, e.g. whether related entities should be fetched.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the metadata entries found with the given ids.</returns>
		Task<IEnumerable<LogMetadata>> GetLogMetadataByIdsAsync(IReadOnlyCollection<Guid> logIds, LogMetadataQueryOptions? queryOptions = null, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously obtains the log metadata entry using the given user-local id for the given user if the log exists.
		/// </summary>
		/// <param name="userAppId">The application of the user and the log.</param>
		/// <param name="userId">The user id of the relevant user.</param>
		/// <param name="localLogId">The user-local id of the log as provided by the client.</param>
		/// <param name="queryOptions">An object that encapsulates options for querying methods, e.g. whether related entities should be fetched.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the following result: The log metadata object if the log exists, or <see langword="null"/> otherwise.</returns>
		Task<LogMetadata?> GetLogMetadataByUserLocalIdAsync(Guid userAppId, Guid userId, Guid localLogId, LogMetadataQueryOptions? queryOptions = null, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously creates the given log metadata entry in the repository.
		/// </summary>
		/// <param name="logMetadata">The application data for the log metadata entry to create.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the created object as its result.</returns>
		Task<LogMetadata> AddLogMetadataAsync(LogMetadata logMetadata, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously updates the given log metadata entry in the repository.
		/// </summary>
		/// <param name="logMetadata">The updated log metadata.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the updated object as its result.</returns>
		Task<LogMetadata> UpdateLogMetadataAsync(LogMetadata logMetadata, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously save updates that were made to the given <see cref="LogMetadata"/> objects.
		/// </summary>
		/// <param name="logMetadata">The objects to update.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the updated objects as its result.</returns>
		Task<IList<LogMetadata>> UpdateLogMetadataAsync(IList<LogMetadata> logMetadata, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously obtains log metadata entries for a given application.
		/// </summary>
		/// <param name="appId">The id of the application for which to fetch the entries.</param>
		/// <param name="completenessFilter">
		/// If null, fetches all log metadata entries.
		/// If false, fetches only incomplete entries.
		/// If true, fetches only complete entries.
		/// </param>
		/// <param name="notForKeyId">
		/// If set, only fetches entries that don't have a data key for the recipient key-pair with the given key id.
		/// </param>
		/// <param name="queryOptions">An object that encapsulates options for querying methods, e.g. whether related entities should be fetched.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the fetched objects as its result.</returns>
		Task<IEnumerable<LogMetadata>> ListLogMetadataForApp(Guid appId, bool? completenessFilter = null, KeyId? notForKeyId = null, LogMetadataQueryOptions? queryOptions = null, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously obtains the per-application counts of the log files in the database.
		/// </summary>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing an application name -> log count dictionary as its result.</returns>
		Task<IDictionary<string, int>> GetLogsCountPerAppAsync(CancellationToken ct = default);
		/// <summary>
		/// Asynchronously obtains the per-application averages of the sizes of the log files in the database.
		/// </summary>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing an application name -> average size dictionary as its result.</returns>
		Task<IDictionary<string, double>> GetLogSizeAvgPerAppAsync(CancellationToken ct = default);
	}
}
