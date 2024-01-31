using Microsoft.Extensions.Logging;
using SGL.Analytics.ExporterClient;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.WinForms.Controls.LogGui;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SGL.Analytics.RekeyingTool {
	public partial class MainForm : Form {
		private const string configFileName = "RekeyingTool-Settings.json";
		private CancellationTokenSource? ctsActivity;
		private ILoggerFactory loggerFactory;
		private ILogger logger;
		private RekeyingLogic logic;
		private string prevKeyFilePath;
		private string prevKeyPassphrase;
		private RekeyToolSettings settings;
		private string prevAppname;
		private bool appSelected = false;
		private bool keyLoaded = false;
		private bool updatingDstCertList = false;
		private bool signerLoaded = false;

		public MainForm() {
			InitializeComponent();
			loggerFactory = LoggerFactory.Create(config => config
				.AddProvider(new MessageListLoggingProvider(this.logMessages, LogLevel.Trace))
				.SetMinimumLevel(LogLevel.Trace));
			logger = loggerFactory.CreateLogger(nameof(Program));
			logic = new RekeyingLogic(loggerFactory);
			setUiStateForActivity(false);
			loadSettings();
		}
		private async void loadSettings() {
			using var cts = new CancellationTokenSource();
			ctsActivity = cts;
			var ct = cts.Token;
			try {
				setUiStateForActivity(true);
				if (!File.Exists(configFileName)) {
					try {
						using var configFile = File.Create(configFileName);
						settings = new RekeyToolSettings();
						await JsonSerializer.SerializeAsync(configFile, settings,
							new JsonSerializerOptions(JsonSerializerDefaults.General) { WriteIndented = true });
					}
					catch (Exception ex) {
						logger.LogError("Couldn't create settings file: {msg}", ex.Message);
					}
				}
				else {
					try {
						using var configFile = File.OpenRead(configFileName);
						settings = await JsonSerializer.DeserializeAsync<RekeyToolSettings>(configFile,
							new JsonSerializerOptions(JsonSerializerDefaults.General)) ?? new RekeyToolSettings();
					}
					catch (Exception ex) {
						logger.LogError("Couldn't load settings: {msg}", ex.Message);
						settings = new RekeyToolSettings();
					}
				}
				txtAppName.Text = settings.AppName;
				prevAppname = settings.AppName;
				cmbBackends.Items.AddRange(settings.Backends.Select(kvp => new BackendEntry(kvp.Key, kvp.Value)).ToArray());
				if (cmbBackends.Items.Count > 0) {
					cmbBackends.SelectedIndex = 0;
				}
				else {
					logger.LogError("Config file does not contain backend addresses.");
				}
			}
			catch (OperationCanceledException) { }
			catch (Exception ex) {
				logger.LogError(ex, "Unexpected error: {msg}", ex.Message);
			}
			finally {
				setUiStateForActivity(false);
				cmbBackends.Focus();
			}
		}
		private class BackendEntry {
			public string Name { get; }
			public Uri BaseUri { get; }

			public BackendEntry(string name, Uri baseUri) {
				Name = name;
				BaseUri = baseUri;
			}

			public override string? ToString() {
				return $"{Name} - {BaseUri}";
			}
		}

		private void setUiStateForActivity(bool active) {
			cmbBackends.Enabled = !active;
			txtAppName.Enabled = !active;
			btnBrowseKeyFile.Enabled = !active;
			txtKeyPassphrase.Enabled = !active;
			btnBrowseSignerCert.Enabled = !active;
			lstDstCerts.Enabled = !active;
			radRekeyLogs.Enabled = !active;
			radRekeyUserRegistrations.Enabled = !active;
			btnStart.Enabled = !active;
			btnCancel.Enabled = active;
			progActivity.Visible = active;
		}

		private async void btnBrowseKeyFile_Click(object sender, EventArgs e) {
			if (browseKeyFileDialog.ShowDialog() == DialogResult.OK) {
				lblKeyFilePath.Text = browseKeyFileDialog.FileName;
				await updateSrcKeyInfoAsync();
			}
		}

		private async void updateSrcKeyInfo(object sender, EventArgs e) {
			await updateSrcKeyInfoAsync();
		}
		private async Task updateSrcKeyInfoAsync() {
			await tryLoadKeyFile();
			if (logic.AuthenticationKeyId != null || logic.AuthenticationCertificate != null) {
				lblAuthKeyInfo.Text = $"Key Id: {logic.AuthenticationKeyId}\nCN: {logic.AuthenticationCertificate?.SubjectDN}";
			}
			else {
				lblAuthKeyInfo.Text = "";
			}
			if (logic.DecryptionKeyId != null || logic.DecryptionCertificate != null) {
				lblDecryptionKeyInfo.Text = $"Key Id: {logic.DecryptionKeyId}\nCN: {logic.DecryptionCertificate?.SubjectDN}";
			}
			else {
				lblDecryptionKeyInfo.Text = "";
			}
		}

		private async Task updateDstCertList(CancellationToken ct) {
			if (!signerLoaded) return;
			if (!appSelected) return;
			if (!keyLoaded) return;
			var prevSelectedItem = lstDstCerts.SelectedItem as Certificate;
			CertificateStore certs;
			if (radRekeyLogs.Checked) {
				certs = await logic.LoadLogRecipientCertsAsync(ct);
			}
			else if (radRekeyUserRegistrations.Checked) {
				certs = await logic.LoadUserRegRecipientCertsAsync(ct);
			}
			else {
				lstDstCerts.Items.Clear();
				return;
			}
			lstDstCerts.Items.Clear();
			lstDstCerts.Items.AddRange(certs.ListKnownCertificates().ToArray());
			if (prevSelectedItem != null) {
				var keyId = prevSelectedItem.PublicKey.CalculateId();
				var index = lstDstCerts.Items.Cast<Certificate>().ToList()
					.FindIndex(cert => cert.PublicKey.CalculateId() == keyId && cert.SubjectDN.Equals(prevSelectedItem.SubjectDN));
				if (index >= 0) {
					lstDstCerts.SelectedIndex = index;
				}
			}
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
				logger.LogError(ex, "Error occurred: {msg}", ex.Message);
			}
			finally {
				ctsActivity = null;
				setUiStateForActivity(false);
			}
		}

		private async Task tryLoadKeyFile() {
			if (string.IsNullOrWhiteSpace(browseKeyFileDialog.FileName)) return;
			if (string.IsNullOrWhiteSpace(txtKeyPassphrase.Text)) return;
			if (prevKeyFilePath == browseKeyFileDialog.FileName && prevKeyPassphrase == txtKeyPassphrase.Text) return;
			try {
				using var cts = new CancellationTokenSource();
				ctsActivity = cts;
				var ct = cts.Token;
				setUiStateForActivity(true);
				await logic.LoadKeyFile(browseKeyFileDialog.FileName, () => txtKeyPassphrase.Text.ToCharArray(), ct);
				keyLoaded = true;
				prevKeyFilePath = browseKeyFileDialog.FileName;
				prevKeyPassphrase = txtKeyPassphrase.Text;
				await updateDstCertList(ct);
			}
			catch (OperationCanceledException) { }
			catch (Exception ex) {
				logger.LogError(ex, "Couldn't load key file: {msg}", ex.Message);
			}
			finally {
				setUiStateForActivity(false);
				if (prevKeyFilePath == browseKeyFileDialog.FileName && prevKeyPassphrase == txtKeyPassphrase.Text) {
					txtAppName.Focus();
				}
			}
		}

		private async void txtAppName_Leave(object sender, EventArgs e) {
			if (keyLoaded && (!appSelected || prevAppname != txtAppName.Text)) {
				try {
					using var cts = new CancellationTokenSource();
					ctsActivity = cts;
					var ct = cts.Token;
					setUiStateForActivity(true);
					await logic.SetAppNameAsync(txtAppName.Text, ct);
					prevAppname = txtAppName.Text;
					appSelected = true;
					await updateDstCertList(ct);
				}
				catch (OperationCanceledException) { }
				catch (Exception ex) {
					logger.LogError(ex, "Couldn't set app name: {msg}", ex.Message);
					txtAppName.Text = prevAppname;
				}
				finally {
					setUiStateForActivity(false);
				}
			}
		}

		private async void cmbBackends_SelectedValueChangedAsync(object sender, EventArgs e) {
			var entry = cmbBackends.SelectedItem as BackendEntry;
			if (entry == null) return;
			try {
				logic.BackendBaseUri = entry.BaseUri;
			}
			catch (InvalidOperationException) {
				try {
					using var cts = new CancellationTokenSource();
					ctsActivity = cts;
					var ct = cts.Token;
					logMessages.ClearItems();
					setUiStateForActivity(true);
					await logic.DisposeAsync();
					logic = new RekeyingLogic(loggerFactory);
					logic.BackendBaseUri = entry.BaseUri;
					bool restoreAppName = appSelected || !string.IsNullOrWhiteSpace(txtAppName.Text);
					bool refreshCerts = true;
					prevAppname = "";
					appSelected = false;
					prevKeyFilePath = "";
					prevKeyPassphrase = "";
					keyLoaded = false;
					if (!string.IsNullOrWhiteSpace(browseKeyFileDialog.FileName) && !string.IsNullOrWhiteSpace(txtKeyPassphrase.Text)) {
						await logic.LoadKeyFile(browseKeyFileDialog.FileName, () => txtKeyPassphrase.Text.ToCharArray(), ct);
						keyLoaded = true;
						prevKeyFilePath = browseKeyFileDialog.FileName;
						prevKeyPassphrase = txtKeyPassphrase.Text;
					}
					else {
						refreshCerts = false;
					}
					if (restoreAppName) {
						await logic.SetAppNameAsync(txtAppName.Text, ct);
						prevAppname = txtAppName.Text;
						appSelected = true;
					}
					else {
						refreshCerts = false;
					}
					if (refreshCerts) {
						await updateDstCertList(ct);
					}
				}
				catch (OperationCanceledException) { }
				catch (Exception ex) {
					logger.LogError(ex, "Error while renewing client for new backend: {msg}", ex.Message);
				}
				finally {
					setUiStateForActivity(false);
				}
			}
		}

		private async void btnBrowseSignerCert_Click(object sender, EventArgs e) {
			if (browseSignerCertFileDialog.ShowDialog() == DialogResult.OK) {
				lblSignerCertPath.Text = browseSignerCertFileDialog.FileName;
				try {
					using var cts = new CancellationTokenSource();
					ctsActivity = cts;
					var ct = cts.Token;
					setUiStateForActivity(true);
					await logic.LoadSignerCertificateAsync(lblSignerCertPath.Text, settings.IgnoreSignerValidityPeriod, ct);
					signerLoaded = true;
					await updateDstCertList(ct);
				}
				catch (OperationCanceledException) { }
				catch (Exception ex) {
					logger.LogError(ex, "Error while updating signer certificate: {msg}", ex.Message);
				}
				finally {
					setUiStateForActivity(false);
				}
			}
		}

		private async void updateDstCertList(object sender, EventArgs e) {
			if (updatingDstCertList) return;
			try {
				updatingDstCertList = true;
				setUiStateForActivity(true);
				using var cts = new CancellationTokenSource();
				ctsActivity = cts;
				var ct = cts.Token;
				await updateDstCertList(ct);
			}
			catch (OperationCanceledException) { }
			catch (Exception ex) {
				logger.LogError(ex, "Error updating certificate list: {msg}", ex.Message);
			}
			finally {
				updatingDstCertList = false;
				ctsActivity = null;
				setUiStateForActivity(false);
			}
		}

		private void lstDstCerts_Format(object sender, ListControlConvertEventArgs e) {
			var cert = e.ListItem as Certificate;
			if (cert != null) {
				e.Value = $"{cert.PublicKey.CalculateId()} {cert.SubjectDN}";
			}
			else {
				e.Value = "[error: unexpected type]";
			}
		}

		private void btnClearLog_Click(object sender, EventArgs e) {
			logMessages.ClearItems();
		}
	}
}
