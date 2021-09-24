﻿using System;
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

		public FileLoggingSink(FileLoggingSinkOptions options, string baseDirectory,
			NamedPlaceholderFormatterFactory<LogMessage> formatterFactory,
			NamedPlaceholderFormatterFactory<LogMessage> formatterFactoryFixedTime) {
			this.options = options;
			this.formatterFactory = formatterFactory;
			this.formatterFactoryFixedTime = formatterFactoryFixedTime;

			this.baseDirectory = baseDirectory;
			normalMessageFormatter = formatterFactory.Create(options.MessageFormat);
			exceptionMessageFormatter = formatterFactory.Create(options.MessageFormatException);
			fileNameFormatter = formatterFactory.Create(options.FilenameFormat);
			timeBased = fileNameFormatter.UsesPlaceholder("Time");
			if (timeBased) {
				fileNameFormatterFixedTime = formatterFactoryFixedTime.Create(options.FilenameFormat);
				timeBasedWriters = options.MaxOpenStreams > 0 ?
					new LRUCache<string, (string Path, StreamWriter Writer)>(options.MaxOpenStreams, w => closeList.Add(w.Writer)) :
					new Dictionary<string, (string Path, StreamWriter Writer)>();
			}
			else {
				normalWriters = options.MaxOpenStreams > 0 ?
					new LRUCache<string, StreamWriter>(options.MaxOpenStreams, w => closeList.Add(w)) :
					new Dictionary<string, StreamWriter>();
			}
		}

		FileLoggingSinkOptions options;
		private IDictionary<string, (string Path, StreamWriter Writer)>? timeBasedWriters;
		private IDictionary<string, StreamWriter>? normalWriters;
		private List<StreamWriter> closeList = new();
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

		private async ValueTask<StreamWriter> getWriterAsync(LogMessage msg) {
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

		private async ValueTask processPendingClosesAsync() {
			foreach (var writer in closeList) {
				await writer.DisposeAsync();
			}
			closeList.Clear();
		}

		private bool filter(LogMessage msg) {
			if (msg.Level < options.MinLevel) return false;
			return (options.Categories.Count == 0 && options.CategoryContains.Count == 0) ||
				options.Categories.Contains(msg.Category) ||
				options.CategoryContains.Any(c => msg.Category.Contains(c));
		}

		public async Task WriteAsync(LogMessage msg) {
			if (!filter(msg)) return;
			var writer = await getWriterAsync(msg);
			await processPendingClosesAsync();
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
			foreach (var writer in (timeBased ? timeBasedWriters!.Values.Select(w => w.Writer) : normalWriters!.Values).Concat(closeList)) {
				writer.Dispose();
			}
			timeBasedWriters?.Clear();
			normalWriters?.Clear();
			closeList.Clear();
		}

		public async ValueTask DisposeAsync() {
			foreach (var writer in timeBased ? timeBasedWriters!.Values.Select(w => w.Writer) : normalWriters!.Values) {
				await writer.DisposeAsync();
			}
			timeBasedWriters?.Clear();
			normalWriters?.Clear();
			await processPendingClosesAsync();
		}
	}
}
