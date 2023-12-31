﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Application.Services;
using SGL.Analytics.Backend.Logs.Application.Tests.Dummies;
using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Backend.Applications;
using SGL.Utilities.Backend.TestUtilities.Applications;
using SGL.Utilities.Crypto.EndToEnd;
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
		private IApplicationRepository<Domain.Entity.Application, ApplicationQueryOptions> appRepo = new DummyApplicationRepository<Domain.Entity.Application, ApplicationQueryOptions>();
		private ILogFileRepository logFileRepo = new DummyLogFileRepository();
		private DummyLogMetadataRepository logMetadataRepo = new DummyLogMetadataRepository();
		private ITestOutputHelper output;
		private ILoggerFactory loggerFactory;
		private LogManager manager;

		public LogManagerUnitTest(ITestOutputHelper output) {
			this.output = output;
			loggerFactory = LoggerFactory.Create(c => c.AddXUnit(output).SetMinimumLevel(LogLevel.Trace));
			manager = new LogManager(appRepo, logMetadataRepo, logFileRepo, loggerFactory.CreateLogger<LogManager>(), new NullMetricsManager(),
				Options.Create(new LogManagerOptions { RekeyingPagination = 10 }));
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
			LogMetadataDTO dto = new(logFileId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), suffix,
				LogContentEncoding.Plain, EncryptionInfo.CreateUnencrypted());
			var key = new LogPath() { AppName = appName, UserId = userId, LogId = logFileId, Suffix = suffix };
			await using (var origContent = generateRandomMemoryStream()) {
				await manager.IngestLogAsync(userId, appName, appApiToken, dto, origContent);
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
			LogMetadataDTO dto1 = new(logFileId, DateTime.Now.AddMinutes(-120), DateTime.Now.AddMinutes(-95), suffix,
				LogContentEncoding.Plain, EncryptionInfo.CreateUnencrypted());
			LogMetadataDTO dto2 = new(logFileId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), suffix,
				LogContentEncoding.Plain, EncryptionInfo.CreateUnencrypted());
			await using (var content = generateRandomMemoryStream()) {
				await manager.IngestLogAsync(user1Id, appName, appApiToken, dto1, content);
			}
			await using (var origContent = generateRandomMemoryStream()) {
				var logFile = await manager.IngestLogAsync(user2Id, appName, appApiToken, dto2, origContent);
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
			LogMetadataDTO dto = new(logFileId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), suffix,
				LogContentEncoding.Plain, EncryptionInfo.CreateUnencrypted());
			await using (var origContent = generateRandomMemoryStream()) {
				var streamWrapper = new TriggeredBlockingStream(origContent);
				var task = manager.IngestLogAsync(userId, appName, appApiToken, dto, streamWrapper);
				streamWrapper.TriggerReadError(new IOException("Connection to client lost."));
				await Assert.ThrowsAsync<IOException>(async () => await task);
				origContent.Position = 0;
				var logFile = await manager.IngestLogAsync(userId, appName, appApiToken, dto, origContent);
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
			LogMetadataDTO dto = new(logFileId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), suffix,
				LogContentEncoding.Plain, EncryptionInfo.CreateUnencrypted());
			await using (var origContent = generateRandomMemoryStream()) {
				await Assert.ThrowsAsync<ApplicationDoesNotExistException>(async () => await manager.IngestLogAsync(userId, "DoesNotExist", "FakeAPIToken", dto, origContent));
			}
		}
		[Fact]
		public async Task ReattemptingIngestOfLogWhereServerAssignedNewIdPicksUpTheExistingEntry() {
			Guid logFileId = Guid.NewGuid();
			Guid user1Id = Guid.NewGuid();
			string suffix = ".log";
			LogMetadataDTO dto1 = new(logFileId, DateTime.Now.AddMinutes(-120), DateTime.Now.AddMinutes(-95), suffix,
				LogContentEncoding.Plain, EncryptionInfo.CreateUnencrypted());
			await using (var content = generateRandomMemoryStream()) {
				await manager.IngestLogAsync(user1Id, appName, appApiToken, dto1, content);
			}

			Guid user2Id = Guid.NewGuid();
			LogMetadataDTO dto2 = new(logFileId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), suffix,
				LogContentEncoding.Plain, EncryptionInfo.CreateUnencrypted());
			await using (var origContent = generateRandomMemoryStream()) {
				var streamWrapper = new TriggeredBlockingStream(origContent);
				var task = manager.IngestLogAsync(user2Id, appName, appApiToken, dto2, streamWrapper);
				streamWrapper.TriggerReadError(new IOException("Connection to client lost."));
				await Assert.ThrowsAsync<IOException>(async () => await task);
				origContent.Position = 0;
				var logQuery = logMetadataRepo.Logs.Values.Where(lm => lm.LocalLogId == logFileId && lm.UserId == user2Id);
				Assert.Single(logQuery);
				var logMd = logQuery.Single();

				var logFile = await manager.IngestLogAsync(user2Id, appName, appApiToken, dto2, origContent);
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
