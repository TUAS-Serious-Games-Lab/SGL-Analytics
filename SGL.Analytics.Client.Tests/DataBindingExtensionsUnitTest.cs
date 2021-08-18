﻿using SGL.Analytics.DTO;
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

		private enum TestEnum { EnumValue }
		private class TestData {
			public TestEnum MyEnum { get; set; }
			public bool MyBool { get; set; }
			public short MyShort { get; set; }
			public int MyInt { get; set; }
			public long MyLong { get; set; }
			public double MyDouble { get; set; }
		}

		[Fact]
		public void MapObjectPropertiesCanMapPrimitivesCorrectly() {
			var headers = new FakeHeaders();
			var testData = new TestData() { MyEnum = TestEnum.EnumValue, MyBool = true, MyShort = 12345, MyInt = -1234567890, MyLong = 123456789012345, MyDouble = 123456.78912 };
			headers.MapObjectProperties(testData);
			Assert.Equal("EnumValue", headers.GetValues("MyEnum").Single());
			Assert.Equal("True", headers.GetValues("MyBool").Single());
			Assert.Equal("12345", headers.GetValues("MyShort").Single());
			Assert.Equal("-1234567890", headers.GetValues("MyInt").Single());
			Assert.Equal("123456789012345", headers.GetValues("MyLong").Single());
			Assert.Equal("123456.78912", headers.GetValues("MyDouble").Single());
		}
	}
}
