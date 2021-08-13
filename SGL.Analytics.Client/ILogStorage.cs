﻿using System;
using System.Collections.Generic;
using System.IO;

namespace SGL.Analytics.Client {
	public interface ILogStorage {
		public interface ILogFile {
			public Guid ID { get; }
			public DateTime CreationTime { get; }
			public DateTime EndTime { get; }
			public Stream OpenRead();
			public void Remove();
		}

		(Stream, ILogFile) CreateLogFile();
		IEnumerable<ILogFile> EnumerateLogs();
	}
}
