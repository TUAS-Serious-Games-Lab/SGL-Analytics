using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities.Logging.FileLogging {
	public class FileLoggingSink : IDisposable, IAsyncDisposable {
		private string baseDirectory = Path.Combine(Environment.CurrentDirectory, "log");
		private List<SinkPathComponent> subdirectoryComponents = new();
		private List<SinkPathComponent> filenameComponents = new();
		private string filenameSuffix = ".log";
		private Action<LogMessage, StringBuilder> entryFormatter = LogMessageFormatters.Default;
		private bool timeBased;

		public FileLoggingSink(FileLoggingSinkOptions options) {
			baseDirectory = options.BaseDirectory;
			subdirectoryComponents = options.SubdirectoryComponents.Select(c => SinkPathComponent.FromNameOrLiteral(c)).ToList();
			filenameComponents = options.FilenameComponents.Select(c => SinkPathComponent.FromNameOrLiteral(c)).ToList();
			filenameSuffix = options.FilenameSuffix;
			timeBased = subdirectoryComponents.Any(c => c.TimeBased) || filenameComponents.Any(c => c.TimeBased);
			if (timeBased) {
				timeBasedWriters = new();
			}
			else {
				normalWriters = new();
			}
		}

		private Dictionary<string, (string Path, StreamWriter Writer)>? timeBasedWriters;
		private Dictionary<string, StreamWriter>? normalWriters;
		private StringBuilder stringBuilder = new();

		private async Task<StreamWriter> getWriterAsync(LogMessage msg) {
			var dir = Path.Combine(baseDirectory, string.Join(Path.DirectorySeparatorChar, subdirectoryComponents.Select(c => c.GetDirName(msg))));
			var path = Path.Combine(dir, string.Concat(filenameComponents.Select(c => c.GetFileName(msg)).Append(filenameSuffix)));
			if (timeBased) {
				var timeIndependentPathSlug = Path.Combine(
					string.Join(Path.DirectorySeparatorChar, subdirectoryComponents.Select(c => c.TimeBased ? "<TIME>" : c.GetDirName(msg))),
					string.Concat(filenameComponents.Select(c => c.TimeBased ? "<TIME>" : c.GetFileName(msg))));
				if (timeBasedWriters!.TryGetValue(timeIndependentPathSlug, out var writerEntry)) {
					if (path == writerEntry.Path) {
						return writerEntry.Writer;
					}
					else {
						Directory.CreateDirectory(dir);
						var writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true));
						await writerEntry.Writer.FlushAsync();
						await writerEntry.Writer.DisposeAsync();
						timeBasedWriters[timeIndependentPathSlug] = (path, writer);
						return writer;
					}
				}
				else {
					Directory.CreateDirectory(dir);
					var writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true));
					timeBasedWriters[timeIndependentPathSlug] = (path, writer);
					return writer;
				}
			}
			else {
				if (!normalWriters!.TryGetValue(path, out var writer)) {
					Directory.CreateDirectory(dir);
					writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true));
					normalWriters.Add(path, writer);
					return writer;
				}
				else return writer;
			}
		}

		public async Task WriteAsync(LogMessage msg) {
			var writer = await getWriterAsync(msg);
			stringBuilder.Clear();
			entryFormatter(msg, stringBuilder);
			await writer.WriteLineAsync(stringBuilder);
		}

		public void Dispose() {
			foreach (var writer in timeBased ? timeBasedWriters!.Values.Select(w => w.Writer) : normalWriters!.Values) {
				writer.Dispose();
			}
		}

		public async ValueTask DisposeAsync() {
			foreach (var writer in timeBased ? timeBasedWriters!.Values.Select(w => w.Writer) : normalWriters!.Values) {
				await writer.DisposeAsync();
			}
		}
	}
}
