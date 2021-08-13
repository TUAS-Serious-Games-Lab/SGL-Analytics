using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SGL.Analytics.Client.Tests {
	public class FileRootDataStoreUnitTest : IDisposable {
		private const string appName = "FileRootDataStoreUnitTest";
		private FileRootDataStore getDS() => new FileRootDataStore(appName);
		public void Dispose() {
			string filename;
			FileRootDataStore temp = getDS();
			filename = temp.StorageFile;
			File.Delete(filename);
		}

		[Fact]
		public void DataDirectoryIsWritable() {
			var store = getDS();
			Assert.True(Directory.Exists(store.DataDirectory));
			var testFile = Path.Combine(store.DataDirectory, "WriteTest.json");
			using (File.Create(testFile)) { }
			Assert.True(File.Exists(testFile));
			File.Delete(testFile);
			Assert.False(File.Exists(testFile));
		}
	}
}
