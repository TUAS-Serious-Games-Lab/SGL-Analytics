using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.RekeyingTool {
	public class RekeyToolSettings {
		public Dictionary<string, Uri> Backends { get; set; } = new Dictionary<string, Uri>();
		public string AppName { get; set; } = "CarlsWortspiele";
	}
}
