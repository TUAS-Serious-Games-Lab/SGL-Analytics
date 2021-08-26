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
			LogPath logPath = new LogPath { AppName = "FileSystemCollectorLogStorageUnitTest", UserId = Guid.NewGuid(), LogId = Guid.NewGuid(), Suffix = ".log" };
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
	}
}
