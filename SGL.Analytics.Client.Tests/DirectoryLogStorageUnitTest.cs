using SGL.Utilities;
using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Client.Tests {
	public class DirectoryLogStorageFixture : IDisposable {
		public DirectoryLogStorage Storage { get; private set; }
		public DirectoryLogStorageFixture() {
			string directory = Path.Combine(Directory.GetCurrentDirectory(), "UnitTestTemp", "DirectoryLogStorageUnitTest");
			Directory.CreateDirectory(directory);
			Storage = new DirectoryLogStorage(directory);
		}
		public void Dispose() {
			Storage.Archiving = false;
			foreach (var log in Storage.EnumerateLogs()) {
				log.Remove();
			}
		}
	}
	public class DirectoryLogStorageUnitTest : IClassFixture<DirectoryLogStorageFixture> {
		private DirectoryLogStorageFixture fixture;
		private ITestOutputHelper output;
		public DirectoryLogStorageUnitTest(DirectoryLogStorageFixture fixture, ITestOutputHelper output) {
			this.fixture = fixture;
			this.output = output;
		}
		[Fact]
		public void CreatedLogFileIsEnumerated() {
			ILogStorage.ILogFile? metadata;
			using (var stream = fixture.Storage.CreateLogFile(out metadata)) { }
			Assert.Contains(metadata, fixture.Storage.EnumerateFinishedLogs());
		}
		[Fact]
		public void WrittenLogContentsArePreserved() {
			var content = Enumerable.Range(0, 16).Select(_ => StringGenerator.GenerateRandomString(256)).ToList();
			ILogStorage.ILogFile? metadata;
			using (var writer = new StreamWriter(fixture.Storage.CreateLogFile(out metadata))) {
				content.ForEach(c => writer.WriteLine(c));
			}
			output.WriteLogContents(metadata);
			using (var reader = new StreamReader(metadata.OpenReadContent())) {
				Assert.Equal(content, reader.EnumerateLines());
			}
		}
		[Fact]
		public void CreationTimeIsCorrect() {
			ILogStorage.ILogFile? metadata;
			DateTime before = DateTime.Now;
			DateTime after;
			using (var stream = fixture.Storage.CreateLogFile(out metadata)) {
				after = DateTime.Now;
			}
			var fileTime = metadata.CreationTime;
			// There is a small tolerance window where fileTime can be few milliseconds earlier than before, probably due to too low precission of the filesystem timestamps.
			Assert.InRange(fileTime, before.AddMilliseconds(-20), after.AddMilliseconds(20));
		}
		[Fact]
		public void EndTimeIsCorrect() {
			ILogStorage.ILogFile? metadata;
			DateTime before;
			using (var stream = fixture.Storage.CreateLogFile(out metadata)) {
				before = DateTime.Now;
			}
			DateTime after = DateTime.Now;
			var fileTime = metadata.EndTime;
			// There is a small tolerance window where fileTime can be few milliseconds earlier than before, probably due to too low precission of the filesystem timestamps.
			Assert.InRange(fileTime, before.AddMilliseconds(-20), after.AddMilliseconds(20));
		}
		[Fact]
		public void RemovedFilesAreNotEnumerated() {
			ILogStorage.ILogFile? metadata;
			using (var stream = fixture.Storage.CreateLogFile(out metadata)) { }
			metadata.Remove();
			Assert.DoesNotContain(metadata, fixture.Storage.EnumerateLogs());
		}
	}
}
