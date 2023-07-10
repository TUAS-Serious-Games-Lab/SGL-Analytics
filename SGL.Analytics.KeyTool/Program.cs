using System.Runtime.InteropServices;

namespace SGL.Analytics.KeyTool {
	internal static class Program {
		[DllImport("kernel32.dll")]
		static extern IntPtr GetConsoleWindow();
		[DllImport("user32.dll")]
		static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
		const int SW_HIDE = 0;

		/// <summary>
		///  The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main() {
			// Workaround for https://github.com/dotnet/runtime/issues/3828
			var handle = GetConsoleWindow();
			ShowWindow(handle, SW_HIDE);

			// To customize application configuration such as set high DPI settings or default font,
			// see https://aka.ms/applicationconfiguration.
			ApplicationConfiguration.Initialize();
			Application.Run(new MainForm());
		}
	}
}