using RandomDataGenerator.FieldOptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WireMock.Types;

namespace SGL.Analytics.Client.Tests {
	public static class MultipartBodySplitter {
		private static readonly byte[] BoundaryMarker = Encoding.ASCII.GetBytes("--");
		private static readonly byte[] CrLf = new byte[] { 0x0D, 0x0A };
		public static IEnumerable<(byte[] Content, Dictionary<string, string> SectionHeaders)> SplitMultipartBody(byte[] requestBody, string boundary) {
			var boundaryBytes = Encoding.ASCII.GetBytes(boundary);
			var boundaryIndices = SearchAll(requestBody, boundaryBytes);
			var segments = SplitAtIndices(requestBody, boundaryIndices);
			var lastSegment = segments.Last();
			if (!segments.First().SequenceEqual(BoundaryMarker)) {
				throw new ArgumentException("Segment before first boundary is not empty.");
			}
			CheckLastSegment(boundaryBytes, lastSegment);
			segments = segments.Skip(1).SkipLast(1);
			return segments.Select(StripEndCrLfAndMarker).Select(segment => ParseSection(segment, boundaryBytes));
		}

		private static void CheckLastSegment(byte[] boundaryBytes, IEnumerable<byte> lastSegment) {
			if (!lastSegment.Take(boundaryBytes.Length).SequenceEqual(boundaryBytes)) {
				throw new ArgumentException("Last egment doesn't start with boundary.");
			}
			lastSegment = lastSegment.Skip(boundaryBytes.Length);
			lastSegment = SkipLinearWhitespace(lastSegment);
			if (!lastSegment.Take(BoundaryMarker.Length).SequenceEqual(BoundaryMarker)) {
				throw new ArgumentException("Last boundary line doesn't end with '--'.");
			}
			lastSegment = lastSegment.Skip(BoundaryMarker.Length);
			if (lastSegment.Any() && !lastSegment.SequenceEqual(CrLf)) {
				throw new ArgumentException("Unexpected data after end marker.");
			}
		}

		private static (byte[] Content, Dictionary<string, string> SectionHeaders) ParseSection(IEnumerable<byte> segmentContent, byte[] boundaryBytes) {
			if (!segmentContent.Take(boundaryBytes.Length).SequenceEqual(boundaryBytes)) {
				throw new ArgumentException("Segment doesn't start with boundary.");
			}
			segmentContent = segmentContent.Skip(boundaryBytes.Length);
			segmentContent = SkipLinearWhitespace(segmentContent);
			if (!segmentContent.Take(CrLf.Length).SequenceEqual(CrLf)) {
				throw new ArgumentException("No CR LF to end boundary line.");
			}
			segmentContent = segmentContent.Skip(CrLf.Length);
			Dictionary<string, string> sectionHeaders = new Dictionary<string, string>();
			for (; ; ) {
				int crLfPos = SearchNext(segmentContent, CrLf);
				var lineBytes = segmentContent.Take(crLfPos);
				segmentContent = segmentContent.Skip(crLfPos + CrLf.Length);
				if (lineBytes.Count() == 0) break;
				var line = Encoding.ASCII.GetString(lineBytes.ToArray());
				var splitLine = line.Split(": ", 2);
				sectionHeaders.Add(splitLine[0], splitLine[1]);
			}
			return (Content: segmentContent.ToArray(), SectionHeaders: sectionHeaders);
		}

		private static IEnumerable<byte> SkipLinearWhitespace(IEnumerable<byte> segmentContent) {
			while (segmentContent.First() is ((byte)' ') or ((byte)'\t')) segmentContent = segmentContent.Skip(1);
			return segmentContent;
		}

		private static IEnumerable<byte> StripEndCrLfAndMarker(IEnumerable<byte> segmentContent) {
			if (!segmentContent.TakeLast(CrLf.Length + BoundaryMarker.Length).SequenceEqual(CrLf.Concat(BoundaryMarker))) {
				throw new ArgumentException("Segment doesn't end with CR LF and '--'.");
			}
			return segmentContent.SkipLast(CrLf.Length + BoundaryMarker.Length);
		}

		private static IEnumerable<IEnumerable<byte>> SplitAtIndices(byte[] input, IEnumerable<int> indices) {
			int startIndex = 0;
			foreach (var curIndex in indices) {
				yield return input.Skip(startIndex).Take(curIndex - startIndex);
				startIndex = curIndex;
			}
			yield return input.Skip(startIndex);
		}

		private static IEnumerable<int> SearchAll(byte[] haystack, byte[] needle) {
			for (int i = 0; i < haystack.Length;) {
				var pos = SearchNext(haystack, needle, i);
				if (pos < 0) {
					yield break;
				}
				else {
					yield return pos;
					i = pos + needle.Length;
				}
			}
			yield break;
		}

		private static int SearchNext(IEnumerable<byte> haystack, byte[] needle, int startOffset = 0) {
			// For simplicity, use naive algorithm, although it runs in O(haystack.length * needle.length).
			// It should be fine for the testing purposes, both arrays, especially needle, are relatively small.
			int haystackLength = haystack.Count();
			for (int i = startOffset; i <= haystackLength - needle.Length; ++i) {
				if (haystack.Skip(i).Take(needle.Length).SequenceEqual(needle)) {
					return i;
				}
			}
			return -1;
		}

		public static string GetBoundaryFromContentType(string? contentType) {
			if (contentType == null) {
				throw new ArgumentNullException(nameof(contentType));
			}
			if (!contentType.StartsWith("multipart")) {
				throw new ArgumentException("Not a multipart content type.");
			}
			const string boundaryKey = "boundary=\"";
			var startPos = contentType.IndexOf(boundaryKey);
			if (startPos < 0) {
				throw new ArgumentException("Not boundary in content type.");
			}
			startPos += boundaryKey.Length;
			var endPos = contentType.IndexOf("\"", startPos + 1);
			if (endPos < 0) {
				throw new ArgumentException("No end quote for boundary in content type.");
			}
			return contentType.Substring(startPos, endPos - startPos);
		}
	}
}
