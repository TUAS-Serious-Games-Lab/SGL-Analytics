using SGL.Utilities;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SGL.Analytics.Client.Tests {
	public class FakeRootDataStore : IRootDataStore {
		public Guid? UserID { get; set; } = Guid.NewGuid();
		public string? UserSecret { get; set; } = SecretGenerator.Instance.GenerateSecret(16);

		public string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SGL.Analytics.Client.Tests.FakeRootDataStore");

		public string? Username { get; set; } = null;

		public Task SaveAsync() {
			return Task.CompletedTask;
		}
	}
}
