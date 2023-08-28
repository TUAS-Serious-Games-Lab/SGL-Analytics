using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient.Implementations {
	/// <summary>
	/// A basic implementation of <see cref="ILogFileSink"/> that writes the data as JSON files into a given directory.
	/// For each log file, two JSON files are written, one for the metadata and one for the content.
	/// </summary>
	public class SimpleDirectoryLogFileSink : ILogFileSink {
		/// <summary>
		/// The directory path where to write the files.
		/// </summary>
		public string DirectoryPath { get; set; } = "./analytics-logs";
		/// <summary>
		/// The prefix string to put in front of the log id when generating the file name for a log content file.
		/// </summary>
		public string ContentPrefix { get; set; } = "";
		/// <summary>
		/// The prefix string to put after the log id when generating the file name for a log content file.
		/// </summary>
		public string ContentSuffix { get; set; } = ".log.json";
		/// <summary>
		/// The prefix string to put in front of the log id when generating the file name for a log metadata file.
		/// </summary>
		public string MetadataPrefix { get; set; } = "";
		/// <summary>
		/// The prefix string to put after the log id when generating the file name for a log metadata file.
		/// </summary>
		public string MetadataSuffix { get; set; } = ".meta.json";
		/// <summary>
		/// The options for writing the metadata JSON files.
		/// Content files are stored as-is, i.e. as they were written by the client before uploading.
		/// Hence, these options don't apply there.
		/// </summary>
		public JsonSerializerOptions MetadataJsonOptions { get; set; } = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

		/// <summary>
		/// Writes the given <paramref name="metadata"/> and <paramref name="content"/> into separate files under <see cref="DirectoryPath"/>.
		/// If <paramref name="content"/> is null because it couldn't be decrypted, no file is written for it.
		/// </summary>
		public async Task ProcessLogFileAsync(LogFileMetadata metadata, Stream? content, CancellationToken ct) {
			var contentTask = content != null ? Task.Run(() => WriteContentFile(metadata.LogFileId, content, ct), ct) : Task.CompletedTask;
			var metadataTask = Task.Run(() => WriteMetadataFile(metadata, ct), ct);
			await contentTask.ConfigureAwait(false);
			await metadataTask.ConfigureAwait(false);
		}

		private async Task WriteContentFile(Guid id, Stream content, CancellationToken ct) {
			var fileName = $"{ContentPrefix}{id:D}{ContentSuffix}";
			string filePath = Path.Combine(DirectoryPath, fileName);
			var dir = Path.GetDirectoryName(filePath);
			if (dir != null) Directory.CreateDirectory(dir);
			using var outputFile = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
			await content.CopyToAsync(outputFile, ct).ConfigureAwait(false);
		}

		private async Task WriteMetadataFile(LogFileMetadata metadata, CancellationToken ct) {
			var fileName = $"{MetadataPrefix}{metadata.LogFileId:D}{MetadataSuffix}";
			var filePath = Path.Combine(DirectoryPath, fileName);
			var dir = Path.GetDirectoryName(filePath);
			if (dir != null) Directory.CreateDirectory(dir);
			using var outputFile = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
			await JsonSerializer.SerializeAsync(outputFile, metadata, MetadataJsonOptions, ct).ConfigureAwait(false);
		}
	}
}
