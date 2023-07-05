namespace SGL.Analytics.KeyTool {
	partial class MainForm {
		/// <summary>
		///  Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		///  Clean up any resources being used.
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
		///  Required method for Designer support - do not modify
		///  the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			GroupBox groupBox1;
			Label label3;
			Label label2;
			GroupBox groupBox2;
			Label label7;
			lblRepeatPassphrase = new Label();
			txtRepeatPassphrase = new TextBox();
			lblPassphrase = new Label();
			txtPassphrase = new TextBox();
			progBarKeyGen = new ProgressBar();
			lblIntermediateKeySavePath = new Label();
			btnBrowseSaveIntermediateKeyFile = new Button();
			tabsKeyType = new TabControl();
			tabEllipticCurve = new TabPage();
			cmbNamedCurve = new ComboBox();
			tabRSA = new TabPage();
			lblRsaKeyStrength = new Label();
			spinRsaKeyStrengthExp = new NumericUpDown();
			label1 = new Label();
			btnRemoveCsrDnInput = new Button();
			btnAddCsrDnInput = new Button();
			lblCsrOutputFile = new Label();
			btnBrowseCsrOutputFile = new Button();
			chkGenerateSigner = new CheckBox();
			flowCsrDnFields = new FlowLayoutPanel();
			tabsMain = new TabControl();
			tabGenerateKeyAndCSR = new TabPage();
			lblKeyGenStatus = new Label();
			btnGenerateKeyAndCsr = new Button();
			tabSignCert = new TabPage();
			tabBuildKeyFile = new TabPage();
			saveIntermediateKeyFileDialog = new SaveFileDialog();
			saveCsrFileDialog = new SaveFileDialog();
			groupBox1 = new GroupBox();
			label3 = new Label();
			label2 = new Label();
			groupBox2 = new GroupBox();
			label7 = new Label();
			groupBox1.SuspendLayout();
			tabsKeyType.SuspendLayout();
			tabEllipticCurve.SuspendLayout();
			tabRSA.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)spinRsaKeyStrengthExp).BeginInit();
			groupBox2.SuspendLayout();
			tabsMain.SuspendLayout();
			tabGenerateKeyAndCSR.SuspendLayout();
			SuspendLayout();
			// 
			// groupBox1
			// 
			groupBox1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			groupBox1.Controls.Add(lblRepeatPassphrase);
			groupBox1.Controls.Add(txtRepeatPassphrase);
			groupBox1.Controls.Add(lblPassphrase);
			groupBox1.Controls.Add(txtPassphrase);
			groupBox1.Controls.Add(progBarKeyGen);
			groupBox1.Controls.Add(lblIntermediateKeySavePath);
			groupBox1.Controls.Add(btnBrowseSaveIntermediateKeyFile);
			groupBox1.Controls.Add(label3);
			groupBox1.Controls.Add(tabsKeyType);
			groupBox1.Location = new Point(3, 3);
			groupBox1.Name = "groupBox1";
			groupBox1.Size = new Size(519, 211);
			groupBox1.TabIndex = 0;
			groupBox1.TabStop = false;
			groupBox1.Text = "Key Generation";
			// 
			// lblRepeatPassphrase
			// 
			lblRepeatPassphrase.AutoSize = true;
			lblRepeatPassphrase.Location = new Point(6, 153);
			lblRepeatPassphrase.Name = "lblRepeatPassphrase";
			lblRepeatPassphrase.Size = new Size(104, 15);
			lblRepeatPassphrase.TabIndex = 8;
			lblRepeatPassphrase.Text = "Repeat Passphrase";
			// 
			// txtRepeatPassphrase
			// 
			txtRepeatPassphrase.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			txtRepeatPassphrase.Location = new Point(129, 150);
			txtRepeatPassphrase.Name = "txtRepeatPassphrase";
			txtRepeatPassphrase.PasswordChar = '*';
			txtRepeatPassphrase.Size = new Size(383, 23);
			txtRepeatPassphrase.TabIndex = 7;
			txtRepeatPassphrase.TextChanged += txtRepeatPassphrase_TextChanged;
			// 
			// lblPassphrase
			// 
			lblPassphrase.AutoSize = true;
			lblPassphrase.Location = new Point(6, 124);
			lblPassphrase.Name = "lblPassphrase";
			lblPassphrase.Size = new Size(65, 15);
			lblPassphrase.TabIndex = 6;
			lblPassphrase.Text = "Passphrase";
			// 
			// txtPassphrase
			// 
			txtPassphrase.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			txtPassphrase.Location = new Point(129, 121);
			txtPassphrase.Name = "txtPassphrase";
			txtPassphrase.PasswordChar = '*';
			txtPassphrase.Size = new Size(383, 23);
			txtPassphrase.TabIndex = 5;
			txtPassphrase.TextChanged += txtPassphrase_TextChanged;
			// 
			// progBarKeyGen
			// 
			progBarKeyGen.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			progBarKeyGen.Location = new Point(6, 180);
			progBarKeyGen.MarqueeAnimationSpeed = 30;
			progBarKeyGen.Name = "progBarKeyGen";
			progBarKeyGen.Size = new Size(507, 25);
			progBarKeyGen.Style = ProgressBarStyle.Continuous;
			progBarKeyGen.TabIndex = 4;
			// 
			// lblIntermediateKeySavePath
			// 
			lblIntermediateKeySavePath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			lblIntermediateKeySavePath.Location = new Point(129, 90);
			lblIntermediateKeySavePath.Name = "lblIntermediateKeySavePath";
			lblIntermediateKeySavePath.Size = new Size(278, 25);
			lblIntermediateKeySavePath.TabIndex = 3;
			lblIntermediateKeySavePath.TextAlign = ContentAlignment.MiddleLeft;
			// 
			// btnBrowseSaveIntermediateKeyFile
			// 
			btnBrowseSaveIntermediateKeyFile.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnBrowseSaveIntermediateKeyFile.Location = new Point(413, 90);
			btnBrowseSaveIntermediateKeyFile.Name = "btnBrowseSaveIntermediateKeyFile";
			btnBrowseSaveIntermediateKeyFile.Size = new Size(100, 25);
			btnBrowseSaveIntermediateKeyFile.TabIndex = 2;
			btnBrowseSaveIntermediateKeyFile.Text = "Browse ...";
			btnBrowseSaveIntermediateKeyFile.UseVisualStyleBackColor = true;
			btnBrowseSaveIntermediateKeyFile.Click += btnBrowseSaveIntermediateKeyFile_Click;
			// 
			// label3
			// 
			label3.AutoSize = true;
			label3.Location = new Point(6, 95);
			label3.Name = "label3";
			label3.Size = new Size(117, 15);
			label3.TabIndex = 1;
			label3.Text = "Intermediate Key File";
			// 
			// tabsKeyType
			// 
			tabsKeyType.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			tabsKeyType.Controls.Add(tabEllipticCurve);
			tabsKeyType.Controls.Add(tabRSA);
			tabsKeyType.Location = new Point(5, 22);
			tabsKeyType.Name = "tabsKeyType";
			tabsKeyType.SelectedIndex = 0;
			tabsKeyType.Size = new Size(511, 62);
			tabsKeyType.TabIndex = 0;
			// 
			// tabEllipticCurve
			// 
			tabEllipticCurve.Controls.Add(label2);
			tabEllipticCurve.Controls.Add(cmbNamedCurve);
			tabEllipticCurve.Location = new Point(4, 24);
			tabEllipticCurve.Name = "tabEllipticCurve";
			tabEllipticCurve.Padding = new Padding(3);
			tabEllipticCurve.Size = new Size(503, 34);
			tabEllipticCurve.TabIndex = 0;
			tabEllipticCurve.Text = "Elliptic Curve";
			tabEllipticCurve.UseVisualStyleBackColor = true;
			// 
			// label2
			// 
			label2.AutoSize = true;
			label2.Location = new Point(6, 9);
			label2.Name = "label2";
			label2.Size = new Size(118, 15);
			label2.TabIndex = 1;
			label2.Text = "Named Elliptic Curve";
			// 
			// cmbNamedCurve
			// 
			cmbNamedCurve.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			cmbNamedCurve.DropDownStyle = ComboBoxStyle.DropDownList;
			cmbNamedCurve.FormattingEnabled = true;
			cmbNamedCurve.Location = new Point(237, 6);
			cmbNamedCurve.Name = "cmbNamedCurve";
			cmbNamedCurve.Size = new Size(260, 23);
			cmbNamedCurve.TabIndex = 0;
			// 
			// tabRSA
			// 
			tabRSA.Controls.Add(lblRsaKeyStrength);
			tabRSA.Controls.Add(spinRsaKeyStrengthExp);
			tabRSA.Controls.Add(label1);
			tabRSA.Location = new Point(4, 24);
			tabRSA.Name = "tabRSA";
			tabRSA.Padding = new Padding(3);
			tabRSA.Size = new Size(503, 34);
			tabRSA.TabIndex = 1;
			tabRSA.Text = "RSA";
			tabRSA.UseVisualStyleBackColor = true;
			// 
			// lblRsaKeyStrength
			// 
			lblRsaKeyStrength.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			lblRsaKeyStrength.Location = new Point(373, 5);
			lblRsaKeyStrength.Name = "lblRsaKeyStrength";
			lblRsaKeyStrength.Size = new Size(110, 23);
			lblRsaKeyStrength.TabIndex = 2;
			lblRsaKeyStrength.Text = "4096";
			lblRsaKeyStrength.TextAlign = ContentAlignment.MiddleRight;
			// 
			// spinRsaKeyStrengthExp
			// 
			spinRsaKeyStrengthExp.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			spinRsaKeyStrengthExp.Location = new Point(450, 5);
			spinRsaKeyStrengthExp.Maximum = new decimal(new int[] { 30, 0, 0, 0 });
			spinRsaKeyStrengthExp.Minimum = new decimal(new int[] { 12, 0, 0, 0 });
			spinRsaKeyStrengthExp.Name = "spinRsaKeyStrengthExp";
			spinRsaKeyStrengthExp.Size = new Size(50, 23);
			spinRsaKeyStrengthExp.TabIndex = 1;
			spinRsaKeyStrengthExp.TabStop = false;
			spinRsaKeyStrengthExp.Value = new decimal(new int[] { 12, 0, 0, 0 });
			spinRsaKeyStrengthExp.ValueChanged += spinRsaKeyStrengthExp_ValueChanged;
			// 
			// label1
			// 
			label1.AutoSize = true;
			label1.Location = new Point(6, 9);
			label1.Name = "label1";
			label1.Size = new Size(74, 15);
			label1.TabIndex = 0;
			label1.Text = "Key Strength";
			// 
			// groupBox2
			// 
			groupBox2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			groupBox2.Controls.Add(btnRemoveCsrDnInput);
			groupBox2.Controls.Add(btnAddCsrDnInput);
			groupBox2.Controls.Add(lblCsrOutputFile);
			groupBox2.Controls.Add(btnBrowseCsrOutputFile);
			groupBox2.Controls.Add(label7);
			groupBox2.Controls.Add(chkGenerateSigner);
			groupBox2.Controls.Add(flowCsrDnFields);
			groupBox2.Location = new Point(3, 220);
			groupBox2.Name = "groupBox2";
			groupBox2.Size = new Size(519, 295);
			groupBox2.TabIndex = 1;
			groupBox2.TabStop = false;
			groupBox2.Text = "Certificate Signing Request";
			// 
			// btnRemoveCsrDnInput
			// 
			btnRemoveCsrDnInput.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnRemoveCsrDnInput.Location = new Point(482, 0);
			btnRemoveCsrDnInput.Name = "btnRemoveCsrDnInput";
			btnRemoveCsrDnInput.Size = new Size(30, 22);
			btnRemoveCsrDnInput.TabIndex = 8;
			btnRemoveCsrDnInput.Text = "-";
			btnRemoveCsrDnInput.UseVisualStyleBackColor = true;
			btnRemoveCsrDnInput.Click += btnRemoveCsrDnInput_Click;
			// 
			// btnAddCsrDnInput
			// 
			btnAddCsrDnInput.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnAddCsrDnInput.Location = new Point(446, 0);
			btnAddCsrDnInput.Name = "btnAddCsrDnInput";
			btnAddCsrDnInput.Size = new Size(30, 22);
			btnAddCsrDnInput.TabIndex = 7;
			btnAddCsrDnInput.Text = "+";
			btnAddCsrDnInput.UseVisualStyleBackColor = true;
			btnAddCsrDnInput.Click += btnAddCsrDnInput_Click;
			// 
			// lblCsrOutputFile
			// 
			lblCsrOutputFile.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			lblCsrOutputFile.Location = new Point(101, 262);
			lblCsrOutputFile.Name = "lblCsrOutputFile";
			lblCsrOutputFile.Size = new Size(305, 25);
			lblCsrOutputFile.TabIndex = 6;
			lblCsrOutputFile.TextAlign = ContentAlignment.MiddleLeft;
			// 
			// btnBrowseCsrOutputFile
			// 
			btnBrowseCsrOutputFile.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			btnBrowseCsrOutputFile.Location = new Point(412, 262);
			btnBrowseCsrOutputFile.Name = "btnBrowseCsrOutputFile";
			btnBrowseCsrOutputFile.Size = new Size(100, 25);
			btnBrowseCsrOutputFile.TabIndex = 5;
			btnBrowseCsrOutputFile.Text = "Browse ...";
			btnBrowseCsrOutputFile.UseVisualStyleBackColor = true;
			btnBrowseCsrOutputFile.Click += btnBrowseCsrOutputFile_Click;
			// 
			// label7
			// 
			label7.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
			label7.AutoSize = true;
			label7.Location = new Point(5, 267);
			label7.Name = "label7";
			label7.Size = new Size(90, 15);
			label7.TabIndex = 4;
			label7.Text = "CSR Output File";
			// 
			// chkGenerateSigner
			// 
			chkGenerateSigner.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			chkGenerateSigner.AutoSize = true;
			chkGenerateSigner.BackColor = SystemColors.Window;
			chkGenerateSigner.Location = new Point(274, 0);
			chkGenerateSigner.Name = "chkGenerateSigner";
			chkGenerateSigner.Size = new Size(166, 19);
			chkGenerateSigner.TabIndex = 2;
			chkGenerateSigner.Text = "Generate Signer Certificate";
			chkGenerateSigner.UseVisualStyleBackColor = false;
			// 
			// flowCsrDnFields
			// 
			flowCsrDnFields.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			flowCsrDnFields.AutoScroll = true;
			flowCsrDnFields.FlowDirection = FlowDirection.TopDown;
			flowCsrDnFields.Location = new Point(6, 22);
			flowCsrDnFields.Name = "flowCsrDnFields";
			flowCsrDnFields.Size = new Size(507, 234);
			flowCsrDnFields.TabIndex = 0;
			flowCsrDnFields.WrapContents = false;
			flowCsrDnFields.SizeChanged += flowCsrDnFields_SizeChanged;
			// 
			// tabsMain
			// 
			tabsMain.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			tabsMain.Controls.Add(tabGenerateKeyAndCSR);
			tabsMain.Controls.Add(tabSignCert);
			tabsMain.Controls.Add(tabBuildKeyFile);
			tabsMain.Location = new Point(0, 0);
			tabsMain.Name = "tabsMain";
			tabsMain.SelectedIndex = 0;
			tabsMain.Size = new Size(533, 580);
			tabsMain.TabIndex = 0;
			// 
			// tabGenerateKeyAndCSR
			// 
			tabGenerateKeyAndCSR.Controls.Add(lblKeyGenStatus);
			tabGenerateKeyAndCSR.Controls.Add(btnGenerateKeyAndCsr);
			tabGenerateKeyAndCSR.Controls.Add(groupBox2);
			tabGenerateKeyAndCSR.Controls.Add(groupBox1);
			tabGenerateKeyAndCSR.Location = new Point(4, 24);
			tabGenerateKeyAndCSR.Name = "tabGenerateKeyAndCSR";
			tabGenerateKeyAndCSR.Padding = new Padding(3);
			tabGenerateKeyAndCSR.Size = new Size(525, 552);
			tabGenerateKeyAndCSR.TabIndex = 0;
			tabGenerateKeyAndCSR.Text = "Generate Key & CSR";
			tabGenerateKeyAndCSR.UseVisualStyleBackColor = true;
			// 
			// lblKeyGenStatus
			// 
			lblKeyGenStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			lblKeyGenStatus.Location = new Point(8, 521);
			lblKeyGenStatus.Name = "lblKeyGenStatus";
			lblKeyGenStatus.Size = new Size(402, 25);
			lblKeyGenStatus.TabIndex = 3;
			// 
			// btnGenerateKeyAndCsr
			// 
			btnGenerateKeyAndCsr.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			btnGenerateKeyAndCsr.Location = new Point(415, 521);
			btnGenerateKeyAndCsr.Name = "btnGenerateKeyAndCsr";
			btnGenerateKeyAndCsr.Size = new Size(100, 25);
			btnGenerateKeyAndCsr.TabIndex = 2;
			btnGenerateKeyAndCsr.Text = "Generate";
			btnGenerateKeyAndCsr.UseVisualStyleBackColor = true;
			btnGenerateKeyAndCsr.Click += btnGenerateKeyAndCsr_Click;
			// 
			// tabSignCert
			// 
			tabSignCert.Location = new Point(4, 24);
			tabSignCert.Name = "tabSignCert";
			tabSignCert.Padding = new Padding(3);
			tabSignCert.Size = new Size(525, 552);
			tabSignCert.TabIndex = 2;
			tabSignCert.Text = "Sign Certificate";
			tabSignCert.UseVisualStyleBackColor = true;
			// 
			// tabBuildKeyFile
			// 
			tabBuildKeyFile.Location = new Point(4, 24);
			tabBuildKeyFile.Name = "tabBuildKeyFile";
			tabBuildKeyFile.Padding = new Padding(3);
			tabBuildKeyFile.Size = new Size(525, 552);
			tabBuildKeyFile.TabIndex = 3;
			tabBuildKeyFile.Text = "Build Key File";
			tabBuildKeyFile.UseVisualStyleBackColor = true;
			// 
			// saveIntermediateKeyFileDialog
			// 
			saveIntermediateKeyFileDialog.Filter = "Intermediary key files|*.intkey|All files|*.*";
			// 
			// saveCsrFileDialog
			// 
			saveCsrFileDialog.Filter = "Certificate signing request files|*.csr|All files|*.*";
			// 
			// MainForm
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(534, 581);
			Controls.Add(tabsMain);
			MinimumSize = new Size(450, 500);
			Name = "MainForm";
			Text = "SGL-Analytics Key Tool";
			groupBox1.ResumeLayout(false);
			groupBox1.PerformLayout();
			tabsKeyType.ResumeLayout(false);
			tabEllipticCurve.ResumeLayout(false);
			tabEllipticCurve.PerformLayout();
			tabRSA.ResumeLayout(false);
			tabRSA.PerformLayout();
			((System.ComponentModel.ISupportInitialize)spinRsaKeyStrengthExp).EndInit();
			groupBox2.ResumeLayout(false);
			groupBox2.PerformLayout();
			tabsMain.ResumeLayout(false);
			tabGenerateKeyAndCSR.ResumeLayout(false);
			ResumeLayout(false);
		}

		#endregion

		private TabControl tabsMain;
		private TabPage tabGenerateKeyAndCSR;
		private TabPage tabSignCert;
		private TabPage tabBuildKeyFile;
		private TabControl tabsKeyType;
		private TabPage tabEllipticCurve;
		private TabPage tabRSA;
		private NumericUpDown spinRsaKeyStrengthExp;
		private Label label1;
		private Label lblRsaKeyStrength;
		private ComboBox cmbNamedCurve;
		private Label label3;
		private SaveFileDialog saveIntermediateKeyFileDialog;
		private Button btnBrowseSaveIntermediateKeyFile;
		private Label lblIntermediateKeySavePath;
		private ProgressBar progBarKeyGen;
		private TextBox txtPassphrase;
		private TextBox txtRepeatPassphrase;
		private FlowLayoutPanel flowCsrDnFields;
		private Label lblKeyGenStatus;
		private Button btnGenerateKeyAndCsr;
		private CheckBox chkGenerateSigner;
		private Label lblCsrOutputFile;
		private Button btnBrowseCsrOutputFile;
		private Button btnAddCsrDnInput;
		private Button btnRemoveCsrDnInput;
		private SaveFileDialog saveCsrFileDialog;
		private Label lblRepeatPassphrase;
		private Label lblPassphrase;
	}
}