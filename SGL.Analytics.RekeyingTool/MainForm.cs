using Microsoft.Extensions.Logging;
using SGL.Utilities.WinForms.Controls.LogGui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SGL.Analytics.RekeyingTool {
	public partial class MainForm : Form {
		private CancellationTokenSource? ctsActivity;
		private ILoggerFactory loggerFactory;
		private ILogger logger;

		public MainForm() {
			InitializeComponent();
			loggerFactory = LoggerFactory.Create(config => config.AddProvider(new MessageListLoggingProvider(this.logMessages, LogLevel.Trace)));
			logger = loggerFactory.CreateLogger(nameof(Program));
			setUiStateForActivity(false);
		}

		private void setUiStateForActivity(bool active) {
			cmbBackendSystem.Enabled = !active;
			txtAppName.Enabled = !active;
			btnBrowseKeyFile.Enabled = !active;
			txtKeyPassphrase.Enabled = !active;
			lstDstCerts.Enabled = !active;
			chkRekeyLogs.Enabled = !active;
			chkRekeyUserRegistrations.Enabled = !active;
			btnStart.Enabled = !active;
			btnCancel.Enabled = active;
			progActivity.Visible = active;
		}

		private void onChangeDataRepository(object sender, EventArgs e) {

		}

		private void btnBrowseKeyFile_Click(object sender, EventArgs e) {
			// TODO: Browse
			updateSrcKeyInfo();
		}

		private void updateSrcKeyInfo(object sender, EventArgs e) {
			updateSrcKeyInfo();
		}
		private void updateSrcKeyInfo() {

		}

		private void btnCancel_Click(object sender, EventArgs e) {
			ctsActivity?.Cancel();
		}

		private async void btnStart_Click(object sender, EventArgs e) {
			try {
				setUiStateForActivity(true);
				using var cts = new CancellationTokenSource();
				ctsActivity = cts;
				var ct = cts.Token;
				while (true) {
					await Task.Delay(100);
					cts.Token.ThrowIfCancellationRequested();
				}
			}
			catch (OperationCanceledException) { }
			catch (Exception ex) {
				logger.LogError(ex, "Error occurred.");
			}
			finally {
				ctsActivity = null;
				setUiStateForActivity(false);
			}
		}
	}
}
