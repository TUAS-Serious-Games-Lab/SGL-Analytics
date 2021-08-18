using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SGL.Analytics.Client.Tests {
	public class DataBindingExtensionsUnitTest {
		private class FakeHeaders : HttpHeaders { }

		[Fact]
		public void MapObjectPropertiesCanMapLogMetadataDTOCorrectly() {
			var headers = new FakeHeaders();
			LogMetadataDTO dto = new LogMetadataDTO(AppName: "UnitTestDummy", UserId: Guid.NewGuid(), LogFileId: Guid.NewGuid(), CreationTime: DateTime.Now.AddMinutes(-5), EndTime: DateTime.Now);
			headers.MapObjectProperties(dto);
			Assert.Equal(dto.AppName, headers.GetValues("AppName").Single());
			Assert.Equal(dto.UserId, Guid.Parse(headers.GetValues("UserId").Single()));
			Assert.Equal(dto.LogFileId, Guid.Parse(headers.GetValues("LogFileId").Single()));
			Assert.Equal(dto.CreationTime, DateTime.Parse(headers.GetValues("CreationTime").Single()));
			Assert.Equal(dto.EndTime, DateTime.Parse(headers.GetValues("EndTime").Single()));

		}
	}
}
