using SGL.Analytics.Backend.LogCollector.Storage;
using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Backend.LogCollector.Tests {
	public class FileSystemCollectorLogStorageUnitTest {
		private const string appName = "FileSystemCollectorLogStorageUnitTest";
		private const string suffix = ".log";
		private FileSystemCollectorLogStorage fsStorage = new FileSystemCollectorLogStorage(new FSCollectorLogStorageOptions { });
		private ICollectorLogStorage storage;
		private ITestOutputHelper output;

		public FileSystemCollectorLogStorageUnitTest(ITestOutputHelper output) {
			this.output = output;
			storage = fsStorage;
		}

		private MemoryStream makeRandomTextContent() {
			var content = new MemoryStream();
			using (var writer = new StreamWriter(content, leaveOpen: true)) {
				for (int i = 0; i < 10; ++i) {
					writer.WriteLine(StringGenerator.GenerateRandomString(100));
				}
			}
			content.Position = 0;
			return content;
		}

		[Fact]
		public async Task LogIsStoredAndRetrievedCorrectly() {
			LogPath logPath = new LogPath { AppName = appName, UserId = Guid.NewGuid(), LogId = Guid.NewGuid(), Suffix = suffix };
			using (var content = makeRandomTextContent()) {
				await storage.StoreLogAsync(logPath, content);
				content.Position = 0;
				using (var readStream = await storage.ReadLogAsync(logPath)) {
					output.WriteStreamContents(readStream);
					readStream.Position = 0;
					using (var origReader = new StreamReader(content, leaveOpen: true))
					using (var readBackReader = new StreamReader(readStream, leaveOpen: true)) {
						Assert.Equal(origReader.EnumerateLines(), readBackReader.EnumerateLines());
					}
				}
			}
		}
		private async Task separationTest(LogPath logPathA, LogPath logPathB) {
			using (var contentA = makeRandomTextContent())
			using (var contentB = makeRandomTextContent()) {
				var taskA = storage.StoreLogAsync(logPathA, contentA);
				var taskB = storage.StoreLogAsync(logPathB, contentB);
				await Task.WhenAll(taskA, taskB);
				contentA.Position = 0;
				contentB.Position = 0;
				using (var readStreamA = await storage.ReadLogAsync(logPathA))
				using (var readStreamB = await storage.ReadLogAsync(logPathB)) {
					using (var origAReader = new StreamReader(contentA, leaveOpen: true))
					using (var readBackAReader = new StreamReader(readStreamA, leaveOpen: true))
					using (var origBReader = new StreamReader(contentB, leaveOpen: true))
					using (var readBackBReader = new StreamReader(readStreamB, leaveOpen: true)) {
						Assert.Equal(origAReader.EnumerateLines(), readBackAReader.EnumerateLines());
						Assert.Equal(origBReader.EnumerateLines(), readBackBReader.EnumerateLines());
					}
					readStreamA.Position = 0;
					readStreamB.Position = 0;
					using (var readBackAReader = new StreamReader(readStreamA, leaveOpen: true))
					using (var readBackBReader = new StreamReader(readStreamB, leaveOpen: true)) {
						Assert.NotEqual(readBackAReader.EnumerateLines(), readBackBReader.EnumerateLines());
					}
				}
			}
		}

		[Fact]
		public async Task LogsWithSameIdAreSeparatedByUser() {
			LogPath logPathA = new LogPath { AppName = appName, UserId = Guid.NewGuid(), LogId = Guid.NewGuid(), Suffix = suffix };
			LogPath logPathB = new LogPath { AppName = logPathA.AppName, UserId = Guid.NewGuid(), LogId = logPathA.LogId, Suffix = suffix };
			await separationTest(logPathA, logPathB);
		}

		[Fact]
		public async Task LogsWithSameIdAndUserAreSeparatedByApp() {
			LogPath logPathA = new LogPath { AppName = appName + "_A", UserId = Guid.NewGuid(), LogId = Guid.NewGuid(), Suffix = suffix };
			LogPath logPathB = new LogPath { AppName = appName + "_B", UserId = logPathA.UserId, LogId = logPathA.LogId, Suffix = suffix };
			await separationTest(logPathA, logPathB);
		}
	}
}
