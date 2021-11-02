using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	public class FileRootDataStore : IRootDataStore {
		private struct StorageStructure {
			public Guid? UserID { get; set; }
			public string UserSecret { get; set; }
		}
		private StorageStructure storage;
		string appName;
		JsonSerializerOptions jsonOptions = new JsonSerializerOptions() {
			WriteIndented = true
		};

		public Guid? UserID { get => storage.UserID; set => storage.UserID = value; }
		public string UserSecret { get => storage.UserSecret; set => storage.UserSecret = value; }

		public FileRootDataStore(string appName, string? storageFileName = null) {
			this.appName = appName;
			if (storageFileName != null) StorageFileName = storageFileName;
			Directory.CreateDirectory(DataDirectory);
			Task.Run(async () => await LoadAsync()).GetAwaiter().GetResult();
		}

		public async Task LoadAsync() {
			var file = StorageFilePath;
			if (!File.Exists(file) || (new FileInfo(file)).Length == 0) {
				// No saved state found, use default state.
				return;
			}
			try {
				await using (var storageFileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true)) {
					storage = await JsonSerializer.DeserializeAsync<StorageStructure>(storageFileStream, jsonOptions);
				}
			}
			catch (JsonException) {
				File.Move(file, Path.ChangeExtension(file, ".invalid.json"), overwrite: true);
			}
		}

		public string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);

		public string StorageFileName { get; set; } = "SGLAnalytics_AppDataStore.json";
		public string StorageFilePath => Path.Combine(DataDirectory, StorageFileName);

		public async Task SaveAsync() {
			await using (var storageFileStream = new FileStream(StorageFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true)) {
				await JsonSerializer.SerializeAsync<StorageStructure>(storageFileStream, storage, jsonOptions);
			}
		}
	}
}
