using System;

namespace RimWorldMcp.Bridge.Core
{
    public sealed class PocketResult
    {
        /// <summary>0 to 100. Higher means a better defensible pocket.</summary>
        public double Score;

        /// <summary>Pocket centre, in map cells.</summary>
        public int CenterX;

        public int CenterZ;

        /// <summary>Share of directions from the centre that are walled off by solid rock.</summary>
        public double EnclosurePercent;

        public string OpeningDirection;

        /// <summary>Width of the widest opening. Zero when the pocket is fully enclosed.</summary>
        public double OpeningDegrees;

        /// <summary>How many separate gaps the rock wall has, as seen from the centre.</summary>
        public int Openings;

        /// <summary>Distance from the centre to the nearest wall, in cells.</summary>
        public double ClearRadius;

        /// <summary>Share of the whole map covered by rock.</summary>
        public double RockPercent;
    }

    /// <summary>
    /// Looks for a crescent: open ground with solid rock on most sides and one wide opening, so a
    /// base can be built with a single front to defend. It casts rays out from each candidate spot
    /// and measures which directions are walled off.
    /// </summary>
    public static class PocketAnalyzer
    {
        // Narrow gaps must not slip between rays, or a leaky wall looks like a crescent.
        private const int RayCount = 64;

        // These are in map cells, so the result does not depend on the sampling resolution.
        private const double MaxWallDistance = 45;
        private const double MinWallThickness = 8;
        private const double MinClearRadius = 10;
        private const double FullRoomRadius = 22;
        private const double MapEdgeMargin = 25;
        private const double MinWallMassCells = 800;

        // Rock this close together counts as one wall: a short gap can be closed with a door, while
        // separate hills that merely stand around a spot cannot be defended as one wall.
        private const double MaxGapWithinWall = 10;

        private static readonly string[] CompassNames = { "E", "NE", "N", "NW", "W", "SW", "S", "SE" };

        /// <param name="rock">Rock grid indexed as [x, z], with z increasing to the north.</param>
        /// <param name="cellsPerSample">How many map cells one grid entry stands for.</param>
        public static PocketResult Analyze(bool[,] rock, int cellsPerSample)
        {
            int width = rock.GetLength(0);
            int height = rock.GetLength(1);
            double rockPercent = CountRockPercent(rock);
            var best = new PocketResult { RockPercent = rockPercent };

            int[,] masses = LabelRockMasses(rock, cellsPerSample, out int[] massCells);
            int margin = (int)Math.Ceiling(MapEdgeMargin / cellsPerSample);
            int centerStep = Math.Max(1, 8 / cellsPerSample);
            for (int x = margin; x < width - margin; x += centerStep)
            {
                for (int z = margin; z < height - margin; z += centerStep)
                {
                    if (rock[x, z])
                    {
                        continue;
                    }
                    PocketResult candidate = Evaluate(rock, masses, massCells, x, z, cellsPerSample, rockPercent);
                    if (candidate != null && candidate.Score > best.Score)
                    {
                        best = candidate;
                    }
                }
            }
            return best;
        }

        private static PocketResult Evaluate(bool[,] rock, int[,] masses, int[] massCells, int centerX, int centerZ, int cellsPerSample, double rockPercent)
        {
            var hitMass = new int[RayCount];
            var raysPerMass = new int[massCells.Length];
            double clearRadius = MaxWallDistance;

            for (int i = 0; i < RayCount; i++)
            {
                double angle = 2 * Math.PI * i / RayCount;
                double distance = WallDistance(rock, masses, centerX, centerZ, angle, cellsPerSample, out hitMass[i]);
                if (distance < 0)
                {
                    continue;
                }
                raysPerMass[hitMass[i]]++;
                clearRadius = Math.Min(clearRadius, distance);
            }

            // A crescent is a single mass of rock, so only the one that wraps around the spot the most
            // counts as the wall. Other hills in the way are obstacles and not protection.
            int wall = -1;
            for (int mass = 0; mass < raysPerMass.Length; mass++)
            {
                if (massCells[mass] >= MinWallMassCells && raysPerMass[mass] > 0 && (wall < 0 || raysPerMass[mass] > raysPerMass[wall]))
                {
                    wall = mass;
                }
            }
            if (wall < 0 || clearRadius < MinClearRadius)
            {
                return null;
            }

            var walled = new bool[RayCount];
            for (int i = 0; i < RayCount; i++)
            {
                walled[i] = hitMass[i] == wall;
            }
            int walledCount = raysPerMass[wall];

            FindOpenings(walled, out int openings, out int widestStart, out int widestLength);
            int openRays = RayCount - walledCount;
            double enclosure = walledCount / (double)RayCount;

            double enclosureFactor = EnclosureFactor(enclosure);
            double singleOpeningFactor = openRays == 0 ? 0.5 : (widestLength / (double)openRays) / openings;
            double roomFactor = 0.5 + 0.5 * Math.Min(1, clearRadius / FullRoomRadius);
            double score = 100 * enclosureFactor * singleOpeningFactor * roomFactor * RockShareFactor(rockPercent);

            return new PocketResult
            {
                Score = Math.Round(score, 1),
                CenterX = centerX * cellsPerSample,
                CenterZ = centerZ * cellsPerSample,
                EnclosurePercent = Math.Round(enclosure * 100, 1),
                OpeningDirection = openRays == 0 ? null : CompassName(widestStart + (widestLength - 1) / 2.0),
                OpeningDegrees = widestLength * 360.0 / RayCount,
                Openings = openings,
                ClearRadius = Math.Round(clearRadius, 1),
                RockPercent = rockPercent,
            };
        }

        /// <returns>Distance to a wall thick enough to count as solid, or -1 if there is none.</returns>
        private static double WallDistance(bool[,] rock, int[,] masses, int centerX, int centerZ, double angle, int cellsPerSample, out int hitMass)
        {
            hitMass = -1;
            int width = rock.GetLength(0);
            int height = rock.GetLength(1);
            double stepX = Math.Cos(angle);
            double stepZ = Math.Sin(angle);
            double maxSteps = (MaxWallDistance + MinWallThickness) / cellsPerSample;
            int requiredThickness = (int)Math.Ceiling(MinWallThickness / cellsPerSample);
            int thickness = 0;
            double firstHit = -1;

            for (double step = 1; step <= maxSteps; step++)
            {
                int x = centerX + (int)Math.Round(stepX * step);
                int z = centerZ + (int)Math.Round(stepZ * step);
                if (x < 0 || z < 0 || x >= width || z >= height)
                {
                    // Enemies arrive from the map edge, so a wall that runs out here protects nothing.
                    return -1;
                }

                if (rock[x, z])
                {
                    if (thickness == 0)
                    {
                        firstHit = step * cellsPerSample;
                        if (firstHit > MaxWallDistance)
                        {
                            return -1;
                        }
                    }
                    thickness++;
                    if (thickness >= requiredThickness)
                    {
                        hitMass = masses[x, z];
                        return firstHit;
                    }
                }
                else
                {
                    thickness = 0;
                    firstHit = -1;
                }
            }
            return -1;
        }

        /// <summary>Groups rock samples into masses, treating short gaps as part of the same mass.</summary>
        private static int[,] LabelRockMasses(bool[,] rock, int cellsPerSample, out int[] massCells)
        {
            int width = rock.GetLength(0);
            int height = rock.GetLength(1);
            int reach = Math.Max(1, (int)Math.Ceiling(MaxGapWithinWall / cellsPerSample));
            var labels = new int[width, height];
            var sizes = new System.Collections.Generic.List<int>();
            var pending = new System.Collections.Generic.Stack<(int X, int Z)>();

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    labels[x, z] = -1;
                }
            }

            for (int startX = 0; startX < width; startX++)
            {
                for (int startZ = 0; startZ < height; startZ++)
                {
                    if (!rock[startX, startZ] || labels[startX, startZ] >= 0)
                    {
                        continue;
                    }

                    int label = sizes.Count;
                    int samples = 0;
                    labels[startX, startZ] = label;
                    pending.Push((startX, startZ));
                    while (pending.Count > 0)
                    {
                        (int x, int z) = pending.Pop();
                        samples++;
                        for (int nextX = Math.Max(0, x - reach); nextX <= Math.Min(width - 1, x + reach); nextX++)
                        {
                            for (int nextZ = Math.Max(0, z - reach); nextZ <= Math.Min(height - 1, z + reach); nextZ++)
                            {
                                if (rock[nextX, nextZ] && labels[nextX, nextZ] < 0)
                                {
                                    labels[nextX, nextZ] = label;
                                    pending.Push((nextX, nextZ));
                                }
                            }
                        }
                    }
                    sizes.Add(samples * cellsPerSample * cellsPerSample);
                }
            }

            massCells = sizes.ToArray();
            return labels;
        }

        private static void FindOpenings(bool[] walled, out int openings, out int widestStart, out int widestLength)
        {
            openings = 0;
            widestStart = 0;
            widestLength = 0;

            // Starting from a walled ray keeps a gap that wraps past index 0 in one piece.
            int origin = Array.IndexOf(walled, true);
            int current = 0;
            for (int i = 1; i <= walled.Length; i++)
            {
                int index = (origin + i) % walled.Length;
                if (!walled[index])
                {
                    if (current == 0)
                    {
                        openings++;
                    }
                    current++;
                    if (current > widestLength)
                    {
                        widestLength = current;
                        widestStart = index - current + 1;
                    }
                }
                else
                {
                    current = 0;
                }
            }
        }

        // A closed ring has to be mined open, and little enclosure offers little protection. The
        // best shape walls off most directions while leaving a usable way in.
        private static double EnclosureFactor(double enclosure)
        {
            if (enclosure < 0.4)
            {
                return 0;
            }
            if (enclosure < 0.6)
            {
                return (enclosure - 0.4) / 0.2;
            }
            if (enclosure <= 0.88)
            {
                return 1;
            }
            return 1 - 0.5 * (enclosure - 0.88) / 0.12;
        }

        // Ideal terrain has enough rock to shelter in and to mine, but plenty of space to farm.
        private static double RockShareFactor(double rockPercent)
        {
            if (rockPercent < 8)
            {
                return rockPercent / 8;
            }
            if (rockPercent <= 45)
            {
                return 1;
            }
            return Math.Max(0.2, 1 - (rockPercent - 45) / 40);
        }

        private static string CompassName(double ray)
        {
            double degrees = ray * 360.0 / RayCount;
            int octant = (int)Math.Round(degrees / 45.0);
            return CompassNames[((octant % 8) + 8) % 8];
        }

        private static double CountRockPercent(bool[,] rock)
        {
            int count = 0;
            foreach (bool cell in rock)
            {
                if (cell)
                {
                    count++;
                }
            }
            return Math.Round(100.0 * count / rock.Length, 1);
        }
    }
}
