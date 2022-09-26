using Org.BouncyCastle.Asn1.Cms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient.Implementations {
	public class SimpleDirectoryUserRegistrationSink : IUserRegistrationSink {
		public string DirectoryPath { get; set; } = "./analytics-users";
		public string FilePrefix { get; set; } = "";
		public string FileSuffix { get; set; } = ".user.json";
		public JsonSerializerOptions JsonOptions { get; set; } = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

		public async Task ProcessUserRegistrationAsync(UserRegistrationData userRegistrationData, CancellationToken ct) {
			await Task.Run(async () => {
				var fileName = $"{FilePrefix}{userRegistrationData.UserId:D}{FileSuffix}";
				var filePath = Path.Combine(DirectoryPath, fileName);
				var dir = Path.GetDirectoryName(filePath);
				if (dir != null) Directory.CreateDirectory(dir);
				using var outputFile = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
				await JsonSerializer.SerializeAsync(outputFile, userRegistrationData, JsonOptions, ct);
			}, ct);
		}
	}
}
