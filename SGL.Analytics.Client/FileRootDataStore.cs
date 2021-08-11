using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	public class FileRootDataStore : IRootDataStore {
		public Guid? UserID { get; set; }

		public string GetDataDirectory(string appID) {
			throw new NotImplementedException();
		}

		public Task SaveAsync() {
			throw new NotImplementedException();
		}
	}
}
