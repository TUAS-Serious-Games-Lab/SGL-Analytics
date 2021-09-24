using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities.Logging.FileLogging {
	public class FileLoggingSink : IDisposable, IAsyncDisposable {
		private NamedPlaceholderFormatterFactory<LogMessage> formatterFactory;
		private NamedPlaceholderFormatterFactory<LogMessage> formatterFactoryFixedTime;

		private string baseDirectory = Path.Combine(Environment.CurrentDirectory, "log");
		private NamedPlaceholderFormatter<LogMessage> normalMessageFormatter;
		private NamedPlaceholderFormatter<LogMessage> exceptionMessageFormatter;
		private NamedPlaceholderFormatter<LogMessage> fileNameFormatter;
		private NamedPlaceholderFormatter<LogMessage>? fileNameFormatterFixedTime;
		private bool timeBased;

		public FileLoggingSink(FileLoggingSinkOptions options,
			NamedPlaceholderFormatterFactory<LogMessage> formatterFactory,
			NamedPlaceholderFormatterFactory<LogMessage> formatterFactoryFixedTime) {
			this.formatterFactory = formatterFactory;
			this.formatterFactoryFixedTime = formatterFactoryFixedTime;

			baseDirectory = options.BaseDirectory;
			normalMessageFormatter = formatterFactory.Create(options.MessageFormat);
			exceptionMessageFormatter = formatterFactory.Create(options.MessageFormatException);
			fileNameFormatter = formatterFactory.Create(options.FilenameFormat);
			timeBased = fileNameFormatter.UsesPlaceholder("Time");
			if (timeBased) {
				fileNameFormatterFixedTime = formatterFactoryFixedTime.Create(options.FilenameFormat);
				timeBasedWriters = new LRUCache<string, (string Path, StreamWriter Writer)>(options.MaxOpenStreams);
			}
			else {
				normalWriters = new LRUCache<string, StreamWriter>(options.MaxOpenStreams);
			}
		}

		private LRUCache<string, (string Path, StreamWriter Writer)>? timeBasedWriters;
		private LRUCache<string, StreamWriter>? normalWriters;
		private StringBuilder stringBuilder = new();

		private string sanitizeFilename(string filename) => new string(filename.Select(c => c switch {
			'.' => c,
			'-' => c,
			'(' => c,
			')' => c,
			'[' => c,
			']' => c,
			_ when c == Path.DirectorySeparatorChar => c,
			_ when c == Path.AltDirectorySeparatorChar => c,
			_ when char.IsLetterOrDigit(c) => c,
			_ => '_'
		}).ToArray());

		private async Task<StreamWriter> getWriterAsync(LogMessage msg) {
			stringBuilder.Clear();
			var filename = sanitizeFilename(fileNameFormatter.AppendFormattedTo(stringBuilder, msg).ToString());
			var path = Path.Combine(baseDirectory, filename);
			var dir = Path.GetDirectoryName(path);
			if (timeBased) {
				stringBuilder.Clear();
				var timeIndependentFilenameSlug = sanitizeFilename(fileNameFormatterFixedTime!.AppendFormattedTo(stringBuilder, msg).ToString());
				if (timeBasedWriters!.TryGetValue(timeIndependentFilenameSlug, out var writerEntry)) {
					if (path == writerEntry.Path) {
						return writerEntry.Writer;
					}
					else {
						Directory.CreateDirectory(dir ?? baseDirectory);
						var writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true));
						await writerEntry.Writer.FlushAsync();
						await writerEntry.Writer.DisposeAsync();
						timeBasedWriters[timeIndependentFilenameSlug] = (path, writer);
						return writer;
					}
				}
				else {
					Directory.CreateDirectory(dir ?? baseDirectory);
					var writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true));
					timeBasedWriters[timeIndependentFilenameSlug] = (path, writer);
					return writer;
				}
			}
			else {
				if (!normalWriters!.TryGetValue(path, out var writer)) {
					Directory.CreateDirectory(dir ?? baseDirectory);
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
			if (msg.Exception != null) {
				exceptionMessageFormatter.AppendFormattedTo(stringBuilder, msg);
			}
			else {
				normalMessageFormatter.AppendFormattedTo(stringBuilder, msg);
			}
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
