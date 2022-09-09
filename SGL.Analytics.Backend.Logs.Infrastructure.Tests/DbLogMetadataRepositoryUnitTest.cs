using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using SGL.Analytics.Backend.Logs.Infrastructure.Services;
using SGL.Utilities.Backend;
using SGL.Utilities.Backend.TestUtilities;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Tests {
	public class DbLogMetadataRepositoryUnitTest : IDisposable {
		TestDatabase<LogsContext> testDb = new();

		private LogsContext createContext() {
			return new LogsContext(testDb.ContextOptions);
		}

		[Fact]
		public async Task AddedMetadataEntryCanBeRetrievedThroughDb() {
			var logId = Guid.NewGuid();
			KeyId keyId1 = KeyId.Parse("02:12345678:12345678:87654321:87654321:12345678:12345678:87654321:11111111");
			KeyId keyId2 = KeyId.Parse("02:12345678:12345678:87654321:87654321:12345678:12345678:87654321:22222222");
			EncryptionInfo encryptionInfo = new EncryptionInfo {
				DataMode = DataEncryptionMode.AES_256_CCM,
				IVs = new List<byte[]> { Encoding.UTF8.GetBytes("Test IV 1") },
				MessagePublicKey = Encoding.UTF8.GetBytes("Test Shared Message Public Key"),
				DataKeys = new Dictionary<KeyId, DataKeyInfo> {
					[keyId1] = new DataKeyInfo {
						Mode = KeyEncryptionMode.ECDH_KDF2_SHA256_AES_256_CCM,
						EncryptedKey = Encoding.UTF8.GetBytes("Fake Encrypted Data Key 1"),
						MessagePublicKey = Encoding.UTF8.GetBytes("Fake Message Public Key 1")
					},
					[keyId2] = new DataKeyInfo {
						Mode = KeyEncryptionMode.ECDH_KDF2_SHA256_AES_256_CCM,
						EncryptedKey = Encoding.UTF8.GetBytes("Fake Encrypted Data Key 2"),
						MessagePublicKey = Encoding.UTF8.GetBytes("Fake Message Public Key 2")
					}
				}
			};
			var app = new Domain.Entity.Application(Guid.NewGuid(), "DbLogMetadataRepositoryUnitTest", "FakeApiToken");
			var logMd = LogMetadata.Create(logId, app, Guid.NewGuid(), logId, DateTime.Now.AddMinutes(-15), DateTime.Now.AddMinutes(-1), DateTime.Now, ".log.gz", DTO.LogContentEncoding.GZipCompressed, 42, encryptionInfo);
			await using (var context = createContext()) {
				context.Applications.Add(app);
				var repo = new DbLogMetadataRepository(context);
				logMd = await repo.AddLogMetadataAsync(logMd);
			}
			LogMetadata? logMdRead;
			await using (var context = createContext()) {
				var repo = new DbLogMetadataRepository(context);
				logMdRead = await repo.GetLogMetadataByIdAsync(logId);
			}
			Assert.NotNull(logMdRead);
			Assert.Equal(logId, logMdRead?.Id);
			Assert.Equal(logMd.Id, logMdRead?.Id);
			Assert.Equal(logMd.AppId, logMdRead?.AppId);
			Assert.Equal(logMd.UserId, logMdRead?.UserId);
			Assert.Equal(logMd.LocalLogId, logMdRead?.LocalLogId);
			Assert.Equal(logMd.CreationTime.ToUniversalTime(), logMdRead?.CreationTime);
			Assert.Equal(logMd.EndTime.ToUniversalTime(), logMdRead?.EndTime);
			Assert.Equal(logMd.UploadTime.ToUniversalTime(), logMdRead?.UploadTime);
			Assert.Equal(logMd.Size, logMdRead?.Size);
			var readEncryptionInfo = logMdRead!.EncryptionInfo;
			Assert.Equal(encryptionInfo.DataMode, readEncryptionInfo.DataMode);
			Assert.Equal(encryptionInfo.IVs, readEncryptionInfo.IVs);
			Assert.Equal(encryptionInfo.MessagePublicKey, readEncryptionInfo.MessagePublicKey);
			var dk1 = Assert.Contains(keyId1, readEncryptionInfo.DataKeys as IReadOnlyDictionary<KeyId, DataKeyInfo>);
			Assert.Equal(encryptionInfo.DataKeys[keyId1].EncryptedKey, dk1.EncryptedKey);
			Assert.Equal(encryptionInfo.DataKeys[keyId1].Mode, dk1.Mode);
			Assert.Equal(encryptionInfo.DataKeys[keyId1].MessagePublicKey, dk1.MessagePublicKey);

			var dk2 = Assert.Contains(keyId2, readEncryptionInfo.DataKeys as IReadOnlyDictionary<KeyId, DataKeyInfo>);
			Assert.Equal(encryptionInfo.DataKeys[keyId2].EncryptedKey, dk2.EncryptedKey);
			Assert.Equal(encryptionInfo.DataKeys[keyId2].Mode, dk2.Mode);
			Assert.Equal(encryptionInfo.DataKeys[keyId2].MessagePublicKey, dk2.MessagePublicKey);
		}

		[Fact]
		public async Task MetadataEntryIsCorrectlyUpdatedInDb() {
			var logId = Guid.NewGuid();
			var keyId1 = KeyId.Parse("02:12345678:12345678:87654321:87654321:12345678:12345678:87654321:87654321");
			var keyId2 = KeyId.Parse("02:98765432:12345678:87654321:87654321:12345678:12345678:87654321:87654321");
			var keyId3 = KeyId.Parse("01:11111111:12345678:87654321:87654321:12345678:12345678:87654321:87654321");
			EncryptionInfo encryptionInfo = new EncryptionInfo {
				DataMode = DataEncryptionMode.AES_256_CCM,
				IVs = new List<byte[]> { Encoding.UTF8.GetBytes("Test IV 1") },
				MessagePublicKey = Encoding.UTF8.GetBytes("Test Shared Message Public Key"),
				DataKeys = new Dictionary<Utilities.Crypto.Keys.KeyId, DataKeyInfo> {
					[keyId1] = new DataKeyInfo {
						Mode = KeyEncryptionMode.ECDH_KDF2_SHA256_AES_256_CCM,
						EncryptedKey = Encoding.UTF8.GetBytes("Fake Encrypted Data Key 1"),
						MessagePublicKey = Encoding.UTF8.GetBytes("Fake Message Public Key 1")
					},
					[keyId2] = new DataKeyInfo {
						Mode = KeyEncryptionMode.ECDH_KDF2_SHA256_AES_256_CCM,
						EncryptedKey = Encoding.UTF8.GetBytes("Fake Encrypted Data Key 2"),
						MessagePublicKey = Encoding.UTF8.GetBytes("Fake Message Public Key 2")
					}
				}
			};
			var app = new Domain.Entity.Application(Guid.NewGuid(), "DbLogMetadataRepositoryUnitTest", "FakeApiToken");
			var logMd = LogMetadata.Create(logId, app, Guid.NewGuid(), logId, DateTime.Now.AddMinutes(-15), DateTime.Now.AddMinutes(-1), DateTime.Now.AddSeconds(-30), ".log.gz", DTO.LogContentEncoding.GZipCompressed, 42, encryptionInfo);
			await using (var context = createContext()) {
				context.Applications.Add(app);
				logMd.App = app;
				var repo = new DbLogMetadataRepository(context);
				logMd = await repo.AddLogMetadataAsync(logMd);
			}
			LogMetadata? logMd2;
			await using (var context = createContext()) {
				var repo = new DbLogMetadataRepository(context);
				logMd2 = await repo.GetLogMetadataByIdAsync(logId);
				Assert.NotNull(logMd2);
				if (logMd2 is null) throw new NotNullException();
				encryptionInfo.IVs[0] = Encoding.UTF8.GetBytes("Changed IV");
				encryptionInfo.DataKeys[keyId1].EncryptedKey = Encoding.UTF8.GetBytes("Updated Fake Key");
				encryptionInfo.DataKeys.Remove(keyId2);
				encryptionInfo.DataKeys[keyId3] = new DataKeyInfo {
					Mode = KeyEncryptionMode.RSA_PKCS1,
					EncryptedKey = Encoding.UTF8.GetBytes("Fake Encrypted Data Key 2"),
					MessagePublicKey = null
				};
				logMd2.EncryptionInfo = encryptionInfo;
				logMd2.LocalLogId = Guid.NewGuid();
				logMd2.UploadTime = DateTime.Now;
				await repo.UpdateLogMetadataAsync(logMd2);
			}
			LogMetadata? logMdRead;
			await using (var context = createContext()) {
				var repo = new DbLogMetadataRepository(context);
				logMdRead = await repo.GetLogMetadataByIdAsync(logId);
			}
			Assert.NotNull(logMdRead);
			Assert.Equal(logId, logMdRead?.Id);
			Assert.Equal(logMd2.Id, logMdRead?.Id);
			Assert.Equal(logMd2.AppId, logMdRead?.AppId);
			Assert.Equal(logMd2.UserId, logMdRead?.UserId);
			Assert.Equal(logMd2.LocalLogId, logMdRead?.LocalLogId);
			Assert.Equal(logMd2.CreationTime.ToUniversalTime(), logMdRead?.CreationTime);
			Assert.Equal(logMd2.EndTime.ToUniversalTime(), logMdRead?.EndTime);
			Assert.Equal(logMd2.UploadTime.ToUniversalTime(), logMdRead?.UploadTime);
			Assert.Equal(logMd.Size, logMdRead?.Size);
			var readEncryptionInfo = logMdRead!.EncryptionInfo;
			Assert.Equal(encryptionInfo.DataMode, readEncryptionInfo.DataMode);
			Assert.Equal(encryptionInfo.IVs, readEncryptionInfo.IVs);
			Assert.Equal(encryptionInfo.MessagePublicKey, readEncryptionInfo.MessagePublicKey);
			Assert.DoesNotContain(keyId2, readEncryptionInfo.DataKeys as IReadOnlyDictionary<KeyId, DataKeyInfo>);
			var dk1 = Assert.Contains(keyId1, readEncryptionInfo.DataKeys as IReadOnlyDictionary<KeyId, DataKeyInfo>);
			Assert.Equal(encryptionInfo.DataKeys[keyId1].EncryptedKey, dk1.EncryptedKey);
			Assert.Equal(encryptionInfo.DataKeys[keyId1].Mode, dk1.Mode);
			Assert.Equal(encryptionInfo.DataKeys[keyId1].MessagePublicKey, dk1.MessagePublicKey);

			var dk3 = Assert.Contains(keyId3, readEncryptionInfo.DataKeys as IReadOnlyDictionary<KeyId, DataKeyInfo>);
			Assert.Equal(encryptionInfo.DataKeys[keyId3].EncryptedKey, dk3.EncryptedKey);
			Assert.Equal(encryptionInfo.DataKeys[keyId3].Mode, dk3.Mode);
			Assert.Equal(encryptionInfo.DataKeys[keyId3].MessagePublicKey, dk3.MessagePublicKey);
		}

		[Fact]
		public async Task RequestForNonExistentLogMetadataReturnsNull() {
			var logId = Guid.NewGuid();
			LogMetadata? logMdRead;
			await using (var context = createContext()) {
				var repo = new DbLogMetadataRepository(context);
				logMdRead = await repo.GetLogMetadataByIdAsync(logId);
			}
			Assert.Null(logMdRead);
		}

		[Fact]
		public async Task AttemptingToCreateMetadataEntryWithDuplicateIdThrowsCorrectException() {
			var id = Guid.NewGuid();
			EncryptionInfo encryptionInfo1 = new EncryptionInfo {
				DataMode = DataEncryptionMode.AES_256_CCM,
				IVs = new List<byte[]> { Encoding.UTF8.GetBytes("Test IV 1") },
				MessagePublicKey = Encoding.UTF8.GetBytes("Test Shared Message Public Key"),
				DataKeys = new Dictionary<Utilities.Crypto.Keys.KeyId, DataKeyInfo> {
					[KeyId.Parse("02:12345678:12345678:87654321:87654321:12345678:12345678:87654321:87654321")] = new DataKeyInfo {
						Mode = KeyEncryptionMode.ECDH_KDF2_SHA256_AES_256_CCM,
						EncryptedKey = Encoding.UTF8.GetBytes("Fake Encrypted Data Key"),
						MessagePublicKey = Encoding.UTF8.GetBytes("Fake Message Public Key")
					}
				}
			};
			EncryptionInfo encryptionInfo2 = new EncryptionInfo {
				DataMode = DataEncryptionMode.AES_256_CCM,
				IVs = new List<byte[]> { Encoding.UTF8.GetBytes("Test IV 1") },
				MessagePublicKey = Encoding.UTF8.GetBytes("Test Shared Message Public Key"),
				DataKeys = new Dictionary<Utilities.Crypto.Keys.KeyId, DataKeyInfo> {
					[KeyId.Parse("02:12345678:12345678:87654321:87654321:12345678:12345678:87654321:87654322")] = new DataKeyInfo {
						Mode = KeyEncryptionMode.ECDH_KDF2_SHA256_AES_256_CCM,
						EncryptedKey = Encoding.UTF8.GetBytes("Fake Encrypted Data Key"),
						MessagePublicKey = Encoding.UTF8.GetBytes("Fake Message Public Key")
					}
				}
			};
			var app = new Domain.Entity.Application(Guid.NewGuid(), "DbLogMetadataRepositoryUnitTest", "FakeApiToken");
			await using (var context = createContext()) {
				context.Applications.Add(app);
				await context.SaveChangesAsync();
				var repo = new DbLogMetadataRepository(context);
				var logMd = LogMetadata.Create(id, app, Guid.NewGuid(), id, DateTime.Now, DateTime.Now, DateTime.Now, ".log.gz", DTO.LogContentEncoding.GZipCompressed, 42, encryptionInfo1);
				logMd.App = app;
				await repo.AddLogMetadataAsync(logMd);
			}
			await using (var context = createContext()) {
				app = await context.Applications.SingleOrDefaultAsync(a => a.Id == app.Id);
				var repo = new DbLogMetadataRepository(context);
				var logMd = LogMetadata.Create(id, app, Guid.NewGuid(), id, DateTime.Now, DateTime.Now, DateTime.Now, ".log.gz", DTO.LogContentEncoding.GZipCompressed, 42, encryptionInfo2);
				logMd.App = app;
				await Assert.ThrowsAsync<EntityUniquenessConflictException>(async () => await repo.AddLogMetadataAsync(logMd));
			}
		}

		public void Dispose() {
			testDb.Dispose();
		}
	}
}
