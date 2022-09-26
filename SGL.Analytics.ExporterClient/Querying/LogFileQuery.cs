using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	internal class LogFileQuery : ILogFileQuery {
		private Func<IEnumerable<DownstreamLogMetadataDTO>, IEnumerable<DownstreamLogMetadataDTO>> queryApplicator;

		internal LogFileQuery() : this(e => e) { }

		private LogFileQuery(Func<IEnumerable<DownstreamLogMetadataDTO>, IEnumerable<DownstreamLogMetadataDTO>> queryApplicator) {
			this.queryApplicator = queryApplicator;
		}

		private ILogFileQuery appendToQuery(Func<IEnumerable<DownstreamLogMetadataDTO>, IEnumerable<DownstreamLogMetadataDTO>> current) {
			var prev = queryApplicator;
			return new LogFileQuery(queryApplicator: q => current(prev(q)));
		}

		public ILogFileQuery StartedBefore(DateTime timestamp) {
			var utcTimestamp = timestamp.ToUniversalTime();
			return appendToQuery(q => q.Where(mdto => mdto.CreationTime > utcTimestamp));
		}

		public ILogFileQuery StartedAfter(DateTime timestamp) {
			var utcTimestamp = timestamp.ToUniversalTime();
			return appendToQuery(q => q.Where(mdto => mdto.CreationTime > utcTimestamp));
		}

		public ILogFileQuery EndedBefore(DateTime timestamp) {
			var utcTimestamp = timestamp.ToUniversalTime();
			return appendToQuery(q => q.Where(mdto => mdto.EndTime < utcTimestamp));
		}

		public ILogFileQuery EndedAfter(DateTime timestamp) {
			var utcTimestamp = timestamp.ToUniversalTime();
			return appendToQuery(q => q.Where(mdto => mdto.EndTime > utcTimestamp));
		}

		public ILogFileQuery UploadedBefore(DateTime timestamp) {
			var utcTimestamp = timestamp.ToUniversalTime();
			return appendToQuery(q => q.Where(mdto => mdto.UploadTime < utcTimestamp));
		}

		public ILogFileQuery UploadedAfter(DateTime timestamp) {
			var utcTimestamp = timestamp.ToUniversalTime();
			return appendToQuery(q => q.Where(mdto => mdto.UploadTime > utcTimestamp));
		}

		public ILogFileQuery UserOneOf(IEnumerable<Guid> userIds) {
			var ids = userIds.ToHashSet();
			return appendToQuery(q => q.Where(mdto => ids.Contains(mdto.UserId)));
		}

		internal IEnumerable<DownstreamLogMetadataDTO> ApplyTo(IEnumerable<DownstreamLogMetadataDTO> mdtos) {
			return queryApplicator(mdtos);
		}
	}
}
