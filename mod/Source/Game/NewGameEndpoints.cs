using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>
    /// The first new game screens: scenario, storyteller and world settings. These keep their choices
    /// in the screen itself and only use them when the player presses Next, so setting them here shows
    /// up on screen and has no other effect.
    /// </summary>
    internal static class NewGameEndpoints
    {
        private const BindingFlags PrivateField = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private const int MaxDescription = 300;

        private const int MaxVisibleFactions = 12;

        private static object GetField(object page, string name)
        {
            return FieldOf(page, name).GetValue(page);
        }

        private static void SetField(object page, string name, object value)
        {
            FieldOf(page, name).SetValue(page, value);
        }

        private static FieldInfo FieldOf(object page, string name)
        {
            return page.GetType().GetField(name, PrivateField)
                ?? throw new InvalidOperationException($"{page.GetType().Name}.{name} no longer exists. New game setup needs updating for this version of the game.");
        }

        private static string CurrentScreen()
        {
            if (Find.WindowStack == null)
            {
                return null;
            }
            if (Find.WindowStack.IsOpen<Page_SelectScenario>())
            {
                return "scenario";
            }
            if (Find.WindowStack.IsOpen<Page_SelectStoryteller>())
            {
                return "storyteller";
            }
            return Find.WindowStack.IsOpen<Page_CreateWorldParams>() ? "world" : null;
        }

        public static void Setup(QueryParams query, JsonWriter json)
        {
            string screen = CurrentScreen();
            json.BeginObject();
            json.Prop("screen", screen);
            switch (screen)
            {
                case "scenario":
                    WriteScenarios(json, Find.WindowStack.WindowOfType<Page_SelectScenario>());
                    break;
                case "storyteller":
                    WriteStorytellers(json, Find.WindowStack.WindowOfType<Page_SelectStoryteller>());
                    break;
                case "world":
                    WriteWorld(json, Find.WindowStack.WindowOfType<Page_CreateWorldParams>());
                    break;
            }
            json.EndObject();
        }

        public static void Configure(QueryParams query, JsonWriter json)
        {
            switch (CurrentScreen())
            {
                case "scenario":
                    ConfigureScenario(query, Find.WindowStack.WindowOfType<Page_SelectScenario>());
                    break;
                case "storyteller":
                    ConfigureStoryteller(query, Find.WindowStack.WindowOfType<Page_SelectStoryteller>());
                    break;
                case "world":
                    ConfigureWorld(query, Find.WindowStack.WindowOfType<Page_CreateWorldParams>());
                    break;
                default:
                    throw new BridgeException(409, "not_on_new_game_screen", "The player is not on the scenario, storyteller or world settings screen.");
            }
            Setup(query, json);
        }

        private static void WriteScenarios(JsonWriter json, Page_SelectScenario page)
        {
            var current = GetField(page, "curScen") as Scenario;
            json.Prop("selected", current?.name);
            json.Key("scenarios").BeginArray();
            foreach (Scenario scenario in ScenarioLister.AllScenarios())
            {
                json.BeginObject();
                json.Prop("name", scenario.name);
                json.Prop("category", scenario.Category.ToString());
                json.Prop("summary", GameAccess.Truncate(GameAccess.Clean(scenario.summary), MaxDescription));
                json.EndObject();
            }
            json.EndArray();
        }

        private static void ConfigureScenario(QueryParams query, Page_SelectScenario page)
        {
            string name = query.RequireString("scenario");
            Scenario scenario = ScenarioLister.AllScenarios().FirstOrDefault(s => string.Equals(s.name, name, StringComparison.OrdinalIgnoreCase))
                ?? throw new NotFoundException($"No scenario named '{name}'.");
            SetField(page, "curScen", scenario);
        }

        private static void WriteStorytellers(JsonWriter json, Page_SelectStoryteller page)
        {
            var storyteller = GetField(page, "storyteller") as StorytellerDef;
            var difficulty = GetField(page, "difficulty") as DifficultyDef;
            var values = (Difficulty)GetField(page, "difficultyValues");

            json.Prop("selected_storyteller", storyteller?.defName);
            json.Prop("selected_difficulty", difficulty?.defName);
            GameInitData init = Find.GameInitData;
            json.Prop("selected_game_mode", init == null || !init.permadeathChosen ? null : init.permadeath ? "commitment" : "reload");
            json.Prop("ready_for_next", difficulty != null && init != null && init.permadeathChosen);
            json.Key("storytellers").BeginArray();
            foreach (StorytellerDef def in DefDatabase<StorytellerDef>.AllDefs.Where(d => d.listVisible).OrderBy(d => d.listOrder))
            {
                json.BeginObject();
                json.Prop("def", def.defName);
                json.Prop("label", def.label);
                json.Prop("description", GameAccess.Truncate(GameAccess.Clean(def.description), MaxDescription));
                json.EndObject();
            }
            json.EndArray();
            json.Key("difficulties").BeginArray();
            foreach (DifficultyDef def in DefDatabase<DifficultyDef>.AllDefs)
            {
                json.BeginObject();
                json.Prop("def", def.defName);
                json.Prop("label", def.label);
                json.Prop("custom", def.isCustom);
                json.Prop("description", GameAccess.Truncate(GameAccess.Clean(def.description), MaxDescription));
                json.EndObject();
            }
            json.EndArray();
            json.Key("difficulty_values").BeginObject();
            foreach (FieldInfo field in DifficultyFields())
            {
                object value = field.GetValue(values);
                if (value is bool flag)
                {
                    json.Prop(field.Name, flag);
                }
                else if (value is float number)
                {
                    json.Prop(field.Name, number);
                }
            }
            json.EndObject();
        }

        private static IEnumerable<FieldInfo> DifficultyFields()
        {
            return typeof(Difficulty).GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(f => f.FieldType == typeof(bool) || f.FieldType == typeof(float));
        }

        private static void ConfigureStoryteller(QueryParams query, Page_SelectStoryteller page)
        {
            string storytellerName = query.GetString("storyteller");
            StorytellerDef storyteller = storytellerName == null ? null : DefDatabase<StorytellerDef>.GetNamedSilentFail(storytellerName)
                ?? throw new NotFoundException($"No storyteller named '{storytellerName}'.");
            string difficultyName = query.GetString("difficulty");
            DifficultyDef difficulty = difficultyName == null ? null : DefDatabase<DifficultyDef>.GetNamedSilentFail(difficultyName)
                ?? throw new NotFoundException($"No difficulty named '{difficultyName}'.");

            // Everything is checked before anything changes.
            var overrides = new List<(FieldInfo Field, object Value)>();
            foreach ((string name, string value) in query.GetPairs("difficulty_values"))
            {
                FieldInfo field = DifficultyFields().FirstOrDefault(f => f.Name == name)
                    ?? throw new NotFoundException($"No difficulty value named '{name}'.");
                overrides.Add((field, ParseDifficultyValue(field, value)));
            }
            string gameMode = query.GetString("game_mode");
            if (gameMode != null && gameMode != "reload" && gameMode != "commitment")
            {
                throw new BadRequestException("Parameter 'game_mode' must be reload or commitment.");
            }
            if (storyteller == null && difficulty == null && overrides.Count == 0 && gameMode == null)
            {
                throw new BadRequestException("Pass 'storyteller', 'difficulty', 'difficulty_values' or 'game_mode'.");
            }
            if (gameMode != null)
            {
                Find.GameInitData.permadeathChosen = true;
                Find.GameInitData.permadeath = gameMode == "commitment";
            }

            if (storyteller != null)
            {
                SetField(page, "storyteller", storyteller);
            }
            var values = (Difficulty)GetField(page, "difficultyValues");
            if (difficulty != null)
            {
                SetField(page, "difficulty", difficulty);
                if (!difficulty.isCustom)
                {
                    values.CopyFrom(difficulty);
                }
            }
            if (overrides.Count > 0)
            {
                // The game only honors individual values under its custom difficulty.
                DifficultyDef custom = DefDatabase<DifficultyDef>.AllDefs.FirstOrDefault(d => d.isCustom);
                if (custom != null)
                {
                    SetField(page, "difficulty", custom);
                }
                foreach ((FieldInfo field, object value) in overrides)
                {
                    field.SetValue(values, value);
                }
            }
        }

        private static object ParseDifficultyValue(FieldInfo field, string value)
        {
            if (field.FieldType == typeof(bool))
            {
                if (bool.TryParse(value, out bool flag))
                {
                    return flag;
                }
                throw new BadRequestException($"'{field.Name}' must be true or false.");
            }
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
            {
                return number;
            }
            throw new BadRequestException($"'{field.Name}' must be a number.");
        }

        private static void WriteWorld(JsonWriter json, Page_CreateWorldParams page)
        {
            json.Prop("seed", (string)GetField(page, "seedString"));
            json.Prop("planet_coverage_pct", GameAccess.Percent((float)GetField(page, "planetCoverage")));
            json.Prop("rainfall", GetField(page, "rainfall").ToString());
            json.Prop("temperature", GetField(page, "temperature").ToString());
            json.Prop("population", GetField(page, "population").ToString());
            json.Prop("landmark_density", GetField(page, "landmarkDensity").ToString());
            json.Prop("pollution_pct", GameAccess.Percent(page.pollution));
            json.Key("factions").BeginArray();
            if (GetField(page, "factions") is List<FactionDef> factions)
            {
                foreach (FactionDef faction in factions)
                {
                    json.Value(faction.defName);
                }
            }
            json.EndArray();
            json.Key("faction_options").BeginArray();
            foreach (FactionDef faction in FactionGenerator.ConfigurableFactions.Where(f => f.displayInFactionSelection))
            {
                json.BeginObject();
                json.Prop("def", faction.defName);
                json.Prop("label", faction.label);
                json.Prop("max", faction.maxConfigurableAtWorldCreation);
                json.EndObject();
            }
            json.EndArray();
            WriteEnumNames<OverallRainfall>(json, "rainfall_options");
            WriteEnumNames<OverallTemperature>(json, "temperature_options");
            WriteEnumNames<OverallPopulation>(json, "population_options");
            WriteEnumNames<LandmarkDensity>(json, "landmark_density_options");
        }

        private static void WriteEnumNames<T>(JsonWriter json, string key) where T : struct, Enum
        {
            json.Key(key).BeginArray();
            foreach (string name in Enum.GetNames(typeof(T)))
            {
                json.Value(name);
            }
            json.EndArray();
        }

        private static void ConfigureWorld(QueryParams query, Page_CreateWorldParams page)
        {
            // Everything is checked before anything changes.
            OverallRainfall? rainfall = ParseEnum<OverallRainfall>(query, "rainfall");
            OverallTemperature? temperature = ParseEnum<OverallTemperature>(query, "temperature");
            OverallPopulation? population = ParseEnum<OverallPopulation>(query, "population");
            LandmarkDensity? landmarks = ParseEnum<LandmarkDensity>(query, "landmark_density");
            float? coverage = null;
            if (query.GetString("planet_coverage_pct") != null)
            {
                int percent = query.GetInt("planet_coverage_pct", 0, 1, 100);
                if (percent != 30 && percent != 50 && percent != 100)
                {
                    throw new BadRequestException("Parameter 'planet_coverage_pct' must be 30, 50 or 100.");
                }
                coverage = percent / 100f;
            }
            int? pollution = query.GetString("pollution_pct") == null ? (int?)null : query.GetInt("pollution_pct", 0, 0, 100);
            string seed = query.GetString("seed");
            List<FactionDef> chosenFactions = ParseFactions(query);
            if (pollution != null && !ModsConfig.BiotechActive)
            {
                throw new BadRequestException("World pollution needs the Biotech expansion.");
            }

            if (seed != null)
            {
                SetField(page, "seedString", seed);
            }
            if (chosenFactions != null)
            {
                // Factions hidden from this screen, such as the insects, must stay in the list.
                var current = (List<FactionDef>)GetField(page, "factions");
                current.RemoveAll(f => f.displayInFactionSelection);
                current.AddRange(chosenFactions);
            }
            if (coverage != null)
            {
                SetField(page, "planetCoverage", coverage.Value);
            }
            if (rainfall != null)
            {
                SetField(page, "rainfall", rainfall.Value);
            }
            if (temperature != null)
            {
                SetField(page, "temperature", temperature.Value);
            }
            if (population != null)
            {
                SetField(page, "population", population.Value);
            }
            if (landmarks != null)
            {
                SetField(page, "landmarkDensity", landmarks.Value);
            }
            if (pollution != null)
            {
                page.pollution = pollution.Value / 100f;
            }
        }

        private static List<FactionDef> ParseFactions(QueryParams query)
        {
            List<(string Name, string Value)> pairs = query.GetPairs("factions");
            if (pairs.Count == 0)
            {
                return null;
            }
            var factions = new List<FactionDef>();
            foreach ((string name, string value) in pairs)
            {
                FactionDef faction = FactionGenerator.ConfigurableFactions.FirstOrDefault(f => f.defName == name && f.displayInFactionSelection)
                    ?? throw new NotFoundException($"No faction named '{name}' can be chosen. See faction_options.");
                if (!int.TryParse(value, out int count) || count < 1 || count > faction.maxConfigurableAtWorldCreation)
                {
                    throw new BadRequestException($"The count for '{name}' must be between 1 and {faction.maxConfigurableAtWorldCreation}.");
                }
                factions.AddRange(Enumerable.Repeat(faction, count));
            }
            if (factions.Count > MaxVisibleFactions)
            {
                throw new BadRequestException($"A world can have at most {MaxVisibleFactions} factions.");
            }
            return factions;
        }

        private static T? ParseEnum<T>(QueryParams query, string name) where T : struct, Enum
        {
            string raw = query.GetString(name);
            if (raw == null)
            {
                return null;
            }
            if (Enum.TryParse(raw, ignoreCase: true, out T value) && Enum.IsDefined(typeof(T), value))
            {
                return value;
            }
            throw new BadRequestException($"Parameter '{name}' must be one of: {string.Join(", ", Enum.GetNames(typeof(T)))}.");
        }
    }
}
