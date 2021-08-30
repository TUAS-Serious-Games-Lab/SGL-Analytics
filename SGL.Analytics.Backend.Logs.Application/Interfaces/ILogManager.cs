using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Logs.Application.Model;
using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Interfaces {
	public interface ILogManager {
		Task<LogFile> IngestLogAsync(LogMetadataDTO logMetaDTO, Stream logContent);
	}
}
