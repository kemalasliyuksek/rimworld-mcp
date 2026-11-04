using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>
    /// The player's own options: RimWorld's settings, the mod list and each mod's settings. These change
    /// on request through the game's own config files. Code and files of the game and mods stay untouched.
    /// </summary>
    internal static class GameOptionsEndpoints
    {
        private const int MaxDescription = 180;

        private static readonly FieldInfo PrefsDataField = typeof(Prefs).GetField("data", BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly FieldInfo ModSettingsField = typeof(Mod).GetField("modSettings", BindingFlags.Instance | BindingFlags.NonPublic);

        private static IEnumerable<FieldInfo> SimpleFields(Type type)
        {
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public).Where(f => IsSimple(f.FieldType));
        }

        private static bool IsSimple(Type type)
        {
            return type == typeof(bool) || type == typeof(int) || type == typeof(float) || type == typeof(string) || type.IsEnum;
        }

        private static void WriteFields(JsonWriter json, object target)
        {
            json.Key("values").BeginObject();
            foreach (FieldInfo field in SimpleFields(target.GetType()))
            {
                object value = field.GetValue(target);
                switch (value)
                {
                    case null:
                        json.Key(field.Name).Null();
                        break;
                    case bool flag:
                        json.Prop(field.Name, flag);
                        break;
                    case int number:
                        json.Prop(field.Name, number);
                        break;
                    case float number:
                        json.Prop(field.Name, number);
                        break;
                    default:
                        json.Prop(field.Name, value.ToString());
                        break;
                }
            }
            json.EndObject();
        }

        // Everything is parsed before anything is set, so a mistake cannot leave half the changes applied.
        private static List<(FieldInfo Field, object Value)> ParseChanges(object target, QueryParams query)
        {
            List<(string Name, string Value)> pairs = query.GetPairs("values");
            if (pairs.Count == 0)
            {
                throw new BadRequestException("Pass 'values', such as runInBackground:true,volumeMusic:0.3.");
            }
            var changes = new List<(FieldInfo, object)>();
            foreach ((string name, string value) in pairs)
            {
                FieldInfo field = SimpleFields(target.GetType()).FirstOrDefault(f => f.Name == name)
                    ?? throw new NotFoundException($"No setting named '{name}'.");
                changes.Add((field, ParseValue(field, value)));
            }
            return changes;
        }

        private static object ParseValue(FieldInfo field, string value)
        {
            Type type = field.FieldType;
            if (type == typeof(string))
            {
                return value;
            }
            if (type == typeof(bool) && bool.TryParse(value, out bool flag))
            {
                return flag;
            }
            if (type == typeof(int) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int whole))
            {
                return whole;
            }
            if (type == typeof(float) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
            {
                return number;
            }
            if (type.IsEnum && Enum.GetNames(type).Contains(value))
            {
                return Enum.Parse(type, value);
            }
            throw new BadRequestException($"'{value}' is not a valid value for '{field.Name}' ({type.Name}).");
        }

        public static void GetSettings(QueryParams query, JsonWriter json)
        {
            json.BeginObject();
            WriteFields(json, PrefsObject());
            json.EndObject();
        }

        public static void SetSettings(QueryParams query, JsonWriter json)
        {
            object prefs = PrefsObject();
            foreach ((FieldInfo field, object value) in ParseChanges(prefs, query))
            {
                field.SetValue(prefs, value);
            }
            Prefs.Apply();
            Prefs.Save();

            json.BeginObject();
            json.Prop("note", "Saved. Some settings, such as the language, only take full effect after a restart.");
            WriteFields(json, prefs);
            json.EndObject();
        }

        private static object PrefsObject()
        {
            return PrefsDataField?.GetValue(null)
                ?? throw new InvalidOperationException("The game's settings could not be read. This version of the game may need an update of the mod.");
        }

        public static void InstalledMods(QueryParams query, JsonWriter json)
        {
            string search = query.GetString("q");
            IEnumerable<ModMetaData> mods = ModLister.AllInstalledMods;
            if (search != null)
            {
                mods = mods.Where(m => m.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                    || m.PackageIdPlayerFacing.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            json.BeginObject();
            json.Key("mods").BeginArray();
            foreach (ModMetaData mod in mods.OrderByDescending(m => m.Active).ThenBy(m => m.Name))
            {
                json.BeginObject();
                json.Prop("name", mod.Name);
                json.Prop("package_id", mod.PackageIdPlayerFacing);
                json.Prop("active", mod.Active);
                json.Prop("author", mod.AuthorsString);
                json.Prop("version", mod.ModVersion);
                json.Prop("source", mod.Source.ToString());
                json.Prop("compatible_with_this_game_version", mod.VersionCompatible);
                json.Prop("description", GameAccess.Truncate(GameAccess.Clean(mod.Description), MaxDescription));
                json.EndObject();
            }
            json.EndArray();
            WriteActiveOrder(json);
            json.EndObject();
        }

        private static void WriteActiveOrder(JsonWriter json)
        {
            json.Key("load_order").BeginArray();
            foreach (ModMetaData mod in ModsConfig.ActiveModsInLoadOrder)
            {
                json.Value(mod.PackageIdPlayerFacing);
            }
            json.EndArray();
            json.Key("warnings").BeginObject();
            foreach (KeyValuePair<string, string> warning in ModsConfig.GetModWarnings().Where(w => !w.Value.NullOrEmpty()))
            {
                json.Prop(warning.Key, GameAccess.Clean(warning.Value));
            }
            json.EndObject();
        }

        private static ModMetaData ResolveInstalled(string identifier)
        {
            return ModLister.AllInstalledMods.FirstOrDefault(m => string.Equals(m.PackageIdPlayerFacing, identifier, StringComparison.OrdinalIgnoreCase))
                ?? throw new NotFoundException($"No installed mod with the package id '{identifier}'.");
        }

        /// <summary>The mod list takes effect after a restart, so the running game is unaffected.</summary>
        public static void ConfigureMods(QueryParams query, JsonWriter json)
        {
            List<ModMetaData> enable = query.GetList("enable").Select(ResolveInstalled).ToList();
            List<ModMetaData> disable = query.GetList("disable").Select(ResolveInstalled).ToList();
            List<ModMetaData> order = query.GetList("order").Select(ResolveInstalled).ToList();
            bool sort = query.GetString("sort") == "true";
            if (enable.Count == 0 && disable.Count == 0 && order.Count == 0 && !sort)
            {
                throw new BadRequestException("Pass 'enable', 'disable', 'order' or 'sort'.");
            }
            if (disable.Any(m => m.IsCoreMod) || (order.Count > 0 && !order.Any(m => m.IsCoreMod)))
            {
                throw new BadRequestException("The Core mod must stay active.");
            }

            if (order.Count > 0)
            {
                ModsConfig.SetActiveToList(order.Select(m => m.PackageId).ToList());
            }
            foreach (ModMetaData mod in enable)
            {
                ModsConfig.SetActive(mod, active: true);
            }
            foreach (ModMetaData mod in disable)
            {
                ModsConfig.SetActive(mod, active: false);
            }
            if (sort)
            {
                ModsConfig.TrySortMods();
            }
            ModsConfig.Save();

            json.BeginObject();
            json.Prop("restart_needed", true);
            json.Prop("note", "Saved to the game's mod list. It applies after restarting the game. Changing mods can affect existing saves.");
            WriteActiveOrder(json);
            json.EndObject();
        }

        private static (Mod Handle, object Settings) ResolveModSettings(QueryParams query)
        {
            string identifier = query.RequireString("mod");
            Mod handle = LoadedModManager.ModHandles.FirstOrDefault(m => string.Equals(m.Content.PackageIdPlayerFacing, identifier, StringComparison.OrdinalIgnoreCase))
                ?? throw new NotFoundException($"No running mod with the package id '{identifier}' has a settings class.");
            object settings = ModSettingsField?.GetValue(handle)
                ?? throw new NotFoundException($"The mod '{identifier}' has no settings loaded.");
            return (handle, settings);
        }

        public static void GetModSettings(QueryParams query, JsonWriter json)
        {
            (Mod handle, object settings) = ResolveModSettings(query);
            json.BeginObject();
            json.Prop("mod", handle.Content.Name);
            json.Prop("note", "Only public, simple values are listed. A mod may have more settings than this.");
            WriteFields(json, settings);
            json.EndObject();
        }

        public static void SetModSettings(QueryParams query, JsonWriter json)
        {
            (Mod handle, object settings) = ResolveModSettings(query);
            foreach ((FieldInfo field, object value) in ParseChanges(settings, query))
            {
                field.SetValue(settings, value);
            }
            handle.WriteSettings();

            json.BeginObject();
            json.Prop("mod", handle.Content.Name);
            json.Prop("note", "Saved. Some mods only read their settings when the game starts.");
            WriteFields(json, settings);
            json.EndObject();
        }
    }
}
