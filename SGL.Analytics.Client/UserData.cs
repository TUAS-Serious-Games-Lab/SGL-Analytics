namespace SGL.Analytics.Client {
	public class UserData {
		public string FirstName { get; set; }
		public string LastName { get; set; }

		UserData(string firstName, string lastName) {
			FirstName = firstName;
			LastName = lastName;
		}
	}
}
