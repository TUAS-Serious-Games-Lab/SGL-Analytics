using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient.Implementations {
	public class SimpleDirectoryLogFileSink : ILogFileSink {
		public string DirectoryPath { get; set; } = "./analytics-logs";
		public string ContentPrefix { get; set; } = "";
		public string ContentSuffix { get; set; } = ".log.json";
		public string MetadataPrefix { get; set; } = "";
		public string MetadataSuffix { get; set; } = ".meta.json";
		public JsonSerializerOptions MetadataJsonOptions { get; set; } = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

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
