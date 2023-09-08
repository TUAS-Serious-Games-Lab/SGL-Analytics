using Org.BouncyCastle.Crypto;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System.Text.Json;

namespace SGL.Analytics.KeyTool {
	public partial class MainForm : Form {
		private KeyToolSettings settings;
		private const string configFile = "KeyTool-Settings.json";
		private int rsaKeyStrength;
		private List<DistinguishedNameEntryEdit> dnEntryEdits = null!;
		private string? keyGenPassphrase = null;
		private List<CertificateSigningRequest> loadedCsrs = new List<CertificateSigningRequest>();

		public MainForm() {
			InitializeComponent();
			settings = LoadSettings();
			cmbNamedCurve.Items.Clear();
			cmbNamedCurve.Items.AddRange(KeyPair.GetSupportedNamedEllipticCurves().OrderByDescending(curve => curve.KeyLength).Select(curve => curve.Name).ToArray());
			var defaultCurveIndex = cmbNamedCurve.Items.IndexOf(settings.DefaultCurveName);
			if (defaultCurveIndex >= 0) {
				cmbNamedCurve.SelectedIndex = defaultCurveIndex;
			}
			FillDistinguishedNameEntries(settings.InitialDistinguishedName);
			spinRsaKeyStrengthExp_ValueChanged(spinRsaKeyStrengthExp, EventArgs.Empty);
			dtpValidTo.Value = DateTime.UtcNow.AddYears(settings.DefaultValidityYears);
		}

		private KeyToolSettings LoadSettings() {
			if (!File.Exists(configFile)) {
				try {
					using var configFile = File.Create(MainForm.configFile);
					var settings = new KeyToolSettings();
					JsonSerializer.Serialize(configFile, settings,
						new JsonSerializerOptions(JsonSerializerDefaults.General) { WriteIndented = true });
					return settings;
				}
				catch { }
			}
			try {
				using var configFile = File.OpenRead(MainForm.configFile);
				var settings = JsonSerializer.Deserialize<KeyToolSettings>(configFile, new JsonSerializerOptions(JsonSerializerDefaults.General));
				return settings ?? new KeyToolSettings();
			}
			catch (Exception ex) {
				MessageBox.Show($"Couldn't load settings:\n{ex.Message}", "Error loading settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
				var settings = new KeyToolSettings();
				return settings;
			}
		}

		private void FillDistinguishedNameEntries(List<KeyToolSettings.DistinguishedNameComponent> entries) {
			dnEntryEdits = entries.Select(entry => new DistinguishedNameEntryEdit {
				TypeCode = entry.Type,
				Value = entry.Value
			}).ToList();
			flowCsrDnFields.Controls.AddRange(dnEntryEdits.ToArray());
			flowCsrDnFields_SizeChanged(flowCsrDnFields, EventArgs.Empty);
		}

		private void spinRsaKeyStrengthExp_ValueChanged(object sender, EventArgs e) {
			rsaKeyStrength = 1 << (int)(spinRsaKeyStrengthExp.Value);
			lblRsaKeyStrength.Text = $"{rsaKeyStrength}";
		}

		private void flowCsrDnFields_SizeChanged(object sender, EventArgs e) {
			foreach (var edit in dnEntryEdits) {
				edit.Left = 5;
				edit.Width = flowCsrDnFields.ClientSize.Width - 10;
			}
		}

		private void btnBrowseSaveIntermediateKeyFile_Click(object sender, EventArgs e) {
			switch (saveIntermediateKeyFileDialog.ShowDialog()) {
				case DialogResult.OK:
					lblIntermediateKeySavePath.Text = saveIntermediateKeyFileDialog.FileName;
					break;
				case DialogResult.Cancel:
					break;
			}
		}

		private void btnBrowseCsrOutputFile_Click(object sender, EventArgs e) {
			switch (saveCsrFileDialog.ShowDialog()) {
				case DialogResult.OK:
					lblCsrOutputFile.Text = saveCsrFileDialog.FileName;
					break;
				case DialogResult.Cancel:
					break;
			}
		}

		private void btnAddCsrDnInput_Click(object sender, EventArgs e) {
			dnEntryEdits.Add(new DistinguishedNameEntryEdit());
			flowCsrDnFields.Controls.Clear();
			flowCsrDnFields.Controls.AddRange(dnEntryEdits.ToArray());
			flowCsrDnFields_SizeChanged(flowCsrDnFields, EventArgs.Empty);
		}

		private void btnRemoveCsrDnInput_Click(object sender, EventArgs e) {
			if (dnEntryEdits.Count < 1) return;
			dnEntryEdits.RemoveAt(dnEntryEdits.Count - 1);
			flowCsrDnFields.Controls.Clear();
			flowCsrDnFields.Controls.AddRange(dnEntryEdits.ToArray());
			flowCsrDnFields_SizeChanged(flowCsrDnFields, EventArgs.Empty);
		}

		private void UpdatePassphraseState() {
			if (txtPassphrase.Text.Length < settings.MinPassphraseLength) {
				lblPassphrase.BackColor = Color.Red;
				lblKeyGenStatus.Text = "Passphrase too short!";
				lblKeyGenStatus.BackColor = Color.Red;
				keyGenPassphrase = null;
				return;
			}
			else {
				lblPassphrase.BackColor = Color.Transparent;
			}
			if (txtPassphrase.Text != txtRepeatPassphrase.Text) {
				lblRepeatPassphrase.BackColor = Color.Red;
				lblKeyGenStatus.Text = "Passphrase repetition doesn't match";
				lblKeyGenStatus.BackColor = Color.Red;
				keyGenPassphrase = null;
				return;
			}
			else {
				lblRepeatPassphrase.BackColor = Color.Transparent;
			}
			lblKeyGenStatus.Text = "";
			lblKeyGenStatus.BackColor = Color.Transparent;
			keyGenPassphrase = txtPassphrase.Text;
		}

		private void txtPassphrase_TextChanged(object sender, EventArgs e) {
			UpdatePassphraseState();
		}

		private void txtRepeatPassphrase_TextChanged(object sender, EventArgs e) {
			UpdatePassphraseState();
		}

		private async void btnGenerateKeyAndCsr_Click(object sender, EventArgs e) {
			if (keyGenPassphrase == null) {
				lblKeyGenStatus.Text = "Please enter a passphrase!";
				lblPassphrase.BackColor = Color.Red;
				lblKeyGenStatus.BackColor = Color.Red;
				return;
			}
			if (string.IsNullOrEmpty(lblIntermediateKeySavePath.Text)) {
				lblKeyGenStatus.Text = "Please select intermediate key file path!";
				lblKeyGenStatus.BackColor = Color.Red;
				return;
			}
			if (string.IsNullOrEmpty(lblCsrOutputFile.Text)) {
				lblKeyGenStatus.Text = "Please select certificate signing request file path!";
				lblKeyGenStatus.BackColor = Color.Red;
				return;
			}
			lblKeyGenStatus.Text = "";
			lblKeyGenStatus.BackColor = Color.Transparent;
			var intermediateKeyPath = lblIntermediateKeySavePath.Text;
			var csrOutputPath = lblCsrOutputFile.Text;
			if (File.Exists(intermediateKeyPath)) {
				if (MessageBox.Show("The intermediate key output file already exists.\nShould this file be overwritten?\n" +
					"Please make SURE this isn't an important key file!",
					"Overwrite key file?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) {
					return;
				}
			}
			if (File.Exists(csrOutputPath)) {
				if (MessageBox.Show("The certificate signing request output file already exists.\nShould this file be overwritten?\n",
					"Overwrite CSR file?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) {
					return;
				}
			}
			var passphrase = keyGenPassphrase;
			bool storeUnencrypted = false;
			if (passphrase.Length == 0) {
				if (MessageBox.Show("No passphrase for the private key was given, but the minimum length allows an empty passphrase.\n" +
						"Shall the private key be saved in unencrypted form?\n" +
						"WARNING: Storing private keys without a passphrase is NOT RECOMENDED!\n" +
						"Only proceed if you are sure you want an unencrypted key, e.g. for testing purposes.",
						"WARNING: Unencrypted Private Key?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
						MessageBoxDefaultButton.Button2) == DialogResult.Yes) {
					storeUnencrypted = true;
				}
				else {
					return;
				}
			}
			btnGenerateKeyAndCsr.Enabled = false;
			var isSignerCert = chkGenerateSigner.Checked;
			var keyType = (tabsKeyType.SelectedTab == tabEllipticCurve) ? KeyType.EllipticCurves : KeyType.RSA;
			var ellipticCurveName = cmbNamedCurve.SelectedItem as string;
			var selectedRsaKeyStrength = rsaKeyStrength;
			var csrDn = new DistinguishedName(dnEntryEdits.Select(entry => new KeyValuePair<string, string>(entry.TypeCode ?? "", entry.Value)));
			lblKeyGenStatus.Text = "Generating ...";
			lblKeyGenStatus.BackColor = Color.Yellow;
			progBarKeyGen.Value = 0;
			progBarKeyGen.Style = ProgressBarStyle.Marquee;
			var keyGenTask = Task.Run(async () => {
				await GenerateKeyAndCsr(intermediateKeyPath, csrOutputPath, isSignerCert, keyType, ellipticCurveName, selectedRsaKeyStrength, passphrase, csrDn, storeUnencrypted);
			});
			try {
				await keyGenTask;
				lblKeyGenStatus.Text = "Successfully created key and CSR files.";
				lblKeyGenStatus.BackColor = Color.Transparent;
				progBarKeyGen.Value = 100;
				progBarKeyGen.Style = ProgressBarStyle.Continuous;
				btnGenerateKeyAndCsr.Enabled = true;
			}
			catch (Exception ex) {
				lblKeyGenStatus.Text = "Error: " + ex.Message;
				lblKeyGenStatus.BackColor = Color.Red;
				progBarKeyGen.Value = 0;
				progBarKeyGen.Style = ProgressBarStyle.Continuous;
				btnGenerateKeyAndCsr.Enabled = true;
			}
		}

		private void lstInputCsrs_SelectedIndexChanged(object sender, EventArgs e) {
			if (lstInputCsrs.SelectedIndex < 0) return;
			switch (lstInputCsrs.SelectedItem) {
				case CertificateSigningRequest csr:
					lblCsrKeyId.Text = csr?.SubjectPublicKey?.CalculateId()?.ToString() ?? "";
					lblCsrDn.Text = csr?.SubjectDN.ToString() ?? "";
					lblCsrKeyUsages.Text = csr?.RequestedKeyUsages.GetValueOrDefault(KeyUsages.NoneDefined).ToString("G");
					lblCsrBasicConstraints.Text = csr?.RequestedCABasicConstraints?.ToString() ?? "";
					break;
				default:
					lblCsrKeyId.Text = "[Invalid CSR]";
					lblCsrDn.Text = "[Invalid CSR]";
					lblCsrKeyUsages.Text = "[Invalid CSR]";
					lblCsrBasicConstraints.Text = "[Invalid CSR]";
					break;
			}
		}

		private void btnBrowseCsrInputFile_Click(object sender, EventArgs e) {
			if (openCsrInputFileDialog.ShowDialog() == DialogResult.OK) {
				try {
					using var csrInputFile = File.OpenText(openCsrInputFileDialog.FileName);
					var tmpLoadedCsrs = CertificateSigningRequest.LoadAllFromPem(csrInputFile).ToList();
					var checkedCsrs = tmpLoadedCsrs.Select(csr => {
						var res = csr.Verify();
						return (csr, valid: res == CertificateCheckOutcome.Valid);
					}).ToList();
					loadedCsrs = checkedCsrs.Where(entry => entry.valid).Select(entry => entry.csr).ToList();
					lstInputCsrs.Items.Clear();
					lstInputCsrs.Items.AddRange(checkedCsrs.Select(entry =>
						entry.valid ? (object)entry.csr : "INVALID SIGNATURE: " + entry.csr.ToString() ?? "").ToArray());
					lstInputCsrs.SelectedIndex = 0;
					lblCsrInputFile.Text = openCsrInputFileDialog.FileName;
					dtpValidTo.Value = DateTime.UtcNow.AddYears(settings.DefaultValidityYears);
				}
				catch (Exception ex) {
					MessageBox.Show("Error while loading CSR file:\n" + ex.Message, "Error loading CSR", MessageBoxButtons.OK, MessageBoxIcon.Error);
					loadedCsrs = new List<CertificateSigningRequest>();
					lstInputCsrs.Items.Clear();
					lblCsrInputFile.Text = "";
				}
			}
		}

		private void btnBrowseSignerCertFile_Click(object sender, EventArgs e) {
			if (openSignerCertFileDialog.ShowDialog() == DialogResult.OK) {
				lblSignerCaCertPath.Text = openSignerCertFileDialog.FileName;
			}
		}

		private void btnBrowseSignerPrivateKey_Click(object sender, EventArgs e) {
			if (openSignerKeyFileDialog.ShowDialog() == DialogResult.OK) {
				lblSignerPrivateKeyPath.Text = openSignerKeyFileDialog.FileName;
			}
		}

		private void btnBrowseCertificateOutputPath_Click(object sender, EventArgs e) {
			if (saveCertFileDialog.ShowDialog() == DialogResult.OK) {
				lblCertificateOutputPath.Text = saveCertFileDialog.FileName;
			}
		}

		private async void btnSignCert_Click(object sender, EventArgs e) {
			if (loadedCsrs.Count == 0) {
				lblSignatureStatus.Text = "No certificate signing request loaded!";
				lblSignatureStatus.BackColor = Color.Red;
				return;
			}
			if (string.IsNullOrEmpty(lblSignerCaCertPath.Text) && !chkSelfSign.Checked) {
				lblSignatureStatus.Text = "No CA certificate specified!";
				lblSignatureStatus.BackColor = Color.Red;
				return;
			}
			if (string.IsNullOrEmpty(lblSignerPrivateKeyPath.Text)) {
				lblSignatureStatus.Text = "No CA private key specified!";
				lblSignatureStatus.BackColor = Color.Red;
				return;
			}
			if (string.IsNullOrEmpty(lblCertificateOutputPath.Text)) {
				lblSignatureStatus.Text = "Please specify output path!";
				lblSignatureStatus.BackColor = Color.Red;
				return;
			}
			lblSignatureStatus.BackColor = Color.Transparent;
			lblSignatureStatus.Text = "";
			var certOutputPath = lblCertificateOutputPath.Text;
			if (File.Exists(certOutputPath)) {
				if (MessageBox.Show("The certificate output file already exists.\nShould this file be overwritten?\n",
					"Overwrite certificate file?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) {
					return;
				}
			}
			var csrs = loadedCsrs;
			var signerCaCertPath = lblSignerCaCertPath.Text;
			var signerPrivateKeyPath = lblSignerPrivateKeyPath.Text;
			var signerPassphrase = txtSignerPassphrase.Text.ToCharArray();
			var validToDate = dtpValidTo.Value;
			var selfSign = chkSelfSign.Checked;
			var allowSignerCert = chkAllowSignerCert.Checked;
			btnSignCert.Enabled = false;
			lblSignatureStatus.Text = "Signing ...";
			lblSignatureStatus.BackColor = Color.Yellow;
			var signingTask = Task.Run(async () => {
				await SignCertificates(csrs, signerCaCertPath, signerPrivateKeyPath, signerPassphrase, validToDate, allowSignerCert, selfSign, certOutputPath);
			});
			try {
				await signingTask;
				lblSignatureStatus.Text = "Successfully signed certificate.";
				lblSignatureStatus.BackColor = Color.Transparent;
				btnSignCert.Enabled = true;
			}
			catch (PemException ex) when (ex.InnerException is InvalidCipherTextException iex) {
				lblSignatureStatus.Text = $"Error decrypting PEM objects, is the Passphrase correct?";
				lblSignatureStatus.BackColor = Color.Red;
				btnSignCert.Enabled = true;
			}
			catch (Exception ex) {
				lblSignatureStatus.Text = $"Error ({ex.GetType().Name}): " + ex.Message;
				lblSignatureStatus.BackColor = Color.Red;
				btnSignCert.Enabled = true;
			}
		}

		private void btnBrowseOpenIntermediateKeyFile_Click(object sender, EventArgs e) {
			if (openIntermediateKeyFileDialog.ShowDialog() == DialogResult.OK) {
				lblIntermediateKeyLoadPath.Text = openIntermediateKeyFileDialog.FileName;
			}
		}

		private void btnBrowseCertificateInputFile_Click(object sender, EventArgs e) {
			if (openCertFileDialog.ShowDialog() == DialogResult.OK) {
				lblCertificateInputPath.Text = openCertFileDialog.FileName;
			}
		}

		private void btnBrowseOutputKeyFile_Click(object sender, EventArgs e) {
			if (saveKeyFileDialog.ShowDialog() == DialogResult.OK) {
				lblKeyFileOutputPath.Text = saveKeyFileDialog.FileName;
			}
		}

		private async void btnBuildKeyFile_Click(object sender, EventArgs e) {
			if (string.IsNullOrEmpty(lblIntermediateKeyLoadPath.Text)) {
				lblCombineStatus.Text = "No intermediate key file specified!";
				lblCombineStatus.BackColor = Color.Red;
				return;
			}
			if (string.IsNullOrEmpty(lblCertificateInputPath.Text)) {
				lblCombineStatus.Text = "No certificate file specified!";
				lblCombineStatus.BackColor = Color.Red;
				return;
			}
			if (string.IsNullOrEmpty(lblKeyFileOutputPath.Text)) {
				lblCombineStatus.Text = "No output path specified!";
				lblCombineStatus.BackColor = Color.Red;
				return;
			}
			lblCombineStatus.BackColor = Color.Transparent;
			lblCombineStatus.Text = "";
			var intermediateKeyLoadPath = lblIntermediateKeyLoadPath.Text;
			var certificateInputPath = lblCertificateInputPath.Text;
			var keyFilePassphrase = txtKeyFilePassphrase.Text.ToCharArray();
			var keyFileOutputPath = lblKeyFileOutputPath.Text;
			btnBuildKeyFile.Enabled = false;
			lblCombineStatus.Text = "Building ...";
			lblCombineStatus.BackColor = Color.Yellow;
			var signingTask = Task.Run(async () => {
				await BuildKeyFile(intermediateKeyLoadPath, certificateInputPath, keyFilePassphrase, keyFileOutputPath);
			});
			try {
				await signingTask;
				lblCombineStatus.Text = "Successfully built key file.";
				lblCombineStatus.BackColor = Color.Transparent;
				btnBuildKeyFile.Enabled = true;
			}
			catch (Exception ex) {
				lblCombineStatus.Text = "Error: " + ex.Message;
				lblCombineStatus.BackColor = Color.Red;
				btnBuildKeyFile.Enabled = true;
			}
		}

		private void chkSelfSign_CheckedChanged(object sender, EventArgs e) {
			lblSignerCaCertPath.BackColor = chkSelfSign.Checked ? Color.DarkGray : Color.Transparent;
			btnBrowseSignerCertFile.Enabled = !chkSelfSign.Checked;
			chkAllowSignerCert.Checked = chkSelfSign.Checked;
			chkAllowSignerCert.Enabled = !chkSelfSign.Checked;
		}
	}
}