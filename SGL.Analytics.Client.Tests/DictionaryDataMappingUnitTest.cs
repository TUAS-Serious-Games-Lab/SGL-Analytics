using SGL.Analytics.Client.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace SGL.Analytics.Client.Tests {
	public class DictionaryDataMappingUnitTest {
		public class TestUserData {
			public enum SomeEnum { A, B, C, D }
			public string Username { get; set; } = "Testuser";
			public int Age { get; set; } = 60;
			public double Height { get; set; } = 175.0;
			public SomeEnum SomeEnumValue { get; set; } = SomeEnum.C;
			public class SomeMeasurement {
				public int Value1 { get; set; } = 5;
				public double Value2 { get; set; } = 123.4;
				public string Location { get; set; } = "Somewhere";
			}
			public SomeMeasurement CurrentMeasurement { get; set; } = new();
			public Dictionary<DateTime, SomeMeasurement> TimeSeries { get; set; } = new() { [DateTime.Now.AddDays(-14)] = new(), [DateTime.Now.AddDays(-7)] = new() };
			public Dictionary<string, int> SomeRatings { get; set; } = new() { ["Foo"] = 9, ["Bar"] = 4 };
			public List<SomeMeasurement> SomeList { get; set; } = new() { new SomeMeasurement(), new SomeMeasurement() };
		}

		[Fact]
		public void ToDictionaryCanMapExampleTypeCorrectly() {
			TestUserData ud = new TestUserData();
			var dict = DictionaryDataMapping.ToDataMappingDictionary(ud);
			Assert.Equal(ud.Username, dict["Username"]);
			Assert.Equal(ud.Age, dict["Age"]);
			Assert.Equal(ud.Height, dict["Height"]);
			Assert.Equal(ud.SomeEnumValue, dict["SomeEnumValue"]);
			Assert.Equal(ud.CurrentMeasurement.Value1, (dict["CurrentMeasurement"] as IDictionary<string, object?>)?["Value1"]);
			Assert.Equal(ud.CurrentMeasurement.Value2, (dict["CurrentMeasurement"] as IDictionary<string, object?>)?["Value2"]);
			Assert.Equal(ud.CurrentMeasurement.Location, (dict["CurrentMeasurement"] as IDictionary<string, object?>)?["Location"]);

			Assert.All(ud.TimeSeries.Keys, k => Assert.Contains(k.ToString("O"), dict["TimeSeries"] as IDictionary<string, object?>));
			Assert.All(ud.SomeRatings.Keys, k => {
				var d = dict["SomeRatings"] as IDictionary<string, object?>;
				Assert.NotNull(d);
				Assert.Equal(ud.SomeRatings[k], Assert.Contains(k, d));
			});
			var sLst = dict["SomeList"] as IList<object>;
			for (int i = 0; i < ud.SomeList.Count; ++i) {
				var measurement = sLst?[i] as IDictionary<string, object?>;
				Assert.Equal(ud.SomeList[i].Value1, measurement?["Value1"]);
				Assert.Equal(ud.SomeList[i].Value2, measurement?["Value2"]);
				Assert.Equal(ud.SomeList[i].Location, measurement?["Location"]);
			}
		}
	}
}
