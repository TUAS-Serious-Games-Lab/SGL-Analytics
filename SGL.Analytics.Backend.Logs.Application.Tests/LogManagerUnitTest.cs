using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Application.Services;
using SGL.Analytics.Backend.Logs.Application.Tests.Dummies;
using SGL.Analytics.DTO;
using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Backend.Logs.Application.Tests {
	public class LogManagerUnitTest {
		private const string appName = "LogManagerUnitTest";
		private readonly string appApiToken = StringGenerator.GenerateRandomWord(32);
		private IApplicationRepository appRepo = new DummyApplicationRepository();
		private ILogFileRepository logFileRepo = new DummyLogFileRepository();
		private ILogMetadataRepository logMetadataRepo = new DummyLogMetadataRepository();
		private ITestOutputHelper output;
		private ILoggerFactory loggerFactory;
		private LogManager manager;

		public LogManagerUnitTest(ITestOutputHelper output) {
			this.output = output;
			loggerFactory = LoggerFactory.Create(c => c.AddXUnit(output).SetMinimumLevel(LogLevel.Trace));
			manager = new LogManager(appRepo, logMetadataRepo, logFileRepo, loggerFactory.CreateLogger<LogManager>());
			appRepo.AddApplicationAsync(new Domain.Entity.Application(0, appName, appApiToken)).Wait();
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
			LogMetadataDTO dto = new(appName, userId, logFileId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2));
			var key = new LogPath() { AppName = appName, UserId = userId, LogId = logFileId, Suffix = ".log.gz" };
			await using (var origContent = generateRandomMemoryStream()) {
				await manager.IngestLogAsync(dto, origContent);
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
			LogMetadataDTO dto1 = new(appName, user1Id, logFileId, DateTime.Now.AddMinutes(-120), DateTime.Now.AddMinutes(-95));
			LogMetadataDTO dto2 = new(appName, user2Id, logFileId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2));
			await using (var content = generateRandomMemoryStream()) {
				await manager.IngestLogAsync(dto1, content);
			}
			await using (var origContent = generateRandomMemoryStream()) {
				var logFile = await manager.IngestLogAsync(dto2, origContent);
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
	}
}
