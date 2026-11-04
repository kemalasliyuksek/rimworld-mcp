using RimWorldMcp.Bridge.Core;
using Xunit;

namespace RimWorldMcp.Bridge.Tests
{
    public class HourRangeTests
    {
        [Fact]
        public void EndHourIsNotIncluded()
        {
            Assert.Equal(new[] { 6, 7, 8 }, HourRange.Parse("6-9"));
        }

        [Fact]
        public void WrapsPastMidnight()
        {
            Assert.Equal(new[] { 22, 23, 0, 1 }, HourRange.Parse("22-2"));
        }

        [Fact]
        public void TwentyFourEndsAtMidnight()
        {
            Assert.Equal(new[] { 22, 23 }, HourRange.Parse("22-24"));
        }

        [Fact]
        public void EndingAtZeroStopsAtMidnight()
        {
            Assert.Equal(new[] { 22, 23 }, HourRange.Parse("22-0"));
        }

        [Fact]
        public void ZeroToTwentyFourIsTheWholeDay()
        {
            Assert.Equal(24, HourRange.Parse("0-24").Count);
        }

        [Fact]
        public void AllowsSpaces()
        {
            Assert.Equal(new[] { 1, 2 }, HourRange.Parse(" 1 - 3 "));
        }

        [Theory]
        [InlineData("5")]
        [InlineData("5-5")]
        [InlineData("24-3")]
        [InlineData("-1-3")]
        [InlineData("3-25")]
        [InlineData("a-b")]
        [InlineData("1-2-3")]
        [InlineData("")]
        public void RejectsAnythingElse(string text)
        {
            Assert.Throws<BadRequestException>(() => HourRange.Parse(text));
        }
    }
}
