using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Infrastructure.Services;
using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Tests {
	public class FileSystemCollectorLogStorageUnitTestFixture : IDisposable {
		private readonly string storageDirectory = Path.Combine(Environment.CurrentDirectory, "TempTestData", "LogStorage");
		public FileSystemLogRepository FSStorage { get; set; }
		public ILogFileRepository Storage => FSStorage;

		public FileSystemCollectorLogStorageUnitTestFixture() {
			FSStorage = new FileSystemLogRepository(storageDirectory);
		}

		public void Dispose() {
			Directory.Delete(storageDirectory, true);
		}
	}

	public class FileSystemCollectorLogStorageUnitTest : IClassFixture<FileSystemCollectorLogStorageUnitTestFixture> {
		private const string appName = "FileSystemCollectorLogStorageUnitTest";
		private const string suffix = ".log";
		private FileSystemLogRepository fsStorage => fixture.FSStorage;
		private ILogFileRepository storage => fixture.Storage;
		private ITestOutputHelper output;
		private FileSystemCollectorLogStorageUnitTestFixture fixture;

		public FileSystemCollectorLogStorageUnitTest(ITestOutputHelper output, FileSystemCollectorLogStorageUnitTestFixture fixture) {
			this.output = output;
			this.fixture = fixture;
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

		[Fact]
		public async Task CreatedLogsAreCorrectlyEnumeratedForAppUser() {
			Guid userId = Guid.NewGuid();
			var positivePathList = new List<LogPath>() {
				new LogPath() { AppName = appName, UserId = userId, LogId = Guid.NewGuid(), Suffix = suffix },
				new LogPath() { AppName = appName, UserId = userId, LogId = Guid.NewGuid(), Suffix = suffix },
				new LogPath() { AppName = appName, UserId = userId, LogId = Guid.NewGuid(), Suffix = suffix },
				new LogPath() { AppName = appName, UserId = userId, LogId = Guid.NewGuid(), Suffix = suffix }
			};
			var negativePathList = new List<LogPath>() {
				new LogPath() { AppName = appName + "_A", UserId = userId, LogId = Guid.NewGuid(), Suffix = suffix },
				new LogPath() { AppName = appName + "_B", UserId = Guid.NewGuid(), LogId = Guid.NewGuid(), Suffix = suffix },
				new LogPath() { AppName = appName, UserId = Guid.NewGuid(), LogId = Guid.NewGuid(), Suffix = suffix }
			};
			using (var content = new MemoryStream()) {
				foreach (var p in positivePathList) {
					await storage.StoreLogAsync(p, content);
				}
				foreach (var p in negativePathList) {
					await storage.StoreLogAsync(p, content);
				}
			}
			Assert.All(positivePathList, p => Assert.Contains(p, storage.EnumerateLogs(appName, userId)));
			Assert.All(negativePathList, p => Assert.DoesNotContain(p, storage.EnumerateLogs(appName, userId)));
		}

		[Fact]
		public async Task CreatedLogsAreCorrectlyEnumeratedForApp() {
			Guid userId = Guid.NewGuid();
			var positivePathList = new List<LogPath>() {
				new LogPath() { AppName = appName, UserId = userId, LogId = Guid.NewGuid(), Suffix = suffix },
				new LogPath() { AppName = appName, UserId = userId, LogId = Guid.NewGuid(), Suffix = suffix },
				new LogPath() { AppName = appName, UserId = userId, LogId = Guid.NewGuid(), Suffix = suffix },
				new LogPath() { AppName = appName, UserId = userId, LogId = Guid.NewGuid(), Suffix = suffix },
				new LogPath() { AppName = appName, UserId = Guid.NewGuid(), LogId = Guid.NewGuid(), Suffix = suffix }
			};
			var negativePathList = new List<LogPath>() {
				new LogPath() { AppName = appName + "_A", UserId = userId, LogId = Guid.NewGuid(), Suffix = suffix },
				new LogPath() { AppName = appName + "_B", UserId = Guid.NewGuid(), LogId = Guid.NewGuid(), Suffix = suffix },
			};
			using (var content = new MemoryStream()) {
				foreach (var p in positivePathList) {
					await storage.StoreLogAsync(p, content);
				}
				foreach (var p in negativePathList) {
					await storage.StoreLogAsync(p, content);
				}
			}
			Assert.All(positivePathList, p => Assert.Contains(p, storage.EnumerateLogs(appName)));
			Assert.All(negativePathList, p => Assert.DoesNotContain(p, storage.EnumerateLogs(appName)));
		}

		[Fact]
		public async Task CreatedLogsAreCorrectlyEnumeratedOverall() {
			Guid userId = Guid.NewGuid();
			var pathList = new List<LogPath>() {
				new LogPath() { AppName = appName, UserId = userId, LogId = Guid.NewGuid(), Suffix = suffix },
				new LogPath() { AppName = appName, UserId = userId, LogId = Guid.NewGuid(), Suffix = suffix },
				new LogPath() { AppName = appName, UserId = userId, LogId = Guid.NewGuid(), Suffix = suffix },
				new LogPath() { AppName = appName, UserId = userId, LogId = Guid.NewGuid(), Suffix = suffix },
				new LogPath() { AppName = appName, UserId = Guid.NewGuid(), LogId = Guid.NewGuid(), Suffix = suffix },
				new LogPath() { AppName = appName + "_A", UserId = userId, LogId = Guid.NewGuid(), Suffix = suffix },
				new LogPath() { AppName = appName + "_B", UserId = Guid.NewGuid(), LogId = Guid.NewGuid(), Suffix = suffix },
			};
			using (var content = new MemoryStream()) {
				foreach (var p in pathList) {
					await storage.StoreLogAsync(p, content);
				}
			}
			Assert.All(pathList, p => Assert.Contains(p, storage.EnumerateLogs()));
		}

		[Fact]
		public async Task AttemptingToReadNonExistentLogThrowsCorrectException() {
			var path = new LogPath() { AppName = appName, UserId = Guid.NewGuid(), LogId = Guid.NewGuid(), Suffix = suffix };
			var ex = await Assert.ThrowsAsync<LogFileNotAvailableException>(async () => { await using (var stream = await storage.ReadLogAsync(path)) { } });
			Assert.Equal(path, ex.LogPath);
		}

		[Fact]
		public async Task DeletedLogIsNoLongerEnumerated() {
			var path = new LogPath() { AppName = appName, UserId = Guid.NewGuid(), LogId = Guid.NewGuid(), Suffix = suffix };
			using (var content = new MemoryStream()) {
				await storage.StoreLogAsync(path, content);
				Assert.Contains(path, storage.EnumerateLogs());
				await storage.DeleteLogAsync(path);
				Assert.DoesNotContain(path, storage.EnumerateLogs());
			}
		}

		[Fact]
		public async Task LastFinishedStoreWins() {
			var path = new LogPath() { AppName = appName, UserId = Guid.NewGuid(), LogId = Guid.NewGuid(), Suffix = suffix };

			using (var contentA = makeRandomTextContent())
			using (var contentB = makeRandomTextContent()) {
				using (var trigStreamA = new TriggeredBlockingStream(contentA))
				using (var trigStreamB = new TriggeredBlockingStream(contentB)) {
					var taskA = storage.StoreLogAsync(path, trigStreamA);
					var taskB = storage.StoreLogAsync(path, trigStreamB);
					trigStreamB.TriggerReadReady();
					await taskB;
					contentB.Position = 0;
					using (var readStream = await storage.ReadLogAsync(path)) {
						output.WriteStreamContents(readStream);
						readStream.Position = 0;
						using (var origReader = new StreamReader(contentB, leaveOpen: true))
						using (var readBackReader = new StreamReader(readStream, leaveOpen: true)) {
							Assert.Equal(origReader.EnumerateLines(), readBackReader.EnumerateLines());
						}
					}
					output.WriteLine("");
					trigStreamA.TriggerReadReady();
					await taskA;
					contentA.Position = 0;
					using (var readStream = await storage.ReadLogAsync(path)) {
						output.WriteStreamContents(readStream);
						readStream.Position = 0;
						using (var origReader = new StreamReader(contentA, leaveOpen: true))
						using (var readBackReader = new StreamReader(readStream, leaveOpen: true)) {
							Assert.Equal(origReader.EnumerateLines(), readBackReader.EnumerateLines());
						}
					}
				}
			}
		}
	}
}
