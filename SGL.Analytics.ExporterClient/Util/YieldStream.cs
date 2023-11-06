using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient.Util {
	internal class YieldStream : Stream {
		private readonly Stream innerStream;

		public YieldStream(Stream innerStream) {
			this.innerStream = innerStream;
		}

		public override bool CanRead => innerStream.CanRead;

		public override bool CanSeek => innerStream.CanSeek;

		public override bool CanWrite => innerStream.CanWrite;

		public override long Length => innerStream.Length;

		public override long Position { get => innerStream.Position; set => innerStream.Position = value; }

		public override void Flush() {
			innerStream.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count) {
			return innerStream.Read(buffer, offset, count);
		}

		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
			await Task.Yield();
			return await base.ReadAsync(buffer, offset, count, cancellationToken);
		}

		public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
			await Task.Yield();
			return await base.ReadAsync(buffer, cancellationToken);
		}

		public override long Seek(long offset, SeekOrigin origin) {
			return innerStream.Seek(offset, origin);
		}

		public override void SetLength(long value) {
			innerStream.SetLength(value);
		}

		public override void Write(byte[] buffer, int offset, int count) {
			innerStream.Write(buffer, offset, count);
		}
	}

}
