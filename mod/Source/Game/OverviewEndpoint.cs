using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    internal static class OverviewEndpoint
    {
        private const int MaxUnreadLetters = 15;

        private const int MaxExplanationLength = 600;

        // AlertsReadout keeps this private and offers no accessor. Reflection avoids a Harmony
        // dependency for a single read.
        private static readonly FieldInfo ActiveAlertsField =
            typeof(AlertsReadout).GetField("activeAlerts", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Overview(QueryParams query, JsonWriter json)
        {
            Map map = GameAccess.ResolveMap(query);

            json.BeginObject();
            WriteTime(json, map);
            WriteMap(json, map);
            WriteColony(json, map);
            WriteFood(json, map);
            WriteThreatSummary(json, map);
            WriteResearch(json);

            json.Key("alerts").BeginArray();
            foreach (Alert alert in ActiveAlerts())
            {
                json.BeginObject();
                json.Prop("label", SafeLabel(alert));
                json.Prop("priority", alert.Priority.ToString());
                json.EndObject();
            }
            json.EndArray();

            WriteUnreadLetters(json);
            json.Prop("active_quests", Find.QuestManager.QuestsListForReading.Count(q => q.State == QuestState.Ongoing && !q.hidden && !q.hiddenInUI));
            json.EndObject();
        }

        public static void Alerts(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            json.BeginObject();
            json.Key("alerts").BeginArray();
            foreach (Alert alert in ActiveAlerts())
            {
                json.BeginObject();
                json.Prop("label", SafeLabel(alert));
                json.Prop("priority", alert.Priority.ToString());
                json.Prop("explanation", SafeExplanation(alert));
                json.EndObject();
            }
            json.EndArray();
            json.EndObject();
        }

        private static IEnumerable<Alert> ActiveAlerts()
        {
            AlertsReadout readout = Find.Alerts;
            if (readout == null || ActiveAlertsField == null)
            {
                return Enumerable.Empty<Alert>();
            }
            return ((List<Alert>)ActiveAlertsField.GetValue(readout)).OrderByDescending(a => a.Priority).ToList();
        }

        // Alert text comes from many mods. One broken alert must not take down the whole response.
        private static string SafeLabel(Alert alert)
        {
            try
            {
                return GameAccess.Clean(alert.GetLabel());
            }
            catch (Exception)
            {
                return alert.GetType().Name;
            }
        }

        private static string SafeExplanation(Alert alert)
        {
            try
            {
                return GameAccess.Truncate(GameAccess.Clean(alert.GetExplanation()), MaxExplanationLength);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void WriteTime(JsonWriter json, Map map)
        {
            json.Key("time").BeginObject();
            json.Prop("date", GameAccess.LocalDate(map));
            json.Prop("season", GenLocalDate.Season(map).ToString());
            json.Prop("ticks_game", Find.TickManager.TicksGame);
            json.Prop("days_passed", GenDate.DaysPassed);
            json.Prop("paused", Find.TickManager.Paused);
            json.Prop("speed", Find.TickManager.CurTimeSpeed.ToString());
            json.Prop("storyteller", Find.Storyteller.def.label);
            json.Prop("difficulty", Find.Storyteller.difficultyDef?.label);
            json.EndObject();
        }

        private static void WriteMap(JsonWriter json, Map map)
        {
            json.Key("map").BeginObject();
            json.Prop("id", map.uniqueID);
            json.Prop("biome", map.Biome.label);
            json.Prop("size_x", map.Size.x);
            json.Prop("size_z", map.Size.z);
            json.Prop("outdoor_temperature_c", map.mapTemperature.OutdoorTemp);
            json.Prop("weather", map.weatherManager.curWeather?.label);
            json.Prop("danger", map.dangerWatcher.DangerRating.ToString());
            json.Prop("wealth", map.wealthWatcher.WealthTotal);
            json.Key("conditions").BeginArray();
            // Map conditions alone would miss those on the whole world, such as an aurora.
            var conditions = new List<GameCondition>();
            map.gameConditionManager.GetAllGameConditionsAffectingMap(map, conditions);
            foreach (GameCondition condition in conditions)
            {
                json.BeginObject();
                json.Prop("label", GameAccess.Clean(condition.LabelCap));
                if (!condition.Permanent)
                {
                    json.Prop("days_left", condition.TicksLeft / (double)GameAccess.TicksPerDay);
                }
                json.EndObject();
            }
            json.EndArray();
            json.Key("other_map_ids").BeginArray();
            foreach (Map other in Find.Maps.Where(m => m != map))
            {
                json.Value(other.uniqueID);
            }
            json.EndArray();
            json.EndObject();
        }

        private static void WriteColony(JsonWriter json, Map map)
        {
            List<Pawn> colonists = GameAccess.AllColonists();
            List<Pawn> withMood = colonists.Where(p => p.needs?.mood != null).ToList();

            json.Key("colony").BeginObject();
            json.Prop("name", Faction.OfPlayer.Name);
            json.Prop("colonists", colonists.Count);
            json.Prop("colonists_on_this_map", map.mapPawns.FreeColonistsSpawnedCount);
            json.Prop("prisoners", map.mapPawns.PrisonersOfColonySpawned.Count);
            json.Prop("slaves", map.mapPawns.SlavesOfColonySpawned.Count);
            json.Prop("animals", map.mapPawns.SpawnedColonyAnimals.Count);
            if (withMood.Count > 0)
            {
                Pawn lowest = withMood.MinBy(p => p.needs.mood.CurLevelPercentage);
                json.Prop("average_mood_pct", GameAccess.Percent(withMood.Average(p => p.needs.mood.CurLevelPercentage)));
                json.Prop("lowest_mood_pct", GameAccess.Percent(lowest.needs.mood.CurLevelPercentage));
                json.Prop("lowest_mood_pawn", lowest.LabelShort);
            }
            WriteNames(json, "downed", colonists.Where(p => p.Downed));
            WriteNames(json, "in_mental_state", colonists.Where(p => p.InMentalState));
            WriteNames(json, "drafted", colonists.Where(p => p.Drafted));
            WriteNames(json, "needs_medical_care", colonists.Where(p => p.health.HasHediffsNeedingTend()));
            WriteNames(json, "idle", colonists.Where(p => p.Spawned && p.mindState != null && p.mindState.IsIdle));
            json.EndObject();
        }

        private static void WriteNames(JsonWriter json, string key, IEnumerable<Pawn> pawns)
        {
            json.Key(key).BeginArray();
            foreach (Pawn pawn in pawns)
            {
                json.Value(pawn.LabelShort);
            }
            json.EndArray();
        }

        private static float NutritionOf(IEnumerable<Thing> items)
        {
            return items.Sum(t => t.def.GetStatValueAbstract(StatDefOf.Nutrition) * t.stackCount);
        }

        private static void WriteFood(JsonWriter json, Map map)
        {
            float stored = map.resourceCounter.TotalHumanEdibleNutrition;
            // The same conditions as the game's resource counter, apart from being in storage.
            List<Thing> edible = GameAccess.VisibleUnstoredItems(map)
                .Where(t => t.def.CountAsResource && t.def.IsNutritionGivingIngestible && t.def.ingestible.HumanEdible && !t.IsNotFresh())
                .ToList();
            float forbidden = NutritionOf(edible.Where(t => t.IsForbidden(Faction.OfPlayer)));
            float onGround = NutritionOf(edible) - forbidden;
            float perDay = map.mapPawns.FreeColonistsAndPrisonersSpawned
                .Where(p => p.needs?.food != null)
                .Sum(p => p.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed, ignoreMalnutrition: true) * GameAccess.TicksPerDay);

            json.Key("food").BeginObject();
            json.Prop("stored_nutrition", stored);
            json.Prop("on_ground_nutrition", onGround);
            // Nobody eats forbidden food until the player allows it, which a new colony's supplies need.
            json.Prop("forbidden_nutrition", forbidden);
            json.Prop("human_nutrition_per_day", perDay);
            if (perDay > 0f)
            {
                json.Prop("days_of_food", (stored + onGround) / perDay);
            }
            json.EndObject();
        }

        private static void WriteThreatSummary(JsonWriter json, Map map)
        {
            List<Pawn> hostiles = ThreatEndpoint.VisibleHostiles(map);
            json.Key("threats").BeginObject();
            json.Prop("visible_hostile_pawns", hostiles.Count);
            json.Prop("active", hostiles.Count(ThreatEndpoint.IsActive));
            json.EndObject();
        }

        private static void WriteResearch(JsonWriter json)
        {
            ResearchProjectDef project = Find.ResearchManager.GetProject();
            json.Key("research").BeginObject();
            json.Prop("current", project?.LabelCap.ToString());
            if (project != null)
            {
                json.Prop("progress_pct", GameAccess.Percent(project.ProgressPercent));
            }
            json.EndObject();
        }

        private static void WriteUnreadLetters(JsonWriter json)
        {
            // The letter stack is the column on the right side of the screen: letters the player
            // has not dismissed yet, some of which wait for a decision.
            List<Letter> letters = Find.LetterStack.LettersListForReading;
            json.Key("letters").BeginArray();
            for (int i = letters.Count - 1; i >= Math.Max(0, letters.Count - MaxUnreadLetters); i--)
            {
                Letter letter = letters[i];
                json.BeginObject();
                json.Prop("label", GameAccess.Clean(letter.Label));
                json.Prop("type", letter.def?.defName);
                json.Prop("days_ago", (Find.TickManager.TicksGame - letter.arrivalTick) / (double)GameAccess.TicksPerDay);
                json.EndObject();
            }
            json.EndArray();
        }
    }
}
