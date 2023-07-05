using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SGL.Analytics.KeyTool {
	public class KeyToolSettings {
		[JsonConverter(typeof(JsonStringEnumConverter))]
		public KeyType DefaultKeyType { get; set; } = KeyType.EllipticCurves;
		public string DefaultCurveName { get; set; } = "secp521r1";
		public int DefaultRsaKeyStrength { get; set; } = 4096;
		public List<DistinguishedNameComponent> InitialDistinguishedName { get; set; } = new List<DistinguishedNameComponent> {
			new DistinguishedNameComponent{ Type = "C"},
			new DistinguishedNameComponent{ Type = "O"},
			new DistinguishedNameComponent{ Type = "OU"},
			new DistinguishedNameComponent{ Type = "CN"}
		};

		public class DistinguishedNameComponent {
			public string Type { get; set; } = "";
			public string Value { get; set; } = "";
		}
	}
}
