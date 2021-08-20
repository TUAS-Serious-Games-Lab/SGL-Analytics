using System;
using System.IO;
using System.Threading.Tasks;

namespace SGL.Analytics.Client.Tests {
	public class FakeRootDataStore : IRootDataStore {
		public Guid? UserID { get; set; } = Guid.NewGuid();

		public string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SGL.Analytics.Client.Tests.FakeRootDataStore");

		public Task SaveAsync() {
			return Task.CompletedTask;
		}
	}
}
