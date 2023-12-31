using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {

	/// <summary>
	/// An implementation of <see cref="IRootDataStore"/> that stores the user data in a JSON file in an application directory under the user data directory indicated by <see cref="Environment.SpecialFolder.ApplicationData"/>.
	/// </summary>
	public class FileRootDataStore : IRootDataStore {
		private struct StorageStructure {
			public Guid? UserID { get; set; }
			public string? UserSecret { get; set; }
			public string? Username { get; internal set; }
		}
		private StorageStructure storage;
		JsonSerializerOptions jsonOptions = new JsonSerializerOptions() {
			WriteIndented = true
		};

		/// <inheritdoc/>
		public Guid? UserID { get => storage.UserID; set => storage.UserID = value; }
		/// <inheritdoc/>
		public string? UserSecret { get => storage.UserSecret; set => storage.UserSecret = value; }
		/// <inheritdoc/>
		public string? Username { get => storage.Username; set => storage.Username = value; }

		/// <summary>
		/// Constructs a <see cref="FileRootDataStore"/> using the givven application name to generate the application user data directory path.
		/// If a user data file is present, its contents are loaded into the properties.
		/// Optionally, a custom file name for the data file whithin the directory can be given. By default, the filename <c>"SGLAnalytics_AppDataStore.json"</c> is used.
		/// </summary>
		/// <param name="dataDirectory">The user data directory path where to store the application data.</param>
		/// <param name="storageFileName">Overrides the filename to use.</param>
		public FileRootDataStore(string dataDirectory, string? storageFileName = null) {
			DataDirectory = dataDirectory;
			if (storageFileName != null) StorageFileName = storageFileName;
			Directory.CreateDirectory(dataDirectory);
			Task.Run(async () => await LoadAsync()).GetAwaiter().GetResult();
		}

		/// <summary>
		/// Asynchronously (re-)loads the user data from disk, overwriting unsafed changes in memory.
		/// This is automatically called and waited-for by the constructor to initially load the data file from a previous run.
		/// If the data file does not exist, e.g. because this is the first run, this function returns without an error and leaves the properties with their initial value.
		/// </summary>
		/// <returns>A task object representing the load operation.</returns>
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
				string backupFileName = Path.ChangeExtension(file, ".invalid.json");
				try {
#if NETCOREAPP3_0_OR_GREATER
					File.Move(file, backupFileName, overwrite: true);
#else
					File.Delete(backupFileName);
					File.Move(file, backupFileName);
#endif
				}
				catch (Exception) {
					File.Delete(file);
				}
			}
		}

		/// <summary>
		/// Gets the path of the directory that contains the user data file.
		/// </summary>
		public string DataDirectory { get; }

		/// <summary>
		/// Gets or sets the name of the file within <see cref="DataDirectory"/> to use for the user data.
		/// </summary>
		public string StorageFileName { get; set; } = "SGLAnalytics_AppDataStore.json";

		/// <summary>
		/// Gets the full path of the file containing the user data.
		/// </summary>
		public string StorageFilePath => Path.Combine(DataDirectory, StorageFileName);

		/// <summary>
		/// Asynchronously saves the current values of the user data to disk to make them persistent.
		/// </summary>
		/// <returns>A task representing the store operation.</returns>
		public async Task SaveAsync() {
			await using (var storageFileStream = new FileStream(StorageFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true)) {
				await JsonSerializer.SerializeAsync<StorageStructure>(storageFileStream, storage, jsonOptions);
			}
		}
	}
}
