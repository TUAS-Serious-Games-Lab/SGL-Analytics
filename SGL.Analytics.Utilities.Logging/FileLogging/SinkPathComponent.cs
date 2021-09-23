using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SGL.Analytics.Utilities.Logging.FileLogging {
	public struct SinkPathComponent {
		public enum Mode { Directory, File }
		public bool TimeBased { get; }
		public Func<LogMessage, Mode, string> PathNameGen { get; }

		public SinkPathComponent(Func<LogMessage, Mode, string> pathNameGen, bool timeBased = false) {
			TimeBased = timeBased;
			PathNameGen = pathNameGen;
		}

		public string GetFileName(LogMessage msg) {
			var str = PathNameGen(msg, Mode.File);
			return new string(str.Select(c => c switch {
				'.' => c,
				'-' => c,
				'(' => c,
				')' => c,
				'[' => c,
				']' => c,
				_ when char.IsLetterOrDigit(c) => c,
				_ => '_'
			}).ToArray());
		}
		public string GetDirName(LogMessage msg) {
			var str = PathNameGen(msg, Mode.Directory);
			return new string(str.Select(c => c switch {
				'.' => c,
				'-' => c,
				'(' => c,
				')' => c,
				'[' => c,
				']' => c,
				'/' => c,
				'\\' => c,
				_ when char.IsLetterOrDigit(c) => c,
				_ => '_'
			}).ToArray());
		}

		public static readonly Dictionary<string, SinkPathComponent> NamedInstances = new Dictionary<string, SinkPathComponent>(StringComparer.OrdinalIgnoreCase) {
			["$Category"] = new SinkPathComponent((msg, _) => msg.Category),
			["$Level"] = new SinkPathComponent((msg, _) => msg.Level.ToString()),
			["$EventId"] = new SinkPathComponent((msg, _) => msg.EventId.ToString()),
			["$Year"] = new SinkPathComponent((msg, _) => msg.Time.Year.ToString(), true),
			["$Month"] = new SinkPathComponent((msg, _) => msg.Time.Month.ToString(), true),
			["$Day"] = new SinkPathComponent((msg, _) => msg.Time.Day.ToString(), true),
			["$YearMonth"] = new SinkPathComponent((msg, mode) => mode == Mode.Directory ? msg.Time.ToString($"yyyy{Path.DirectorySeparatorChar}MM") : msg.Time.ToString("yyyy-MM"), true),
			["$Date"] = new SinkPathComponent((msg, mode) => mode == Mode.Directory ? msg.Time.ToString($"yyyy{Path.DirectorySeparatorChar}MM{Path.DirectorySeparatorChar}dd") : msg.Time.ToString("yyyy-MM-dd"), true),
			["$FlatDate"] = new SinkPathComponent((msg, _) => msg.Time.ToString("yyyyMMdd"), true),
			["$Scopes"] = new SinkPathComponent((msg, mode) => string.Join(mode == Mode.Directory ? Path.DirectorySeparatorChar : '_', msg.Scopes)),
			["$ExceptionType"] = new SinkPathComponent((msg, _) => msg.Exception?.GetType()?.Name ?? "null"),
		};

		public static SinkPathComponent FromNameOrLiteral(string nameOrLiteral) {
			if (NamedInstances.TryGetValue(nameOrLiteral, out var component)) {
				return component;
			}
			else {
				return new SinkPathComponent((_, _) => nameOrLiteral);
			}
		}
	}
}
