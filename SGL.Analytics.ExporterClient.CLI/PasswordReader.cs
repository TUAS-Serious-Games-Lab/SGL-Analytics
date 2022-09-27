namespace SGL.Analytics.ExporterClient.CLI {
	public class PasswordReader {
		public static char[] PromptPassword(string prompt) {
			Console.Write(prompt);
			Console.Write(": ");
			char[] password = new char[0];
			ConsoleKeyInfo keyInfo;
			do {
				keyInfo = Console.ReadKey(intercept: true);
				if (keyInfo.Key == ConsoleKey.Backspace && password.Length > 0) {
					password = password.SkipLast(1).ToArray();
					Console.Write("\b \b");
				}
				else if (!char.IsControl(keyInfo.KeyChar)) {
					password = password.Append(keyInfo.KeyChar).ToArray();
					Console.Write('*');
				}
			}
			while (keyInfo.Key != ConsoleKey.Enter);
			Console.WriteLine();
			return password;
		}
	}
}
