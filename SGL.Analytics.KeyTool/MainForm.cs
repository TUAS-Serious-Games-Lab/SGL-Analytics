using SGL.Utilities.Crypto.Keys;
using System.Numerics;

namespace SGL.Analytics.KeyTool {
	public partial class MainForm : Form {
		private const string defaultCurveName = "secp521r1";
		private int rsaKeyStrength;

		public MainForm() {
			InitializeComponent();
			cmbNamedCurve.Items.Clear();
			cmbNamedCurve.Items.AddRange(KeyPair.GetSupportedNamedEllipticCurves().OrderByDescending(curve => curve.KeyLength).Select(curve => curve.Name).ToArray());
			var defaultCurveIndex = cmbNamedCurve.Items.IndexOf(defaultCurveName);
			if (defaultCurveIndex >= 0) {
				cmbNamedCurve.SelectedIndex = defaultCurveIndex;
			}
		}

		private void spinRsaKeyStrengthExp_ValueChanged(object sender, EventArgs e) {
			rsaKeyStrength = 1 << (int)(spinRsaKeyStrengthExp.Value);
			lblRsaKeyStrength.Text = $"{rsaKeyStrength}";
		}
	}
}