using System;
using System.Collections.Generic;
using System.Globalization;

namespace RimWorldMcp.Bridge.Core
{
    /// <summary>Parsed URL query string with validating accessors.</summary>
    public sealed class QueryParams
    {
        private readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);

        public static QueryParams Parse(string query)
        {
            var result = new QueryParams();
            if (string.IsNullOrEmpty(query))
            {
                return result;
            }

            string trimmed = query[0] == '?' ? query.Substring(1) : query;
            foreach (string pair in trimmed.Split('&'))
            {
                if (pair.Length == 0)
                {
                    continue;
                }
                int separator = pair.IndexOf('=');
                string key = separator < 0 ? pair : pair.Substring(0, separator);
                string value = separator < 0 ? "" : pair.Substring(separator + 1);
                result.values[Decode(key)] = Decode(value);
            }
            return result;
        }

        public string GetString(string name, string fallback = null)
        {
            return values.TryGetValue(name, out string value) && value.Length > 0 ? value : fallback;
        }

        public string RequireString(string name)
        {
            return GetString(name) ?? throw new BadRequestException($"Missing required parameter '{name}'.");
        }

        public int GetInt(string name, int fallback, int min, int max)
        {
            string raw = GetString(name);
            if (raw == null)
            {
                return fallback;
            }
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                throw new BadRequestException($"Parameter '{name}' must be an integer.");
            }
            if (value < min || value > max)
            {
                throw new BadRequestException($"Parameter '{name}' must be between {min} and {max}.");
            }
            return value;
        }

        public int RequireInt(string name, int min, int max)
        {
            if (GetString(name) == null)
            {
                throw new BadRequestException($"Missing required parameter '{name}'.");
            }
            return GetInt(name, 0, min, max);
        }

        private static string Decode(string value)
        {
            try
            {
                return Uri.UnescapeDataString(value.Replace('+', ' '));
            }
            catch (UriFormatException)
            {
                throw new BadRequestException("Malformed query string.");
            }
        }
    }
}
