﻿using SGL.Analytics.Backend.Domain.Entity;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Interfaces {
	/// <summary>
	/// Specifies the interface for a repository to store <see cref="LogMetadata"/> objects for analytics logs.
	/// </summary>
	public interface ILogMetadataRepository {
		/// <summary>
		/// Asynchronously obtains the log metadata entry with the given id if it exists.
		/// </summary>
		/// <param name="logId">The unique id of the log.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the following result: The log metadata object if the log exists, or <see langword="null"/> otherwise.</returns>
		Task<LogMetadata?> GetLogMetadataByIdAsync(Guid logId, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously obtains the log metadata entry using the given user-local id for the given user if the log exists.
		/// </summary>
		/// <param name="userAppId">The application of the user and the log.</param>
		/// <param name="userId">The user id of the relevant user.</param>
		/// <param name="localLogId">The user-local id of the log as provided by the client.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the following result: The log metadata object if the log exists, or <see langword="null"/> otherwise.</returns>
		Task<LogMetadata?> GetLogMetadataByUserLocalIdAsync(Guid userAppId, Guid userId, Guid localLogId, CancellationToken ct = default);
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
