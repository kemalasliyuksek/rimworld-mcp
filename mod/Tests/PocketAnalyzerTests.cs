using System;
using RimWorldMcp.Bridge.Core;
using Xunit;

namespace RimWorldMcp.Bridge.Tests
{
    public class PocketAnalyzerTests
    {
        private const int MapSize = 250;
        private const int Step = 4;

        private static int Samples => MapSize / Step;

        private static bool[,] Draw(Func<int, int, bool> isRock)
        {
            var grid = new bool[Samples, Samples];
            for (int x = 0; x < Samples; x++)
            {
                for (int z = 0; z < Samples; z++)
                {
                    grid[x, z] = isRock(x * Step, z * Step);
                }
            }
            return grid;
        }

        /// <summary>A thick ring around the map centre with a gap facing the given way.</summary>
        private static bool[,] Crescent(double openingDegrees, double gapDegrees)
        {
            return Draw((x, z) =>
            {
                double dx = x - 125;
                double dz = z - 125;
                double distance = Math.Sqrt(dx * dx + dz * dz);
                if (distance < 30 || distance > 55)
                {
                    return false;
                }
                double angle = Math.Atan2(dz, dx) * 180 / Math.PI;
                double offset = Math.Abs(((angle - openingDegrees + 540) % 360) - 180);
                return offset > gapDegrees / 2;
            });
        }

        [Theory]
        [InlineData(0, "E")]
        [InlineData(90, "N")]
        [InlineData(180, "W")]
        [InlineData(270, "S")]
        public void FindsACrescentAndWhichWayItOpens(double openingDegrees, string expected)
        {
            PocketResult result = PocketAnalyzer.Analyze(Crescent(openingDegrees, 90), Step);

            Assert.True(result.Score >= 70, $"Score was {result.Score}");
            Assert.Equal(expected, result.OpeningDirection);
            Assert.Equal(1, result.Openings);
            Assert.InRange(result.EnclosurePercent, 60, 85);
            Assert.InRange(result.OpeningDegrees, 70, 140);
        }

        [Fact]
        public void ThePocketCentreIsInsideTheCrescent()
        {
            PocketResult result = PocketAnalyzer.Analyze(Crescent(0, 90), Step);

            double distanceFromMiddle = Math.Sqrt(Math.Pow(result.CenterX - 125, 2) + Math.Pow(result.CenterZ - 125, 2));
            Assert.True(distanceFromMiddle < 30, $"Centre was {distanceFromMiddle} cells from the middle");
            Assert.True(result.ClearRadius >= 10);
        }

        [Fact]
        public void FlatGroundHasNoPocket()
        {
            PocketResult result = PocketAnalyzer.Analyze(Draw((x, z) => false), Step);

            Assert.Equal(0, result.Score);
            Assert.Null(result.OpeningDirection);
            Assert.Equal(0, result.RockPercent);
        }

        [Fact]
        public void SolidRockHasNoPocket()
        {
            PocketResult result = PocketAnalyzer.Analyze(Draw((x, z) => true), Step);

            Assert.Equal(0, result.Score);
            Assert.Equal(100, result.RockPercent);
        }

        [Fact]
        public void AStraightCliffDoesNotCount()
        {
            // Rock along one side only: it protects your back, but three sides stay open.
            PocketResult result = PocketAnalyzer.Analyze(Draw((x, z) => x > 180), Step);

            Assert.True(result.Score < 30, $"Score was {result.Score}");
        }

        [Fact]
        public void AWallWithManyGapsScoresLowerThanOneWithASingleOpening()
        {
            bool[,] manyGaps = Draw((x, z) =>
            {
                double dx = x - 125;
                double dz = z - 125;
                double distance = Math.Sqrt(dx * dx + dz * dz);
                double angle = (Math.Atan2(dz, dx) * 180 / Math.PI) + 180;
                return distance >= 30 && distance <= 55 && angle % 90 > 22;
            });

            double scattered = PocketAnalyzer.Analyze(manyGaps, Step).Score;
            double crescent = PocketAnalyzer.Analyze(Crescent(0, 90), Step).Score;

            Assert.True(scattered < crescent / 2, $"Scattered {scattered}, crescent {crescent}");
        }

        [Fact]
        public void AClosedRingScoresLowerThanACrescent()
        {
            double closed = PocketAnalyzer.Analyze(Crescent(0, 0), Step).Score;
            double open = PocketAnalyzer.Analyze(Crescent(0, 90), Step).Score;

            Assert.True(closed < open, $"Closed {closed}, open {open}");
        }

        [Fact]
        public void ThinRidgesDoNotCountAsWalls()
        {
            // A shape like the crescent, but only one cell-sample thick, offers nothing to dig into.
            bool[,] thin = Draw((x, z) =>
            {
                double dx = x - 125;
                double dz = z - 125;
                double distance = Math.Sqrt(dx * dx + dz * dz);
                double angle = Math.Abs(Math.Atan2(dz, dx) * 180 / Math.PI);
                return distance >= 35 && distance <= 38 && angle > 45;
            });

            Assert.Equal(0, PocketAnalyzer.Analyze(thin, Step).Score);
        }

        [Fact]
        public void TooMuchRockOverallLowersTheScore()
        {
            // The same pocket, but buried in a mountain that leaves little room to farm.
            bool[,] crescent = Crescent(0, 90);
            bool[,] buried = Draw((x, z) => crescent[x / Step, z / Step] || Math.Sqrt(Math.Pow(x - 125, 2) + Math.Pow(z - 125, 2)) > 60);

            double mild = PocketAnalyzer.Analyze(crescent, Step).Score;
            double heavy = PocketAnalyzer.Analyze(buried, Step).Score;

            Assert.True(heavy < mild, $"Buried {heavy}, mild {mild}");
        }

        [Fact]
        public void AnOpeningThatWrapsPastEastIsTreatedAsOneOpening()
        {
            // Index 0 of the ray circle points east, so this gap crosses the point where the
            // search starts counting.
            PocketResult result = PocketAnalyzer.Analyze(Crescent(0, 120), Step);

            Assert.Equal(1, result.Openings);
            Assert.Equal("E", result.OpeningDirection);
        }

        [Fact]
        public void ResultsDoNotDependOnTheSamplingResolution()
        {
            Func<int, bool[,]> render = step =>
            {
                int size = MapSize / step;
                var grid = new bool[size, size];
                bool[,] reference = Crescent(90, 90);
                for (int x = 0; x < size; x++)
                {
                    for (int z = 0; z < size; z++)
                    {
                        grid[x, z] = reference[Math.Min(x * step / Step, Samples - 1), Math.Min(z * step / Step, Samples - 1)];
                    }
                }
                return grid;
            };

            PocketResult coarse = PocketAnalyzer.Analyze(render(4), 4);
            PocketResult fine = PocketAnalyzer.Analyze(render(2), 2);

            Assert.Equal(coarse.OpeningDirection, fine.OpeningDirection);
            Assert.InRange(fine.EnclosurePercent, coarse.EnclosurePercent - 15, coarse.EnclosurePercent + 15);
        }
    }
}
