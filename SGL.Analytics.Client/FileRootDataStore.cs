using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	public class FileRootDataStore : IRootDataStore {
		string appName;

		public Guid? UserID { get; set; }

		public string DataDirectory => throw new NotImplementedException();

		public FileRootDataStore(string appName) {
			this.appName = appName;
		}

		public Task SaveAsync() {
			throw new NotImplementedException();
		}
	}
}
