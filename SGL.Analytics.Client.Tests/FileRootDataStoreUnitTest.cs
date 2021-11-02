using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SGL.Analytics.Client.Tests {
	public class FileRootDataStoreUnitTest {
		private const string appName = "FileRootDataStoreUnitTest";
		private FileRootDataStore getDS() => new FileRootDataStore(appName);

		public FileRootDataStoreUnitTest() {
			cleanUp();
		}

		private void cleanUp() {
			string filename;
			FileRootDataStore temp = getDS();
			filename = temp.StorageFilePath;
			File.Delete(filename);
			File.Delete(Path.ChangeExtension(filename, ".invalid.json"));
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
		[Fact]
		public void UserIdIsInitiallyNull() {
			var store = getDS();
			Assert.Null(store.UserID);
		}
		[Fact]
		public async Task NullUserIdCanBeCorrectlyStored() {
			var store1 = getDS();
			await store1.SaveAsync();
			var store2 = getDS();
			Assert.Null(store2.UserID);
		}
		[Fact]
		public async Task NonNullUserIdCanBeCorrectlyStored() {
			var id = Guid.NewGuid();
			var store1 = getDS();
			store1.UserID = id;
			await store1.SaveAsync();
			var store2 = getDS();
			Assert.Equal(id, store2.UserID);
		}
		[Fact]
		public async Task LoadAsyncCorrectlyLoadsUserIdInplace() {
			var id = Guid.NewGuid();
			var store1 = getDS();
			var store2 = getDS();
			store1.UserID = id;
			await store1.SaveAsync();
			await store2.LoadAsync();
			Assert.Equal(id, store2.UserID);
		}
		[Fact]
		public void EmptyStorageFileIsEquivalentToInitialState() {
			var store1 = getDS();
			using (File.Create(store1.StorageFilePath)) { }
			var store2 = getDS();
			Assert.Null(store2.UserID);
		}
		[Fact]
		public void InvalidStorageFileIsEquivalentToInitialStateAndBackedUp() {
			var store1 = getDS();
			var file = store1.StorageFilePath;
			using (var writer = File.CreateText(file)) {
				writer.WriteLine("This is not valid JSON.");
			}
			var store2 = getDS();
			Assert.Null(store2.UserID);
			Assert.True(File.Exists(Path.ChangeExtension(file, ".invalid.json")));
		}
	}
}
