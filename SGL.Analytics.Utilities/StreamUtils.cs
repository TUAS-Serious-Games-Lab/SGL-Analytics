using System.IO;
using System.Linq;
using Xunit;

namespace SGL.Analytics.Utilities {
	public static class StreamUtils {
		public static void AssertEqualContent(Stream expected, Stream actual) {
			byte[] expBuff = new byte[32];
			byte[] actBuff = new byte[32];
			var readBytesExp = expected.Read(expBuff, 0, expBuff.Length);
			var readBytesAct = actual.Read(actBuff, 0, actBuff.Length);
			while (readBytesExp > 0 && readBytesAct > 0) {
				var expBytes = expBuff.Take(readBytesExp);
				var actBytes = expBuff.Take(readBytesAct);
				Assert.Equal(expBytes, actBytes);
				readBytesExp = expected.Read(expBuff, 0, expBuff.Length);
				readBytesAct = actual.Read(actBuff, 0, actBuff.Length);
			}
			Assert.Equal(readBytesExp, readBytesAct);
		}
	}
}
