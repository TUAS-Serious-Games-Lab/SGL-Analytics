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
			tabControl1 = new TabControl();
			tabGenerateKey = new TabPage();
			tabCreateCSR = new TabPage();
			tabSignCert = new TabPage();
			tabBuildKeyFile = new TabPage();
			tabControl1.SuspendLayout();
			SuspendLayout();
			// 
			// tabControl1
			// 
			tabControl1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			tabControl1.Controls.Add(tabGenerateKey);
			tabControl1.Controls.Add(tabCreateCSR);
			tabControl1.Controls.Add(tabSignCert);
			tabControl1.Controls.Add(tabBuildKeyFile);
			tabControl1.Location = new Point(0, 0);
			tabControl1.Name = "tabControl1";
			tabControl1.SelectedIndex = 0;
			tabControl1.Size = new Size(912, 544);
			tabControl1.TabIndex = 0;
			// 
			// tabGenerateKey
			// 
			tabGenerateKey.Location = new Point(4, 24);
			tabGenerateKey.Name = "tabGenerateKey";
			tabGenerateKey.Padding = new Padding(3);
			tabGenerateKey.Size = new Size(905, 518);
			tabGenerateKey.TabIndex = 0;
			tabGenerateKey.Text = "Generate Key";
			tabGenerateKey.UseVisualStyleBackColor = true;
			// 
			// tabCreateCSR
			// 
			tabCreateCSR.Location = new Point(4, 24);
			tabCreateCSR.Name = "tabCreateCSR";
			tabCreateCSR.Padding = new Padding(3);
			tabCreateCSR.Size = new Size(905, 518);
			tabCreateCSR.TabIndex = 1;
			tabCreateCSR.Text = "Create CSR";
			tabCreateCSR.UseVisualStyleBackColor = true;
			// 
			// tabSignCert
			// 
			tabSignCert.Location = new Point(4, 24);
			tabSignCert.Name = "tabSignCert";
			tabSignCert.Padding = new Padding(3);
			tabSignCert.Size = new Size(905, 518);
			tabSignCert.TabIndex = 2;
			tabSignCert.Text = "Sign Certificate";
			tabSignCert.UseVisualStyleBackColor = true;
			// 
			// tabBuildKeyFile
			// 
			tabBuildKeyFile.Location = new Point(4, 24);
			tabBuildKeyFile.Name = "tabBuildKeyFile";
			tabBuildKeyFile.Padding = new Padding(3);
			tabBuildKeyFile.Size = new Size(904, 516);
			tabBuildKeyFile.TabIndex = 3;
			tabBuildKeyFile.Text = "Build Key File";
			tabBuildKeyFile.UseVisualStyleBackColor = true;
			// 
			// MainForm
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(913, 545);
			Controls.Add(tabControl1);
			Name = "MainForm";
			Text = "SGL-Analytics Key Tool";
			tabControl1.ResumeLayout(false);
			ResumeLayout(false);
		}

		#endregion

		private TabControl tabControl1;
		private TabPage tabGenerateKey;
		private TabPage tabCreateCSR;
		private TabPage tabSignCert;
		private TabPage tabBuildKeyFile;
	}
}