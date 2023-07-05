namespace SGL.Analytics.KeyTool {
	partial class DistinguishedNameEntryEdit {
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

		#region Component Designer generated code

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			cmbType = new ComboBox();
			txtValue = new TextBox();
			SuspendLayout();
			// 
			// cmbType
			// 
			cmbType.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
			cmbType.DropDownStyle = ComboBoxStyle.DropDownList;
			cmbType.FormattingEnabled = true;
			cmbType.Items.AddRange(new object[] { "C - country code", "ST - state, or province name", "L - locality name", "O - organization", "OU - organizational unit name", "T - Title", "CN - common name" });
			cmbType.Location = new Point(3, 3);
			cmbType.Name = "cmbType";
			cmbType.Size = new Size(153, 23);
			cmbType.TabIndex = 0;
			cmbType.SelectedIndexChanged += cmbType_SelectedIndexChanged;
			// 
			// txtValue
			// 
			txtValue.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			txtValue.Location = new Point(162, 3);
			txtValue.Name = "txtValue";
			txtValue.Size = new Size(135, 23);
			txtValue.TabIndex = 1;
			txtValue.TextChanged += txtValue_TextChanged;
			// 
			// DistinguishedNameEntryEdit
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			Controls.Add(txtValue);
			Controls.Add(cmbType);
			MaximumSize = new Size(0, 30);
			MinimumSize = new Size(300, 30);
			Name = "DistinguishedNameEntryEdit";
			Size = new Size(300, 30);
			ResumeLayout(false);
			PerformLayout();
		}

		#endregion

		private ComboBox cmbType;
		private TextBox txtValue;
	}
}
