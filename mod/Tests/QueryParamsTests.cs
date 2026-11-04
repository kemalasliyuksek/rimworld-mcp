using RimWorldMcp.Bridge.Core;
using Xunit;

namespace RimWorldMcp.Bridge.Tests
{
    public class QueryParamsTests
    {
        [Fact]
        public void DecodesPercentEncodingAndPlus()
        {
            QueryParams query = QueryParams.Parse("?q=steel+wall&name=%C5%9Eule");
            Assert.Equal("steel wall", query.GetString("q"));
            Assert.Equal("Şule", query.GetString("name"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("?")]
        public void EmptyQueriesHaveNoValues(string raw)
        {
            Assert.Null(QueryParams.Parse(raw).GetString("x"));
        }

        [Fact]
        public void EmptyValueCountsAsMissing()
        {
            Assert.Equal("fallback", QueryParams.Parse("x=").GetString("x", "fallback"));
        }

        [Fact]
        public void IntegerUsesFallbackWhenAbsent()
        {
            Assert.Equal(7, QueryParams.Parse("").GetInt("limit", 7, 1, 10));
        }

        [Theory]
        [InlineData("limit=abc")]
        [InlineData("limit=0")]
        [InlineData("limit=11")]
        [InlineData("limit=99999999999")]
        public void InvalidIntegersAreRejected(string raw)
        {
            Assert.Throws<BadRequestException>(() => QueryParams.Parse(raw).GetInt("limit", 5, 1, 10));
        }

        [Fact]
        public void ListsSplitOnCommasAndIgnoreBlanks()
        {
            Assert.Equal(new[] { "Loyalist", "Supremacist" }, QueryParams.Parse("memes=Loyalist%2C%20Supremacist%2C").GetList("memes"));
        }

        [Fact]
        public void AnAbsentListIsEmpty()
        {
            Assert.Empty(QueryParams.Parse("").GetList("memes"));
        }

        [Fact]
        public void PairsSplitIntoNamesAndValues()
        {
            var pairs = QueryParams.Parse("skills=Shooting%3A8%2C%20Medicine%3A5").GetPairs("skills");

            Assert.Equal(new[] { ("Shooting", "8"), ("Medicine", "5") }, pairs);
        }

        [Theory]
        [InlineData("skills=Shooting")]
        [InlineData("skills=Shooting%3A")]
        [InlineData("skills=%3A8")]
        public void MalformedPairsAreRejected(string raw)
        {
            Assert.Throws<BadRequestException>(() => QueryParams.Parse(raw).GetPairs("skills"));
        }

        [Fact]
        public void MissingRequiredParametersAreRejected()
        {
            QueryParams query = QueryParams.Parse("");
            Assert.Throws<BadRequestException>(() => query.RequireString("id"));
            Assert.Throws<BadRequestException>(() => query.RequireInt("x", 0, 10));
        }
    }
}
