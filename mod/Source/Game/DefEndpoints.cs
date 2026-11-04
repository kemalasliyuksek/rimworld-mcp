using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>
    /// Game knowledge, read from the live def database. Unlike the XML on disk, this includes every
    /// active mod with all of its patches applied, so it matches what the player's game really does.
    /// </summary>
    internal static class DefEndpoints
    {
        private const int MaxDescription = 200;

        private const int MaxDepth = 4;

        private const int MaxNodes = 3000;

        private const int MaxListItems = 100;

        private static List<Def> allDefs;

        private static IEnumerable<Def> AllDefs()
        {
            if (allDefs == null)
            {
                allDefs = GenDefDatabase.AllDefTypesWithDatabases()
                    .SelectMany(GenDefDatabase.GetAllDefsInDatabaseForDef)
                    .ToList();
            }
            return allDefs;
        }

        public static void Search(QueryParams query, JsonWriter json)
        {
            string text = query.RequireString("q");
            string type = query.GetString("type");
            int limit = query.GetInt("limit", 25, 1, 100);

            List<(Def Def, int Rank)> matches = AllDefs()
                .Where(d => type == null || string.Equals(d.GetType().Name, type, StringComparison.OrdinalIgnoreCase))
                .Select(d => (Def: d, Rank: MatchRank(d, text)))
                .Where(m => m.Rank > 0)
                .OrderByDescending(m => m.Rank)
                .ThenBy(m => m.Def.defName, StringComparer.Ordinal)
                .ToList();

            json.BeginObject();
            json.Prop("total", matches.Count);
            json.Key("defs").BeginArray();
            foreach ((Def def, int _) in matches.Take(limit))
            {
                json.BeginObject();
                json.Prop("type", def.GetType().Name);
                json.Prop("def", def.defName);
                json.Prop("label", def.label);
                json.Prop("mod", def.modContentPack?.Name);
                json.Prop("description", GameAccess.Truncate(GameAccess.Clean(def.description), MaxDescription));
                json.EndObject();
            }
            json.EndArray();
            json.EndObject();
        }

        private static int MatchRank(Def def, string text)
        {
            const StringComparison Ignore = StringComparison.OrdinalIgnoreCase;
            if (string.Equals(def.defName, text, Ignore) || string.Equals(def.label, text, Ignore))
            {
                return 4;
            }
            if ((def.defName?.StartsWith(text, Ignore) ?? false) || (def.label?.StartsWith(text, Ignore) ?? false))
            {
                return 3;
            }
            if ((def.defName?.IndexOf(text, Ignore) ?? -1) >= 0 || (def.label?.IndexOf(text, Ignore) ?? -1) >= 0)
            {
                return 2;
            }
            return (def.description?.IndexOf(text, Ignore) ?? -1) >= 0 ? 1 : 0;
        }

        public static void Get(QueryParams query, JsonWriter json)
        {
            string name = query.RequireString("def");
            string type = query.GetString("type");

            List<Def> matches = AllDefs()
                .Where(d => d.defName == name && (type == null || string.Equals(d.GetType().Name, type, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (matches.Count == 0)
            {
                throw new NotFoundException($"No def named '{name}'. Find names with search_defs.");
            }
            if (matches.Count > 1)
            {
                throw new BadRequestException($"Several defs are named '{name}'. Pass a type: {string.Join(", ", matches.Select(d => d.GetType().Name))}.");
            }

            var writer = new DefDumper(json);
            json.BeginObject();
            json.Prop("type", matches[0].GetType().Name);
            json.Prop("mod", matches[0].modContentPack?.Name);
            json.Key("fields");
            writer.WriteObjectFields(matches[0], 0);
            json.Prop("truncated", writer.Truncated);
            json.EndObject();
        }

        /// <summary>
        /// Dumps public fields only. Properties are skipped on purpose because their getters can
        /// run arbitrary mod code, and fields are all a def's data lives in.
        /// </summary>
        private sealed class DefDumper
        {
            private readonly JsonWriter json;
            private readonly HashSet<object> visited = new HashSet<object>();
            private int nodes;

            public bool Truncated { get; private set; }

            public DefDumper(JsonWriter json)
            {
                this.json = json;
            }

            public void WriteObjectFields(object target, int depth)
            {
                visited.Add(target);
                json.BeginObject();
                foreach (FieldInfo field in target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!IsInteresting(field))
                    {
                        continue;
                    }
                    object value;
                    try
                    {
                        value = field.GetValue(target);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    if (IsEmpty(value))
                    {
                        continue;
                    }
                    json.Key(field.Name);
                    WriteValue(value, depth + 1);
                }
                json.EndObject();
            }

            private static bool IsInteresting(FieldInfo field)
            {
                if (field.IsDefined(typeof(UnsavedAttribute), true))
                {
                    return false;
                }
                string ns = field.FieldType.Namespace ?? "";
                return !ns.StartsWith("UnityEngine", StringComparison.Ordinal)
                    && !typeof(Delegate).IsAssignableFrom(field.FieldType)
                    && !typeof(Graphic).IsAssignableFrom(field.FieldType)
                    && !typeof(GraphicData).IsAssignableFrom(field.FieldType)
                    && field.FieldType != typeof(SoundDef);
            }

            private static bool IsEmpty(object value)
            {
                switch (value)
                {
                    case null:
                        return true;
                    case string text:
                        return text.Length == 0;
                    case ICollection collection:
                        return collection.Count == 0;
                    default:
                        return false;
                }
            }

            private void WriteValue(object value, int depth)
            {
                if (++nodes > MaxNodes)
                {
                    Truncated = true;
                    json.Null();
                    return;
                }

                switch (value)
                {
                    case null:
                        json.Null();
                        return;
                    case string text:
                        json.Value(GameAccess.Clean(text));
                        return;
                    case bool flag:
                        json.Value(flag);
                        return;
                    case int number:
                        json.Value(number);
                        return;
                    case float number:
                        json.Value((double)number);
                        return;
                    case double number:
                        json.Value(number);
                        return;
                    case Enum _:
                    case IntVec2 _:
                    case IntVec3 _:
                    case FloatRange _:
                    case IntRange _:
                        json.Value(value.ToString());
                        return;
                    case Def def:
                        json.Value(def.defName);
                        return;
                    case Type type:
                        json.Value(type.Name);
                        return;
                    case IDictionary _:
                        json.Value("(dictionary)");
                        return;
                    case IEnumerable list:
                        WriteList(list, depth);
                        return;
                }

                Type valueType = value.GetType();
                if (valueType.IsPrimitive)
                {
                    json.Value(Convert.ToDouble(value));
                }
                else if (depth >= MaxDepth || !visited.Add(value) && !valueType.IsValueType)
                {
                    Truncated |= depth >= MaxDepth;
                    json.Value("(" + valueType.Name + ")");
                }
                else
                {
                    WriteObjectFields(value, depth);
                }
            }

            private void WriteList(IEnumerable list, int depth)
            {
                json.BeginArray();
                int count = 0;
                foreach (object item in list)
                {
                    if (++count > MaxListItems)
                    {
                        Truncated = true;
                        break;
                    }
                    WriteValue(item, depth);
                }
                json.EndArray();
            }
        }
    }
}
