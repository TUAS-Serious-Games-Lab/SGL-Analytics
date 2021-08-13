using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	public class FileRootDataStore : IRootDataStore {
		private struct StorageStructure {
			public Guid? UserID;
		}
		private StorageStructure storage;
		string appName;
		JsonSerializerOptions jsonOptions = new JsonSerializerOptions() {
			WriteIndented = true
		};

		public Guid? UserID { get => storage.UserID; set => storage.UserID = value; }

		public FileRootDataStore(string appName) {
			this.appName = appName;
			LoadAsync().Wait();
		}

		public async Task LoadAsync() {
			var file = StorageFile;
			if (!File.Exists(file)) {
				// No saved state found, use default state.
				return;
			}
			await using (var storageFileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize: 4096, useAsync: true)) {
				storage = await JsonSerializer.DeserializeAsync<StorageStructure>(storageFileStream, jsonOptions);
			}
		}

		public string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);

		public string StorageFile => Path.Combine(DataDirectory, "AppDataStore.json");

		public async Task SaveAsync() {
			await using (var storageFileStream = new FileStream(StorageFile, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize: 4096, useAsync: true)) {
				await JsonSerializer.SerializeAsync<StorageStructure>(storageFileStream, storage, jsonOptions);
			}
		}
	}
}
