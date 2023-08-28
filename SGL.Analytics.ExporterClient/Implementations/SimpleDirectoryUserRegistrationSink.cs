using Org.BouncyCastle.Asn1.Cms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient.Implementations {
	/// <summary>
	/// A basic implementation of <see cref="IUserRegistrationSink"/> that writes the user registration data as JSON files into a given directory.
	/// </summary>
	public class SimpleDirectoryUserRegistrationSink : IUserRegistrationSink {
		/// <summary>
		/// The directory path where to write the files.
		/// </summary>
		public string DirectoryPath { get; set; } = "./analytics-users";
		/// <summary>
		/// The prefix string to put in front of the user id when generating the file name for a user registration.
		/// </summary>
		public string FilePrefix { get; set; } = "";
		/// <summary>
		/// The prefix string to put after the user id when generating the file name for a user registration.
		/// </summary>
		public string FileSuffix { get; set; } = ".user.json";
		/// <summary>
		/// The options for writing the JSON files.
		/// </summary>
		public JsonSerializerOptions JsonOptions { get; set; } = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

		/// <summary>
		/// Writes the given <paramref name="userRegistrationData"/> to a file under <see cref="DirectoryPath"/>.
		/// </summary>
		public async Task ProcessUserRegistrationAsync(UserRegistrationData userRegistrationData, CancellationToken ct) {
			await Task.Run(async () => {
				var fileName = $"{FilePrefix}{userRegistrationData.UserId:D}{FileSuffix}";
				var filePath = Path.Combine(DirectoryPath, fileName);
				var dir = Path.GetDirectoryName(filePath);
				if (dir != null) Directory.CreateDirectory(dir);
				using var outputFile = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
				await JsonSerializer.SerializeAsync(outputFile, userRegistrationData, JsonOptions, ct);
			}, ct).ConfigureAwait(false);
		}
	}
}
