using System.Collections.Generic;

namespace RimWorldMcp.Bridge.Core
{
    /// <summary>
    /// Reads map cells written as parts separated by semicolons: a cell (10,20), a filled rectangle
    /// (10,20-15,25), or the edge of one (outline:10,20-15,25), which is how a room's walls are drawn.
    /// </summary>
    public static class CellSpec
    {
        public const int MaxCells = 5000;

        private const string OutlinePrefix = "outline:";

        public static List<(int X, int Z)> Parse(string text)
        {
            var cells = new List<(int X, int Z)>();
            var seen = new HashSet<(int X, int Z)>();
            foreach (string rawPart in text.Split(';'))
            {
                string part = rawPart.Trim();
                if (part.Length == 0)
                {
                    continue;
                }

                bool outline = part.StartsWith(OutlinePrefix, System.StringComparison.OrdinalIgnoreCase);
                string range = outline ? part.Substring(OutlinePrefix.Length) : part;
                string[] corners = range.Split('-');
                if (corners.Length > 2 || (outline && corners.Length != 2))
                {
                    throw Invalid(part);
                }

                (int X, int Z) first = ParseCell(corners[0], part);
                (int X, int Z) second = corners.Length == 2 ? ParseCell(corners[1], part) : first;
                int minX = System.Math.Min(first.X, second.X);
                int maxX = System.Math.Max(first.X, second.X);
                int minZ = System.Math.Min(first.Z, second.Z);
                int maxZ = System.Math.Max(first.Z, second.Z);

                // Checked before the loops, because a typo here could otherwise mean billions of cells. These
                // are longs from the first step, since coordinates near the int limit overflow any int sum.
                long width = (long)maxX - minX + 1;
                long height = (long)maxZ - minZ + 1;
                long size = outline && width > 1 && height > 1 ? 2 * (width + height) - 4 : width * height;
                if (size + cells.Count > MaxCells)
                {
                    throw new BadRequestException($"That is more than {MaxCells} cells at once.");
                }

                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        if ((!outline || x == minX || x == maxX || z == minZ || z == maxZ) && seen.Add((x, z)))
                        {
                            cells.Add((x, z));
                        }
                    }
                }
            }

            if (cells.Count == 0)
            {
                throw new BadRequestException("Pass at least one cell.");
            }
            return cells;
        }

        private static (int X, int Z) ParseCell(string text, string part)
        {
            string[] numbers = text.Split(',');
            if (numbers.Length != 2
                || !int.TryParse(numbers[0].Trim(), out int x)
                || !int.TryParse(numbers[1].Trim(), out int z)
                || x < 0 || z < 0)
            {
                throw Invalid(part);
            }
            return (x, z);
        }

        private static BadRequestException Invalid(string part)
        {
            return new BadRequestException($"'{part}' is not a cell. Write 10,20 for one cell, 10,20-15,25 for a filled rectangle, or outline:10,20-15,25 for its edge, separated by semicolons.");
        }
    }
}
