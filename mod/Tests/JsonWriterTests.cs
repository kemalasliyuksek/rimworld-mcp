using System.Text.Json;
using RimWorldMcp.Bridge.Core;
using Xunit;

namespace RimWorldMcp.Bridge.Tests
{
    public class JsonWriterTests
    {
        [Fact]
        public void WritesNestedStructuresAsValidJson()
        {
            var json = new JsonWriter();
            json.BeginObject();
            json.Prop("name", "Kemal");
            json.Key("skills").BeginArray();
            json.BeginObject().Prop("level", 12).EndObject();
            json.BeginObject().Prop("level", 3).EndObject();
            json.EndArray();
            json.Key("empty_list").BeginArray().EndArray();
            json.Key("empty_object").BeginObject().EndObject();
            json.Prop("flag", true);
            json.EndObject();

            Assert.Equal(
                "{\"name\":\"Kemal\",\"skills\":[{\"level\":12},{\"level\":3}],\"empty_list\":[],\"empty_object\":{},\"flag\":true}",
                json.ToString());
        }

        [Theory]
        [InlineData("say \"hi\"")]
        [InlineData("back\\slash")]
        [InlineData("line\nbreak\r\ttab")]
        [InlineData("control\u0001char")]
        [InlineData("Türkçe karakterler: ğüşıöç İ 日本語")]
        public void RoundTripsStringsThroughAStandardParser(string text)
        {
            string written = new JsonWriter().Value(text).ToString();
            Assert.Equal(text, JsonDocument.Parse(written).RootElement.GetString());
        }

        [Fact]
        public void NullStringBecomesJsonNull()
        {
            Assert.Equal("null", new JsonWriter().Value((string)null).ToString());
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void NonFiniteNumbersBecomeNullBecauseJsonCannotExpressThem(double value)
        {
            Assert.Equal("null", new JsonWriter().Value(value).ToString());
        }

        [Fact]
        public void NumbersIgnoreTheCurrentCulture()
        {
            var original = System.Globalization.CultureInfo.CurrentCulture;
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            try
            {
                Assert.Equal("1234.5", new JsonWriter().Value(1234.5).ToString());
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = original;
            }
        }

        [Fact]
        public void LargeNumbersDoNotUseExponents()
        {
            Assert.Equal("123456789012", new JsonWriter().Value(123456789012d).ToString());
        }
    }
}
