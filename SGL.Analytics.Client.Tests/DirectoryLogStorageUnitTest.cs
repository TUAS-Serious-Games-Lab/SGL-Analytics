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
			Random rnd = new Random();
			var characters = Enumerable.Range('A', 26).Concat(Enumerable.Range('a', 26)).Concat(Enumerable.Range('0', 10)).Append(' ').Select(c => (char)c).ToArray();
			var content = Enumerable.Range(0, 16).Select(_ => new string(Enumerable.Range(0, 256).Select(_ => characters[rnd.Next(characters.Length)]).ToArray())).ToList();
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
