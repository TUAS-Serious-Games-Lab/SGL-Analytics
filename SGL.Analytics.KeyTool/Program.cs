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
			try {
				Application.ThreadException += Application_ThreadException;
				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				// Workaround for https://github.com/dotnet/runtime/issues/3828
				var handle = GetConsoleWindow();
				ShowWindow(handle, SW_HIDE);
				// To customize application configuration such as set high DPI settings or default font,
				// see https://aka.ms/applicationconfiguration.
				ApplicationConfiguration.Initialize();
				Application.Run(new MainForm());
			}
			catch (Exception ex) {
				try {
					File.AppendAllText("SGL.Analytics.KeyTool.err", $"Error: {ex.Message}\nDetails:\n{ex}");
				}
				catch { }
				MessageBox.Show($"Error: {ex.Message}\nDetails:\n{ex}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
			try {
				File.AppendAllText("SGL.Analytics.KeyTool.err", $"Application Domain Error: {(e.ExceptionObject as Exception)?.Message}\nDetails:\n{e.ExceptionObject}");
			}
			catch { }
			MessageBox.Show($"Application Domain Error: {(e.ExceptionObject as Exception)?.Message}\nDetails:\n{e.ExceptionObject}", "Application Domain Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}

		private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e) {
			try {
				File.AppendAllText("SGL.Analytics.KeyTool.err", $"Application Error:{e.Exception.Message}\nDetails:\n{e.Exception}");
			}
			catch { }
			MessageBox.Show($"Application Error:{e.Exception.Message}\nDetails:\n{e.Exception}", "Application Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}
}