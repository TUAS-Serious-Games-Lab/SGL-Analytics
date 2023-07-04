using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SGL.Analytics.KeyTool {
	public partial class DistinguishedNameEntryEdit : UserControl {
		private EventHandler onValueChanged;
		private EventHandler onTypeChanged;

		public DistinguishedNameEntryEdit() {
			InitializeComponent();
		}

		public string? TypeCode {
			get {
				if (cmbType.SelectedIndex < 0) {
					return null;
				}
				var typeString = cmbType.SelectedItem as string;
				if (typeString == null) {
					return null;
				}
				var sepIdx = typeString.IndexOf(" - ");
				if (sepIdx < 0) {
					return null;
				}
				else {
					return typeString.Substring(0, sepIdx);
				}
			}
			set {
				if (value == null) {
					cmbType.SelectedIndex = -1;
				}
				else {
					var lookupString = value + " - ";
					var index = cmbType.Items.Cast<string>().ToList().FindIndex(entry => entry.StartsWith(lookupString));
					if (index < 0) {
						throw new ArgumentException("Given TypeCode is not supported.");
					}
					cmbType.SelectedIndex = index;
				}
			}
		}

		public string Value {
			get => txtValue.Text;
			set => txtValue.Text = value;
		}

		public event EventHandler TypeChanged {
			add => onTypeChanged += value;
#pragma warning disable CS8601 // Possible null reference assignment.
			remove => onTypeChanged -= value;
#pragma warning restore CS8601 // Possible null reference assignment.
		}
		public event EventHandler ValueChanged {
			add => onValueChanged += value;
#pragma warning disable CS8601 // Possible null reference assignment.
			remove => onValueChanged -= value;
#pragma warning restore CS8601 // Possible null reference assignment.
		}

		private void cmbType_SelectedIndexChanged(object sender, EventArgs e) {
			onTypeChanged?.Invoke(this, EventArgs.Empty);
		}

		private void txtValue_TextChanged(object sender, EventArgs e) {
			onValueChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
