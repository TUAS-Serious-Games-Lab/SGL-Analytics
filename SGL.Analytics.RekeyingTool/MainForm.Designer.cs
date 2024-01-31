namespace SGL.Analytics.RekeyingTool {
	partial class MainForm {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing) {
			if (disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
			groupBox1 = new GroupBox();
			txtAppName = new TextBox();
			label2 = new Label();
			cmbBackends = new ComboBox();
			label1 = new Label();
			groupBox2 = new GroupBox();
			lblDecryptionKeyInfo = new Label();
			lblAuthKeyInfo = new Label();
			label6 = new Label();
			label5 = new Label();
			txtKeyPassphrase = new TextBox();
			label4 = new Label();
			btnBrowseKeyFile = new Button();
			lblKeyFilePath = new Label();
			label3 = new Label();
			groupBox3 = new GroupBox();
			radRekeyUserRegistrations = new RadioButton();
			radRekeyLogs = new RadioButton();
			btnBrowseSignerCert = new Button();
			lblSignerCertPath = new Label();
			label7 = new Label();
			lstDstCerts = new ListBox();
			groupBox4 = new GroupBox();
			progActivity = new ProgressBar();
			btnStart = new Button();
			btnCancel = new Button();
			logMessages = new Utilities.WinForms.Controls.LogGui.LogMessageList();
			browseKeyFileDialog = new OpenFileDialog();
			browseSignerCertFileDialog = new OpenFileDialog();
			groupBox1.SuspendLayout();
			groupBox2.SuspendLayout();
			groupBox3.SuspendLayout();
			groupBox4.SuspendLayout();
			SuspendLayout();
			// 
			// groupBox1
			// 
			groupBox1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			groupBox1.Controls.Add(txtAppName);
			groupBox1.Controls.Add(label2);
			groupBox1.Controls.Add(cmbBackends);
			groupBox1.Controls.Add(label1);
			groupBox1.Location = new Point(12, 8);
			groupBox1.Name = "groupBox1";
			groupBox1.Size = new Size(860, 77);
			groupBox1.TabIndex = 0;
			groupBox1.TabStop = false;
			groupBox1.Text = "Data repository";
			// 
			// txtAppName
			// 
			txtAppName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			txtAppName.Location = new Point(153, 45);
			txtAppName.Name = "txtAppName";
			txtAppName.Size = new Size(701, 23);
			txtAppName.TabIndex = 3;
			txtAppName.Leave += txtAppName_Leave;
			// 
			// label2
			// 
			label2.AutoSize = true;
			label2.Location = new Point(6, 48);
			label2.Name = "label2";
			label2.Size = new Size(103, 15);
			label2.TabIndex = 2;
			label2.Text = "Application Name";
			// 
			// cmbBackends
			// 
			cmbBackends.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			cmbBackends.DropDownStyle = ComboBoxStyle.DropDownList;
			cmbBackends.FormattingEnabled = true;
			cmbBackends.Location = new Point(153, 16);
			cmbBackends.Name = "cmbBackends";
			cmbBackends.Size = new Size(701, 23);
			cmbBackends.TabIndex = 1;
			cmbBackends.SelectedValueChanged += cmbBackends_SelectedValueChangedAsync;
			// 
			// label1
			// 
			label1.AutoSize = true;
			label1.Location = new Point(6, 19);
			label1.Name = "label1";
			label1.Size = new Size(93, 15);
			label1.TabIndex = 0;
			label1.Text = "Backend System";
			// 
			// groupBox2
			// 
			groupBox2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			groupBox2.Controls.Add(lblDecryptionKeyInfo);
			groupBox2.Controls.Add(lblAuthKeyInfo);
			groupBox2.Controls.Add(label6);
			groupBox2.Controls.Add(label5);
			groupBox2.Controls.Add(txtKeyPassphrase);
			groupBox2.Controls.Add(label4);
			groupBox2.Controls.Add(btnBrowseKeyFile);
			groupBox2.Controls.Add(lblKeyFilePath);
			groupBox2.Controls.Add(label3);
			groupBox2.Location = new Point(12, 91);
			groupBox2.Name = "groupBox2";
			groupBox2.Size = new Size(860, 162);
			groupBox2.TabIndex = 1;
			groupBox2.TabStop = false;
			groupBox2.Text = "User giving access";
			// 
			// lblDecryptionKeyInfo
			// 
			lblDecryptionKeyInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			lblDecryptionKeyInfo.Location = new Point(153, 114);
			lblDecryptionKeyInfo.Name = "lblDecryptionKeyInfo";
			lblDecryptionKeyInfo.Size = new Size(620, 40);
			lblDecryptionKeyInfo.TabIndex = 8;
			// 
			// lblAuthKeyInfo
			// 
			lblAuthKeyInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			lblAuthKeyInfo.Location = new Point(153, 74);
			lblAuthKeyInfo.Name = "lblAuthKeyInfo";
			lblAuthKeyInfo.Size = new Size(620, 40);
			lblAuthKeyInfo.TabIndex = 7;
			// 
			// label6
			// 
			label6.AutoSize = true;
			label6.Location = new Point(6, 114);
			label6.Name = "label6";
			label6.Size = new Size(111, 15);
			label6.TabIndex = 6;
			label6.Text = "Decryption Key Info";
			// 
			// label5
			// 
			label5.AutoSize = true;
			label5.Location = new Point(6, 74);
			label5.Name = "label5";
			label5.Size = new Size(132, 15);
			label5.TabIndex = 5;
			label5.Text = "Authentication Key Info";
			// 
			// txtKeyPassphrase
			// 
			txtKeyPassphrase.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			txtKeyPassphrase.Location = new Point(153, 45);
			txtKeyPassphrase.Name = "txtKeyPassphrase";
			txtKeyPassphrase.PasswordChar = '*';
			txtKeyPassphrase.Size = new Size(620, 23);
			txtKeyPassphrase.TabIndex = 4;
			txtKeyPassphrase.Leave += updateSrcKeyInfo;
			// 
			// label4
			// 
			label4.AutoSize = true;
			label4.Location = new Point(6, 48);
			label4.Name = "label4";
			label4.Size = new Size(87, 15);
			label4.TabIndex = 3;
			label4.Text = "Key Passphrase";
			// 
			// btnBrowseKeyFile
			// 
			btnBrowseKeyFile.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnBrowseKeyFile.Location = new Point(779, 19);
			btnBrowseKeyFile.Name = "btnBrowseKeyFile";
			btnBrowseKeyFile.Size = new Size(75, 23);
			btnBrowseKeyFile.TabIndex = 2;
			btnBrowseKeyFile.Text = "Browse";
			btnBrowseKeyFile.UseVisualStyleBackColor = true;
			btnBrowseKeyFile.Click += btnBrowseKeyFile_Click;
			// 
			// lblKeyFilePath
			// 
			lblKeyFilePath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			lblKeyFilePath.Location = new Point(153, 19);
			lblKeyFilePath.Name = "lblKeyFilePath";
			lblKeyFilePath.Size = new Size(620, 23);
			lblKeyFilePath.TabIndex = 1;
			// 
			// label3
			// 
			label3.AutoSize = true;
			label3.Location = new Point(6, 23);
			label3.Name = "label3";
			label3.Size = new Size(47, 15);
			label3.TabIndex = 0;
			label3.Text = "Key File";
			// 
			// groupBox3
			// 
			groupBox3.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			groupBox3.Controls.Add(radRekeyUserRegistrations);
			groupBox3.Controls.Add(radRekeyLogs);
			groupBox3.Controls.Add(btnBrowseSignerCert);
			groupBox3.Controls.Add(lblSignerCertPath);
			groupBox3.Controls.Add(label7);
			groupBox3.Controls.Add(lstDstCerts);
			groupBox3.Location = new Point(12, 259);
			groupBox3.Name = "groupBox3";
			groupBox3.Size = new Size(860, 191);
			groupBox3.TabIndex = 2;
			groupBox3.TabStop = false;
			groupBox3.Text = "User receiving access";
			// 
			// radRekeyUserRegistrations
			// 
			radRekeyUserRegistrations.AutoSize = true;
			radRekeyUserRegistrations.Location = new Point(118, 165);
			radRekeyUserRegistrations.Name = "radRekeyUserRegistrations";
			radRekeyUserRegistrations.Size = new Size(153, 19);
			radRekeyUserRegistrations.TabIndex = 7;
			radRekeyUserRegistrations.Text = "Rekey User Registrations";
			radRekeyUserRegistrations.UseVisualStyleBackColor = true;
			radRekeyUserRegistrations.CheckedChanged += updateDstCertList;
			// 
			// radRekeyLogs
			// 
			radRekeyLogs.AutoSize = true;
			radRekeyLogs.Checked = true;
			radRekeyLogs.Location = new Point(7, 166);
			radRekeyLogs.Name = "radRekeyLogs";
			radRekeyLogs.Size = new Size(105, 19);
			radRekeyLogs.TabIndex = 6;
			radRekeyLogs.TabStop = true;
			radRekeyLogs.Text = "Rekey Log Files";
			radRekeyLogs.UseVisualStyleBackColor = true;
			radRekeyLogs.CheckedChanged += updateDstCertList;
			// 
			// btnBrowseSignerCert
			// 
			btnBrowseSignerCert.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnBrowseSignerCert.Location = new Point(779, 19);
			btnBrowseSignerCert.Name = "btnBrowseSignerCert";
			btnBrowseSignerCert.Size = new Size(75, 23);
			btnBrowseSignerCert.TabIndex = 5;
			btnBrowseSignerCert.Text = "Browse";
			btnBrowseSignerCert.UseVisualStyleBackColor = true;
			btnBrowseSignerCert.Click += btnBrowseSignerCert_Click;
			// 
			// lblSignerCertPath
			// 
			lblSignerCertPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			lblSignerCertPath.Location = new Point(183, 19);
			lblSignerCertPath.Name = "lblSignerCertPath";
			lblSignerCertPath.Size = new Size(590, 23);
			lblSignerCertPath.TabIndex = 4;
			// 
			// label7
			// 
			label7.AutoSize = true;
			label7.Location = new Point(7, 19);
			label7.Name = "label7";
			label7.Size = new Size(170, 15);
			label7.TabIndex = 3;
			label7.Text = "Signer Certificate for Validation";
			// 
			// lstDstCerts
			// 
			lstDstCerts.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			lstDstCerts.FormattingEnabled = true;
			lstDstCerts.IntegralHeight = false;
			lstDstCerts.ItemHeight = 15;
			lstDstCerts.Location = new Point(7, 45);
			lstDstCerts.Name = "lstDstCerts";
			lstDstCerts.Size = new Size(847, 115);
			lstDstCerts.TabIndex = 0;
			// 
			// groupBox4
			// 
			groupBox4.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			groupBox4.Controls.Add(progActivity);
			groupBox4.Controls.Add(btnStart);
			groupBox4.Controls.Add(btnCancel);
			groupBox4.Controls.Add(logMessages);
			groupBox4.Location = new Point(12, 456);
			groupBox4.Name = "groupBox4";
			groupBox4.Size = new Size(860, 134);
			groupBox4.TabIndex = 3;
			groupBox4.TabStop = false;
			groupBox4.Text = "Log and progress";
			// 
			// progActivity
			// 
			progActivity.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			progActivity.Location = new Point(6, 105);
			progActivity.Name = "progActivity";
			progActivity.Size = new Size(686, 23);
			progActivity.Style = ProgressBarStyle.Marquee;
			progActivity.TabIndex = 3;
			// 
			// btnStart
			// 
			btnStart.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			btnStart.Location = new Point(698, 105);
			btnStart.Name = "btnStart";
			btnStart.Size = new Size(75, 23);
			btnStart.TabIndex = 2;
			btnStart.Text = "Start";
			btnStart.UseVisualStyleBackColor = true;
			btnStart.Click += btnStart_Click;
			// 
			// btnCancel
			// 
			btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			btnCancel.Location = new Point(779, 105);
			btnCancel.Name = "btnCancel";
			btnCancel.Size = new Size(75, 23);
			btnCancel.TabIndex = 1;
			btnCancel.Text = "Cancel";
			btnCancel.UseVisualStyleBackColor = true;
			btnCancel.Click += btnCancel_Click;
			// 
			// logMessages
			// 
			logMessages.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			logMessages.CriticalItemBackground = Color.DarkRed;
			logMessages.CriticalItemForeground = Color.White;
			logMessages.DebugItemBackground = Color.White;
			logMessages.DebugItemForeground = Color.Black;
			logMessages.ErrorItemBackground = Color.OrangeRed;
			logMessages.ErrorItemForeground = Color.Black;
			logMessages.InfoItemBackground = Color.LightGreen;
			logMessages.InfoItemForeground = Color.Black;
			logMessages.Location = new Point(7, 19);
			logMessages.Margin = new Padding(0);
			logMessages.Name = "logMessages";
			logMessages.SelectedItemBackground = SystemColors.Highlight;
			logMessages.SelectedItemForeground = SystemColors.HighlightText;
			logMessages.Size = new Size(847, 83);
			logMessages.TabIndex = 0;
			logMessages.TraceItemBackground = Color.White;
			logMessages.TraceItemForeground = Color.Gray;
			logMessages.WarningItemBackground = Color.Yellow;
			logMessages.WarningItemForeground = Color.Black;
			// 
			// browseKeyFileDialog
			// 
			browseKeyFileDialog.Filter = "Key Files|*.key";
			// 
			// browseSignerCertFileDialog
			// 
			browseSignerCertFileDialog.Filter = "Certificate Files|*.cert;*.pem";
			// 
			// MainForm
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(884, 596);
			Controls.Add(groupBox4);
			Controls.Add(groupBox3);
			Controls.Add(groupBox2);
			Controls.Add(groupBox1);
			Icon = (Icon)resources.GetObject("$this.Icon");
			MinimumSize = new Size(900, 600);
			Name = "MainForm";
			Text = "MainForm";
			groupBox1.ResumeLayout(false);
			groupBox1.PerformLayout();
			groupBox2.ResumeLayout(false);
			groupBox2.PerformLayout();
			groupBox3.ResumeLayout(false);
			groupBox3.PerformLayout();
			groupBox4.ResumeLayout(false);
			ResumeLayout(false);
		}

		#endregion

		private GroupBox groupBox1;
		private GroupBox groupBox2;
		private GroupBox groupBox3;
		private GroupBox groupBox4;
		private Label label1;
		private ComboBox cmbBackends;
		private TextBox txtAppName;
		private Label label2;
		private Label label3;
		private Label lblKeyFilePath;
		private Button btnBrowseKeyFile;
		private TextBox txtKeyPassphrase;
		private Label label4;
		private Label lblAuthKeyInfo;
		private Label label6;
		private Label label5;
		private Label lblDecryptionKeyInfo;
		private ListBox lstDstCerts;
		private Utilities.WinForms.Controls.LogGui.LogMessageList logMessages;
		private Button btnCancel;
		private Button btnStart;
		private ProgressBar progActivity;
		private OpenFileDialog browseKeyFileDialog;
		private Button btnBrowseSignerCert;
		private Label lblSignerCertPath;
		private Label label7;
		private OpenFileDialog browseSignerCertFileDialog;
		private RadioButton radRekeyUserRegistrations;
		private RadioButton radRekeyLogs;
	}
}