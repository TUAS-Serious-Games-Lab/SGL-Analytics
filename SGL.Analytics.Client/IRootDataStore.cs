using System;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	public interface IRootDataStore {
		Guid? UserID { get; set; }

		string GetDataDirectory(string appID);

		Task SaveAsync();
	}
}