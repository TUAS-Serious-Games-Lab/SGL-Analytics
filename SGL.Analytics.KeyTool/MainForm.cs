using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System.Text.Json;

namespace SGL.Analytics.KeyTool {
	public partial class MainForm : Form {
		private const int minPassphraseLength = 12;
		private const string defaultCurveName = "secp521r1";
		private const string configFile = "KeyTool-Settings.json";
		private int rsaKeyStrength;
		private List<DistinguishedNameEntryEdit> dnEntryEdits;
		private string? keyGenPassphrase = null;

		public MainForm() {
			InitializeComponent();
			cmbNamedCurve.Items.Clear();
			cmbNamedCurve.Items.AddRange(KeyPair.GetSupportedNamedEllipticCurves().OrderByDescending(curve => curve.KeyLength).Select(curve => curve.Name).ToArray());
			var defaultCurveIndex = cmbNamedCurve.Items.IndexOf(defaultCurveName);
			if (defaultCurveIndex >= 0) {
				cmbNamedCurve.SelectedIndex = defaultCurveIndex;
			}
			var settings = LoadSettings();
			FillDistinguishedNameEntries(settings.InitialDistinguishedName);
			spinRsaKeyStrengthExp_ValueChanged(spinRsaKeyStrengthExp, EventArgs.Empty);
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
			if (txtPassphrase.Text.Length < minPassphraseLength) {
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
			btnGenerateKeyAndCsr.Enabled = false;
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
			var isSignerCert = chkGenerateSigner.Checked;
			var keyType = (tabsKeyType.SelectedTab == tabEllipticCurve) ? KeyType.EllipticCurves : KeyType.RSA;
			var ellipticCurveName = cmbNamedCurve.SelectedItem as string;
			var selectedRsaKeyStrength = rsaKeyStrength;
			var passphrase = keyGenPassphrase;
			var csrDn = new DistinguishedName(dnEntryEdits.Select(entry => new KeyValuePair<string, string>(entry.TypeCode ?? "", entry.Value)));
			lblKeyGenStatus.Text = "Generating ...";
			lblKeyGenStatus.BackColor = Color.Yellow;
			progBarKeyGen.Value = 0;
			progBarKeyGen.Style = ProgressBarStyle.Marquee;
			var keyGenTask = Task.Run(async () => {
				await GenerateKeyAndCsr(intermediateKeyPath, csrOutputPath, isSignerCert, keyType, ellipticCurveName, selectedRsaKeyStrength, passphrase, csrDn);
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

		private static async Task GenerateKeyAndCsr(string intermediateKeyPath, string csrOutputPath, bool isSignerCert, KeyType keyType, string? ellipticCurveName, int selectedRsaKeyStrength, string passphrase, DistinguishedName csrDn) {
			var random = new RandomGenerator();
			KeyPair primaryKeyPair = null!;
			KeyPair authKeyPair = null!;
			switch (keyType) {
				case KeyType.RSA:
					var random1 = random.DeriveGenerator(256);
					var random2 = random.DeriveGenerator(256);
					var subTask1 = Task.Run(() => {
						primaryKeyPair = KeyPair.GenerateRSA(random1, selectedRsaKeyStrength);
					});
					var subTask2 = Task.CompletedTask;
					if (!isSignerCert) {
						subTask2 = Task.Run(() => {
							authKeyPair = KeyPair.GenerateRSA(random2, selectedRsaKeyStrength);
						});
					}
					await subTask1;
					await subTask2;
					break;
				case KeyType.EllipticCurves:
					primaryKeyPair = KeyPair.GenerateEllipticCurves(random, 0, ellipticCurveName);
					if (!isSignerCert) {
						authKeyPair = KeyPair.GenerateEllipticCurves(random, 0, ellipticCurveName);
					}
					break;
				default:
					throw new NotImplementedException();
			}
			using (var keyOutputFile = File.Create(intermediateKeyPath, 4096, FileOptions.Asynchronous)) {
				using var pemBuff = new MemoryStream();
				using (var pemWriter = new StreamWriter(pemBuff, leaveOpen: true)) {
					primaryKeyPair.StoreToPem(pemWriter, PemEncryptionMode.AES_256_CBC, passphrase.ToCharArray(), random);
					if (!isSignerCert) {
						authKeyPair.StoreToPem(pemWriter, PemEncryptionMode.AES_256_CBC, passphrase.ToCharArray(), random);
					}
				}
				pemBuff.Position = 0;
				await pemBuff.CopyToAsync(keyOutputFile);
			}
			var csrs = new List<CertificateSigningRequest>();
			if (isSignerCert) {
				var signerCsr = CertificateSigningRequest.Generate(csrDn, primaryKeyPair, requestSubjectKeyIdentifier: true,
					requestAuthorityKeyIdentifier: true, requestKeyUsages: KeyUsages.KeyCertSign,
					requestCABasicConstraints: (true, 1));
				csrs.Add(signerCsr);
			}
			else {
				var encryptionCsr = CertificateSigningRequest.Generate(csrDn, primaryKeyPair, requestSubjectKeyIdentifier: true,
					requestAuthorityKeyIdentifier: true, requestKeyUsages: KeyUsages.KeyEncipherment,
					requestCABasicConstraints: (false, null));
				csrs.Add(encryptionCsr);
				var authCsr = CertificateSigningRequest.Generate(csrDn, authKeyPair, requestSubjectKeyIdentifier: true,
					requestAuthorityKeyIdentifier: true, requestKeyUsages: KeyUsages.DigitalSignature,
					requestCABasicConstraints: (false, null));
				csrs.Add(authCsr);
			}
			using (var csrOutputFile = File.Create(csrOutputPath, 4096, FileOptions.Asynchronous)) {
				using var pemBuff = new MemoryStream();
				using (var pemWriter = new StreamWriter(pemBuff, leaveOpen: true)) {
					foreach (var csr in csrs) {
						csr.StoreToPem(pemWriter);
					}
				}
				pemBuff.Position = 0;
				await pemBuff.CopyToAsync(csrOutputFile);
			}
		}
	}
}