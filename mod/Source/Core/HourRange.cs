using System.Collections.Generic;

namespace RimWorldMcp.Bridge.Core
{
    /// <summary>Reads a stretch of the day such as 22-6, which runs from 22:00 up to but not including 06:00.</summary>
    public static class HourRange
    {
        private const int HoursPerDay = 24;

        public static List<int> Parse(string text)
        {
            string[] parts = text.Split('-');
            if (parts.Length != 2
                || !int.TryParse(parts[0].Trim(), out int start)
                || !int.TryParse(parts[1].Trim(), out int end)
                || start < 0 || start >= HoursPerDay
                || end < 0 || end > HoursPerDay
                || start == end)
            {
                throw new BadRequestException($"'{text}' is not a range of hours. Write it as start-end, from 0 to 24, such as 22-6 for 22:00 to 06:00.");
            }

            var hours = new List<int>();
            for (int hour = start; hour != end % HoursPerDay || hours.Count == 0; hour = (hour + 1) % HoursPerDay)
            {
                hours.Add(hour);
            }
            return hours;
        }
    }
}
