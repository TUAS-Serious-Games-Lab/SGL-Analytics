using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities.Logging.FileLogging {
	public class FileLoggingSink : IDisposable, IAsyncDisposable {
		private static Action<INamedPlaceholderFormatterFactoryBuilder<LogMessage>> formaterFactoryBuilder = builder => {
			builder.AddPlaceholder("AppDomainName", m => AppDomain.CurrentDomain.FriendlyName);
			builder.AddPlaceholder("Category", m => m.Category);
			builder.AddPlaceholder("ScopesJoined", m => string.Join(";", m.Scopes));
			builder.AddPlaceholder("Scope0", m => m.Scopes.FirstOrDefault() ?? "");
			builder.AddPlaceholder("Scope1", m => m.Scopes.Skip(1).FirstOrDefault() ?? "");
			builder.AddPlaceholder("Scope2", m => m.Scopes.Skip(2).FirstOrDefault() ?? "");
			builder.AddPlaceholder("Scope3", m => m.Scopes.Skip(3).FirstOrDefault() ?? "");
			builder.AddPlaceholder("Scope4", m => m.Scopes.Skip(4).FirstOrDefault() ?? "");
			builder.AddPlaceholder("Scope5", m => m.Scopes.Skip(5).FirstOrDefault() ?? "");
			builder.AddPlaceholder("Scope6", m => m.Scopes.Skip(6).FirstOrDefault() ?? "");
			builder.AddPlaceholder("Scope7", m => m.Scopes.Skip(7).FirstOrDefault() ?? "");
			builder.AddPlaceholder("Level", m => m.Level);
			builder.AddPlaceholder("EventId", m => m.EventId);
			builder.AddPlaceholder("Time", m => m.Time);
			builder.AddPlaceholder("Text", m => m.Text);
			builder.AddPlaceholder("Exception", m => m.Exception?.ToString() ?? "");
		};
		private static NamedPlaceholderFormatterFactory<LogMessage> formatterFactory = new NamedPlaceholderFormatterFactory<LogMessage>(formaterFactoryBuilder);
		private static NamedPlaceholderFormatterFactory<LogMessage> formatterFactoryFixedTime = new NamedPlaceholderFormatterFactory<LogMessage>(builder => {
			formaterFactoryBuilder(builder);
			// For writers with time-based filenames we need to replace out-dated writers instead of just opening new ones.
			// Therefore, we need a version of the formatted name that is time-independent as a key to compare if an existing writer is otherwise for the same file, i.e. for the predecessor.
			// For this, we use this factory to always format the strings with epoch for the time-based placeholders.
			builder.AddPlaceholder("Time", m => DateTime.UnixEpoch);
		});

		private string baseDirectory = Path.Combine(Environment.CurrentDirectory, "log");
		private NamedPlaceholderFormatter<LogMessage> normalMessageFormatter;
		private NamedPlaceholderFormatter<LogMessage> exceptionMessageFormatter;
		private NamedPlaceholderFormatter<LogMessage>? fileNameFormatter;
		private NamedPlaceholderFormatter<LogMessage>? fileNameFormatterFixedTime;
		private bool timeBased;

		public FileLoggingSink(FileLoggingSinkOptions options) {
			baseDirectory = options.BaseDirectory;

			normalMessageFormatter = formatterFactory.Create(options.MessageFormat);
			exceptionMessageFormatter = formatterFactory.Create(options.MessageFormatException);
			fileNameFormatter = formatterFactory.Create(options.FilenameFormat);
			timeBased = fileNameFormatter.UsesPlaceholder("Time");
			if (timeBased) {
				fileNameFormatterFixedTime = formatterFactoryFixedTime.Create(options.FilenameFormat);
				timeBasedWriters = new();
			}
			else {
				normalWriters = new();
			}
		}

		private Dictionary<string, (string Path, StreamWriter Writer)>? timeBasedWriters;
		private Dictionary<string, StreamWriter>? normalWriters;
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
				var timeIndependentFilenameSlug = sanitizeFilename(fileNameFormatterFixedTime.AppendFormattedTo(stringBuilder, msg).ToString());
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
