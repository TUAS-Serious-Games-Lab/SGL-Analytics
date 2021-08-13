using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SGL.Analytics.Client.Tests {
	public class DirectoryLogStorageUnitTest {
		private DirectoryLogStorage storage;
		public DirectoryLogStorageUnitTest() {
			string directory = Path.Combine(Directory.GetCurrentDirectory(), "UnitTestTemp", "DirectoryLogStorageUnitTest");
			Directory.CreateDirectory(directory);
			storage = new DirectoryLogStorage(directory);
		}
		[Fact]
		public void CreatedLogFileIsEnumerated() {
			ILogStorage.ILogFile? metadata;
			using (var stream = storage.CreateLogFile(out metadata)) { }
			Assert.Contains(metadata, storage.EnumerateLogs());
		}
		[Fact]
		public void WrittenLogContentsArePreserved() {
			var content = Enumerable.Range(0, 16).Select(_ => StringGenerator.GenerateRandomString(256)).ToList();
			ILogStorage.ILogFile? metadata;
			using (var writer = new StreamWriter(storage.CreateLogFile(out metadata))) {
				content.ForEach(c => writer.WriteLine(c));
			}
			using (var reader = new StreamReader(metadata.OpenRead())) {
				Assert.Equal(content, reader.EnumerateLines());
			}
		}
	}
}
