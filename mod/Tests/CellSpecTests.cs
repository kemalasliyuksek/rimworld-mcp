using System.Collections.Generic;
using RimWorldMcp.Bridge.Core;
using Xunit;

namespace RimWorldMcp.Bridge.Tests
{
    public class CellSpecTests
    {
        [Fact]
        public void ReadsOneCell()
        {
            Assert.Equal(new[] { (10, 20) }, CellSpec.Parse("10,20"));
        }

        [Fact]
        public void FillsARectangle()
        {
            Assert.Equal(new[] { (1, 1), (2, 1), (1, 2), (2, 2) }, CellSpec.Parse("1,1-2,2"));
        }

        [Fact]
        public void CornersCanComeInAnyOrder()
        {
            Assert.Equal(CellSpec.Parse("1,1-2,2"), CellSpec.Parse("2,2-1,1"));
        }

        [Fact]
        public void OutlineLeavesTheMiddleEmpty()
        {
            List<(int X, int Z)> cells = CellSpec.Parse("outline:0,0-2,2");
            Assert.Equal(8, cells.Count);
            Assert.DoesNotContain((1, 1), cells);
        }

        [Fact]
        public void OutlineOfALineIsTheLine()
        {
            Assert.Equal(new[] { (0, 0), (1, 0), (2, 0) }, CellSpec.Parse("outline:0,0-2,0"));
        }

        [Fact]
        public void CombinesPartsWithoutRepeatingCells()
        {
            Assert.Equal(new[] { (1, 1), (2, 1), (3, 3) }, CellSpec.Parse(" 1,1-2,1 ; 1,1 ; 3,3 ;"));
        }

        [Fact]
        public void AcceptsTheLimit()
        {
            Assert.Equal(CellSpec.MaxCells, CellSpec.Parse("0,0-49,99").Count);
            Assert.Equal(CellSpec.MaxCells, CellSpec.Parse("outline:0,0-1250,1250").Count);
        }

        [Theory]
        [InlineData("0,0-50,99")]
        [InlineData("0,0-2147483646,2147483646")]
        [InlineData("0,0-2147483647,2147483647")]
        [InlineData("outline:0,0-2147483646,2147483646")]
        [InlineData("outline:0,0-2147483647,2147483647")]
        [InlineData("outline:0,0-1251,1251")]
        [InlineData("0,0-49,99;60,60")]
        public void RejectsTooManyWithoutTryingThem(string text)
        {
            Assert.Throws<BadRequestException>(() => CellSpec.Parse(text));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ; ")]
        [InlineData("5")]
        [InlineData("a,b")]
        [InlineData("-1,2")]
        [InlineData("1,2-3")]
        [InlineData("1,2-3,4-5,6")]
        [InlineData("outline:1,2")]
        public void RejectsAnythingElse(string text)
        {
            Assert.Throws<BadRequestException>(() => CellSpec.Parse(text));
        }
    }
}
