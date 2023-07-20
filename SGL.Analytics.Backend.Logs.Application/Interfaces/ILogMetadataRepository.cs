using SGL.Analytics.Backend.Domain.Entity;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Interfaces {
	public class LogMetadataQueryOptions {
		public bool FetchRecipientKeys { get; set; } = false;
		public KeyId? FetchRecipientKey { get; set; } = null;
		public int Limit { get; set; } = 0;
		public int Offset { get; set; } = 0;
		public LogMetadataQuerySortCriteria Ordering { get; set; } = LogMetadataQuerySortCriteria.Unordered;
		public bool ForUpdating { get; set; } = false;
	}

	public enum LogMetadataQuerySortCriteria {
		Unordered,
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
		/// <param name="queryOptions">A class that encapsulates options for querying methods, e.g. whether related entities should be fetched.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the following result: The log metadata object if the log exists, or <see langword="null"/> otherwise.</returns>
		Task<LogMetadata?> GetLogMetadataByIdAsync(Guid logId, LogMetadataQueryOptions? queryOptions = null, CancellationToken ct = default);
		Task<IEnumerable<LogMetadata>> GetLogMetadataByIdsAsync(IReadOnlyCollection<Guid> logIds, LogMetadataQueryOptions? queryOptions = null, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously obtains the log metadata entry using the given user-local id for the given user if the log exists.
		/// </summary>
		/// <param name="userAppId">The application of the user and the log.</param>
		/// <param name="userId">The user id of the relevant user.</param>
		/// <param name="localLogId">The user-local id of the log as provided by the client.</param>
		/// <param name="queryOptions">A class that encapsulates options for querying methods, e.g. whether related entities should be fetched.</param>
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

		Task<IList<LogMetadata>> UpdateLogMetadataAsync(IList<LogMetadata> logMetadata, CancellationToken ct = default);

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
