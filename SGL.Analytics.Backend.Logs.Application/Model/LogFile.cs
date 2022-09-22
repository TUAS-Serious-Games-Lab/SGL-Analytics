using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.DTO;
using SGL.Utilities.Crypto.EndToEnd;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Model {
	/// <summary>
	/// Models a high-level representation of an analytics log file, that bundles the metadata provided by <see cref="LogMetadata"/> with the functionality to read the file content as provided by <see cref="ILogFileRepository"/>.
	/// </summary>
	public class LogFile {
		private LogMetadata metadata;
		private ILogFileRepository fileRepo;

		/// <summary>
		/// Creates a <see cref="LogFile"/> object from the <see cref="LogMetadata"/> describing it and the <see cref="ILogFileRepository"/> to read its contents from.
		/// </summary>
		/// <param name="metadata">The underlying metadata object for the log file.</param>
		/// <param name="fileRepo">The repository providing its contents.</param>
		public LogFile(LogMetadata metadata, ILogFileRepository fileRepo) {
			this.metadata = metadata;
			this.fileRepo = fileRepo;
		}

		/// <summary>
		/// The unique id of the analytics log file.
		/// </summary>
		public Guid Id => metadata.Id;
		/// <summary>
		/// The application from which the log originates.
		/// </summary>
		public Domain.Entity.Application App => metadata.App;
		/// <summary>
		/// The id of the user that uploaded the log.
		/// </summary>
		public Guid UserId => metadata.UserId;
		/// <summary>
		/// The id of the log as orignially indicated by the client.
		/// </summary>
		/// <remarks>
		/// This is usually identical to <see cref="Id"/>, except when an id collision happens between users.
		/// While this is astronomically unlikely under normal circumstances, we still need to handle this case cleanly by assigning a new <see cref="Id"/>,
		/// because problems or user interference on the client side may lead to duplicate ids, e.g. a user copying files from one installation to another with a different user id.
		/// </remarks>
		public Guid LocalLogId => metadata.LocalLogId;
		/// <summary>
		/// The time the log was started on the client.
		/// </summary>
		public DateTime CreationTime => metadata.CreationTime;
		/// <summary>
		/// The time when the recording of the log on the client ended.
		/// </summary>
		public DateTime EndTime => metadata.EndTime;
		/// <summary>
		/// If <see cref="Complete"/> is <see langword="true"/>, the time when the upload was completed, or,
		/// if <see cref="Complete"/> is <see langword="false"/>, the time when the upload was started.
		/// </summary>
		public DateTime UploadTime => metadata.UploadTime;
		/// <summary>
		/// The suffix to use for the log file name.
		/// </summary>
		public string FilenameSuffix => metadata.FilenameSuffix;
		/// <summary>
		/// The encoding used for the contents of the log file.
		/// </summary>
		public LogContentEncoding Encoding => metadata.Encoding;

		public long? Size => metadata.Size;

		/// <summary>
		/// Indicates whether the log was uploaded completely.
		/// If this is <see langword="false"/>, it may indicate, that the upload is still running or that it was interrupted and may be reattempted.
		/// </summary>
		public bool Complete => metadata.Complete;

		public EncryptionInfo EncryptionInfo => metadata.EncryptionInfo;

		/// <summary>
		/// Asynchronously opens the log contents for reading.
		/// </summary>
		/// <param name="ct">A cancellation token to allow cancelling the opertation.</param>
		/// <returns>A task representing the operation, that contains the opened stream upon success.
		/// It is the responsibility of the caller to dispose of this stream.</returns>
		public async Task<Stream> OpenReadAsync(CancellationToken ct = default) {
			return await fileRepo.ReadLogAsync(App.Name, UserId, Id, FilenameSuffix, ct);
		}
		/// <summary>
		/// Asynchronously copies the contents of the analytics log into <paramref name="stream"/>.
		/// </summary>
		/// <param name="stream">A stream to write the copied content to.</param>
		/// <param name="ct">A cancellation token to allow cancelling the opertation.</param>
		/// <returns>A task object representing the copy operation.</returns>
		public async Task CopyToAsync(Stream stream, CancellationToken ct = default) {
			await fileRepo.CopyLogIntoAsync(App.Name, UserId, Id, FilenameSuffix, stream, ct);
		}
	}
}
