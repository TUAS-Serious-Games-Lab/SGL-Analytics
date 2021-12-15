using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Application.Services;
using SGL.Analytics.Backend.Logs.Application.Tests.Dummies;
using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.TestUtilities.XUnit;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Backend.Logs.Application.Tests {
	public class LogManagerUnitTest {
		private const string appName = "LogManagerUnitTest";
		private readonly string appApiToken = StringGenerator.GenerateRandomWord(32);
		private IApplicationRepository appRepo = new DummyApplicationRepository();
		private ILogFileRepository logFileRepo = new DummyLogFileRepository();
		private DummyLogMetadataRepository logMetadataRepo = new DummyLogMetadataRepository();
		private ITestOutputHelper output;
		private ILoggerFactory loggerFactory;
		private LogManager manager;

		public LogManagerUnitTest(ITestOutputHelper output) {
			this.output = output;
			loggerFactory = LoggerFactory.Create(c => c.AddXUnit(output).SetMinimumLevel(LogLevel.Trace));
			manager = new LogManager(appRepo, logMetadataRepo, logFileRepo, loggerFactory.CreateLogger<LogManager>(), new NullMetricsManager());
			appRepo.AddApplicationAsync(new Domain.Entity.Application(Guid.NewGuid(), appName, appApiToken)).Wait();
		}

		private static MemoryStream generateRandomMemoryStream() {
			var content = new MemoryStream();
			using (var writer = new StreamWriter(content, leaveOpen: true)) {
				for (int i = 0; i < 16; ++i) {
					writer.WriteLine(StringGenerator.GenerateRandomString(128));
				}
			}
			content.Position = 0;
			return content;
		}

		[Fact]
		public async Task IngestingNewLogFileWithUniqueIdWorksCorrectly() {
			Guid logFileId = Guid.NewGuid();
			Guid userId = Guid.NewGuid();
			string suffix = ".log";
			LogMetadataDTO dto = new(logFileId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), suffix, LogContentEncoding.Plain);
			var key = new LogPath() { AppName = appName, UserId = userId, LogId = logFileId, Suffix = suffix };
			await using (var origContent = generateRandomMemoryStream()) {
				await manager.IngestLogAsync(userId, appName, dto, origContent, origContent.Length);
				var logMd = await logMetadataRepo.GetLogMetadataByIdAsync(logFileId);
				await using (var readContent = new MemoryStream()) {
					await logFileRepo.CopyLogIntoAsync(key, readContent);
					origContent.Position = 0;
					readContent.Position = 0;
					using (var origReader = new StreamReader(origContent, leaveOpen: true))
					using (var readBackReader = new StreamReader(readContent, leaveOpen: true)) {
						Assert.Equal(origReader.EnumerateLines(), readBackReader.EnumerateLines());
					}
				}
			}
		}

		[Fact]
		public async Task LogFileWithIdCollisionFromAnotherUserIsAssignedANewIdAndCorrectlyUploaded() {
			Guid logFileId = Guid.NewGuid();
			Guid user1Id = Guid.NewGuid();
			Guid user2Id = Guid.NewGuid();
			string suffix = ".log";
			LogMetadataDTO dto1 = new(logFileId, DateTime.Now.AddMinutes(-120), DateTime.Now.AddMinutes(-95), suffix, LogContentEncoding.Plain);
			LogMetadataDTO dto2 = new(logFileId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), suffix, LogContentEncoding.Plain);
			await using (var content = generateRandomMemoryStream()) {
				await manager.IngestLogAsync(user1Id, appName, dto1, content, content.Length);
			}
			await using (var origContent = generateRandomMemoryStream()) {
				var logFile = await manager.IngestLogAsync(user2Id, appName, dto2, origContent, origContent.Length);
				Assert.Equal(logFileId, logFile.LocalLogId);
				Assert.NotEqual(logFileId, logFile.Id);
				await using (var readContent = new MemoryStream()) {
					await logFile.CopyToAsync(readContent);
					origContent.Position = 0;
					readContent.Position = 0;
					using (var origReader = new StreamReader(origContent, leaveOpen: true))
					using (var readBackReader = new StreamReader(readContent, leaveOpen: true)) {
						Assert.Equal(origReader.EnumerateLines(), readBackReader.EnumerateLines());
					}
				}
			}
		}
		[Fact]
		public async Task PreviouslyUnfinishedLogFileUploadIsReattemptedCorrectly() {
			Guid logFileId = Guid.NewGuid();
			Guid userId = Guid.NewGuid();
			string suffix = ".log";
			LogMetadataDTO dto = new(logFileId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), suffix, LogContentEncoding.Plain);
			await using (var origContent = generateRandomMemoryStream()) {
				var streamWrapper = new TriggeredBlockingStream(origContent);
				var task = manager.IngestLogAsync(userId, appName, dto, streamWrapper, origContent.Length);
				streamWrapper.TriggerReadError(new IOException("Connection to client lost."));
				await Assert.ThrowsAsync<IOException>(async () => await task);
				origContent.Position = 0;
				var logFile = await manager.IngestLogAsync(userId, appName, dto, origContent, origContent.Length);
				Assert.Equal(logFileId, logFile.LocalLogId);
				Assert.Equal(logFileId, logFile.Id);
				await using (var readContent = new MemoryStream()) {
					await logFile.CopyToAsync(readContent);
					origContent.Position = 0;
					readContent.Position = 0;
					using (var origReader = new StreamReader(origContent, leaveOpen: true))
					using (var readBackReader = new StreamReader(readContent, leaveOpen: true)) {
						Assert.Equal(origReader.EnumerateLines(), readBackReader.EnumerateLines());
					}
				}
			}

		}
		[Fact]
		public async Task AttemptingToIngestLogFileWithNonExistentApplicationThrowsTheCorrectException() {
			Guid logFileId = Guid.NewGuid();
			Guid userId = Guid.NewGuid();
			string suffix = ".log";
			LogMetadataDTO dto = new(logFileId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), suffix, LogContentEncoding.Plain);
			await using (var origContent = generateRandomMemoryStream()) {
				await Assert.ThrowsAsync<ApplicationDoesNotExistException>(async () => await manager.IngestLogAsync(userId, "DoesNotExist", dto, origContent, origContent.Length));
			}
		}
		[Fact]
		public async Task ReattemptingIngestOfLogWhereServerAssignedNewIdPicksUpTheExistingEntry() {
			Guid logFileId = Guid.NewGuid();
			Guid user1Id = Guid.NewGuid();
			string suffix = ".log";
			LogMetadataDTO dto1 = new(logFileId, DateTime.Now.AddMinutes(-120), DateTime.Now.AddMinutes(-95), suffix, LogContentEncoding.Plain);
			await using (var content = generateRandomMemoryStream()) {
				await manager.IngestLogAsync(user1Id, appName, dto1, content, content.Length);
			}

			Guid user2Id = Guid.NewGuid();
			LogMetadataDTO dto2 = new(logFileId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), suffix, LogContentEncoding.Plain);
			await using (var origContent = generateRandomMemoryStream()) {
				var streamWrapper = new TriggeredBlockingStream(origContent);
				var task = manager.IngestLogAsync(user2Id, appName, dto2, streamWrapper, origContent.Length);
				streamWrapper.TriggerReadError(new IOException("Connection to client lost."));
				await Assert.ThrowsAsync<IOException>(async () => await task);
				origContent.Position = 0;
				var logQuery = logMetadataRepo.Logs.Values.Where(lm => lm.LocalLogId == logFileId && lm.UserId == user2Id);
				Assert.Single(logQuery);
				var logMd = logQuery.Single();

				var logFile = await manager.IngestLogAsync(user2Id, appName, dto2, origContent, origContent.Length);
				Assert.Equal(logFileId, logFile.LocalLogId);
				Assert.NotEqual(logFileId, logFile.Id);
				Assert.Equal(logMd.Id, logFile.Id);
				await using (var readContent = new MemoryStream()) {
					await logFile.CopyToAsync(readContent);
					origContent.Position = 0;
					readContent.Position = 0;
					using (var origReader = new StreamReader(origContent, leaveOpen: true))
					using (var readBackReader = new StreamReader(readContent, leaveOpen: true)) {
						Assert.Equal(origReader.EnumerateLines(), readBackReader.EnumerateLines());
					}
				}
			}
		}
	}
}
