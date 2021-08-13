using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SGL.Analytics.Client.Tests {
	public class FileRootDataStoreUnitTest : IDisposable {
		private const string appName = "FileRootDataStoreUnitTest";
		private FileRootDataStore getDS() => new FileRootDataStore(appName);
		public void Dispose() {
			string filename;
			FileRootDataStore temp = getDS();
			filename = temp.StorageFile;
			File.Delete(filename);
		}


	}
}
