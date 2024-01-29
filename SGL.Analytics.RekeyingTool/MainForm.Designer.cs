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
			cmbBackendSystem = new ComboBox();
			label1 = new Label();
			groupBox2 = new GroupBox();
			lblDecryptionKeyId = new Label();
			lblAuthKeyId = new Label();
			label6 = new Label();
			label5 = new Label();
			txtKeyPassphrase = new TextBox();
			label4 = new Label();
			btnBrowseKeyFile = new Button();
			lblKeyFilePath = new Label();
			label3 = new Label();
			groupBox3 = new GroupBox();
			chkRekeyUserRegistrations = new CheckBox();
			chkRekeyLogs = new CheckBox();
			lstDstCerts = new ListBox();
			groupBox4 = new GroupBox();
			progActivity = new ProgressBar();
			btnStart = new Button();
			btnCancel = new Button();
			logMessages = new Utilities.WinForms.Controls.LogGui.LogMessageList();
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
			groupBox1.Controls.Add(cmbBackendSystem);
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
			txtAppName.Leave += onChangeDataRepository;
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
			// cmbBackendSystem
			// 
			cmbBackendSystem.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			cmbBackendSystem.DropDownStyle = ComboBoxStyle.DropDownList;
			cmbBackendSystem.FormattingEnabled = true;
			cmbBackendSystem.Location = new Point(153, 16);
			cmbBackendSystem.Name = "cmbBackendSystem";
			cmbBackendSystem.Size = new Size(701, 23);
			cmbBackendSystem.TabIndex = 1;
			cmbBackendSystem.SelectedIndexChanged += onChangeDataRepository;
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
			groupBox2.Controls.Add(lblDecryptionKeyId);
			groupBox2.Controls.Add(lblAuthKeyId);
			groupBox2.Controls.Add(label6);
			groupBox2.Controls.Add(label5);
			groupBox2.Controls.Add(txtKeyPassphrase);
			groupBox2.Controls.Add(label4);
			groupBox2.Controls.Add(btnBrowseKeyFile);
			groupBox2.Controls.Add(lblKeyFilePath);
			groupBox2.Controls.Add(label3);
			groupBox2.Location = new Point(12, 91);
			groupBox2.Name = "groupBox2";
			groupBox2.Size = new Size(860, 135);
			groupBox2.TabIndex = 1;
			groupBox2.TabStop = false;
			groupBox2.Text = "User giving access";
			// 
			// lblDecryptionKeyId
			// 
			lblDecryptionKeyId.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			lblDecryptionKeyId.Location = new Point(153, 102);
			lblDecryptionKeyId.Name = "lblDecryptionKeyId";
			lblDecryptionKeyId.Size = new Size(620, 23);
			lblDecryptionKeyId.TabIndex = 8;
			// 
			// lblAuthKeyId
			// 
			lblAuthKeyId.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			lblAuthKeyId.Location = new Point(153, 74);
			lblAuthKeyId.Name = "lblAuthKeyId";
			lblAuthKeyId.Size = new Size(620, 23);
			lblAuthKeyId.TabIndex = 7;
			// 
			// label6
			// 
			label6.AutoSize = true;
			label6.Location = new Point(6, 102);
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
			txtKeyPassphrase.Validated += updateSrcKeyInfo;
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
			groupBox3.Controls.Add(chkRekeyUserRegistrations);
			groupBox3.Controls.Add(chkRekeyLogs);
			groupBox3.Controls.Add(lstDstCerts);
			groupBox3.Location = new Point(12, 232);
			groupBox3.Name = "groupBox3";
			groupBox3.Size = new Size(860, 175);
			groupBox3.TabIndex = 2;
			groupBox3.TabStop = false;
			groupBox3.Text = "User receiving access";
			// 
			// chkRekeyUserRegistrations
			// 
			chkRekeyUserRegistrations.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
			chkRekeyUserRegistrations.AutoSize = true;
			chkRekeyUserRegistrations.Location = new Point(97, 150);
			chkRekeyUserRegistrations.Name = "chkRekeyUserRegistrations";
			chkRekeyUserRegistrations.Size = new Size(154, 19);
			chkRekeyUserRegistrations.TabIndex = 2;
			chkRekeyUserRegistrations.Text = "Rekey User Registrations";
			chkRekeyUserRegistrations.UseVisualStyleBackColor = true;
			// 
			// chkRekeyLogs
			// 
			chkRekeyLogs.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
			chkRekeyLogs.AutoSize = true;
			chkRekeyLogs.Location = new Point(6, 150);
			chkRekeyLogs.Name = "chkRekeyLogs";
			chkRekeyLogs.Size = new Size(85, 19);
			chkRekeyLogs.TabIndex = 1;
			chkRekeyLogs.Text = "Rekey Logs";
			chkRekeyLogs.UseVisualStyleBackColor = true;
			// 
			// lstDstCerts
			// 
			lstDstCerts.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			lstDstCerts.FormattingEnabled = true;
			lstDstCerts.IntegralHeight = false;
			lstDstCerts.ItemHeight = 15;
			lstDstCerts.Location = new Point(7, 22);
			lstDstCerts.Name = "lstDstCerts";
			lstDstCerts.Size = new Size(847, 122);
			lstDstCerts.TabIndex = 0;
			// 
			// groupBox4
			// 
			groupBox4.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			groupBox4.Controls.Add(progActivity);
			groupBox4.Controls.Add(btnStart);
			groupBox4.Controls.Add(btnCancel);
			groupBox4.Controls.Add(logMessages);
			groupBox4.Location = new Point(12, 413);
			groupBox4.Name = "groupBox4";
			groupBox4.Size = new Size(860, 142);
			groupBox4.TabIndex = 3;
			groupBox4.TabStop = false;
			groupBox4.Text = "Log and progress";
			// 
			// progActivity
			// 
			progActivity.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			progActivity.Location = new Point(6, 113);
			progActivity.Name = "progActivity";
			progActivity.Size = new Size(686, 23);
			progActivity.Style = ProgressBarStyle.Marquee;
			progActivity.TabIndex = 3;
			// 
			// btnStart
			// 
			btnStart.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			btnStart.Location = new Point(698, 113);
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
			btnCancel.Location = new Point(779, 113);
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
			logMessages.Size = new Size(847, 91);
			logMessages.TabIndex = 0;
			logMessages.TraceItemBackground = Color.White;
			logMessages.TraceItemForeground = Color.Gray;
			logMessages.WarningItemBackground = Color.Yellow;
			logMessages.WarningItemForeground = Color.Black;
			// 
			// MainForm
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(884, 561);
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
		private ComboBox cmbBackendSystem;
		private TextBox txtAppName;
		private Label label2;
		private Label label3;
		private Label lblKeyFilePath;
		private Button btnBrowseKeyFile;
		private TextBox txtKeyPassphrase;
		private Label label4;
		private Label lblAuthKeyId;
		private Label label6;
		private Label label5;
		private Label lblDecryptionKeyId;
		private ListBox lstDstCerts;
		private Utilities.WinForms.Controls.LogGui.LogMessageList logMessages;
		private CheckBox chkRekeyUserRegistrations;
		private CheckBox chkRekeyLogs;
		private Button btnCancel;
		private Button btnStart;
		private ProgressBar progActivity;
	}
}