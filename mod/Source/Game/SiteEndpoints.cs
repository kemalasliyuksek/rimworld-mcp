using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using RimWorldMcp.Bridge.Core;
using UnityEngine;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>
    /// Helps choose where to settle. These endpoints also work before a game starts, on the
    /// site selection screen, because all they need is the generated world.
    /// </summary>
    internal static class SiteEndpoints
    {
        private const int SearchCellsPerSample = 4;

        private const int PreviewCellsPerSample = 5;

        private const int DaysPerTwelfth = 5;

        // The game's own definition of the outdoor growing period, as shown on the terrain tab.
        private const float MinGrowingTemperature = 6f;
        private const float MaxGrowingTemperature = 42f;

        // Predicting a map costs real time on the main thread. This screen has nothing else to do,
        // but the request still has to finish before the HTTP timeout.
        private const long ShapeSearchBudgetMs = 6000;

        private static void RequireWorld()
        {
            if (Current.Game?.World == null)
            {
                throw new BridgeException(409, "no_world", "No world has been generated yet. The player needs to create a world or load a game first.");
            }
        }

        private static int MapSize()
        {
            return Find.GameInitData?.mapSize ?? Find.World.info.initialMapSize.x;
        }

        private static bool IsOnSiteSelection()
        {
            return Find.WindowStack != null && Find.WindowStack.IsOpen<Page_SelectStartingSite>();
        }

        public static void WorldSettings(QueryParams query, JsonWriter json)
        {
            RequireWorld();
            WorldInfo info = Find.World.info;
            List<SurfaceTile> land = Find.WorldGrid.Tiles.Where(t => !t.WaterCovered).ToList();

            json.BeginObject();
            json.Prop("world_name", info.name);
            json.Prop("seed", info.seedString);
            json.Prop("planet_coverage_pct", GameAccess.Percent(info.planetCoverage));
            json.Prop("overall_rainfall", info.overallRainfall.ToString());
            json.Prop("overall_temperature", info.overallTemperature.ToString());
            json.Prop("overall_population", info.overallPopulation.ToString());
            json.Prop("map_size", MapSize());
            json.Prop("land_tiles", land.Count);
            json.Prop("on_site_selection_screen", IsOnSiteSelection());
            PlanetTile selected = Find.WorldInterface.SelectedTile;
            if (selected.Valid)
            {
                json.Prop("selected_tile", selected.tileId);
            }
            WriteCounts(json, "biomes", land.Where(t => t.PrimaryBiome.canBuildBase).GroupBy(t => t.PrimaryBiome.defName));
            WriteCounts(json, "hilliness", land.GroupBy(t => t.hilliness.ToString()));
            WriteCounts(json, "tile_features", land.SelectMany(t => t.Mutators.Select(m => (Tile: t, Mutator: m))).GroupBy(p => p.Mutator.defName));
            json.EndObject();
        }

        private static void WriteCounts<T>(JsonWriter json, string key, IEnumerable<IGrouping<string, T>> groups)
        {
            json.Key(key).BeginObject();
            foreach (IGrouping<string, T> group in groups.OrderByDescending(g => g.Count()))
            {
                json.Prop(group.Key, group.Count());
            }
            json.EndObject();
        }

        public static void Search(QueryParams query, JsonWriter json)
        {
            RequireWorld();
            var filter = new SiteFilter(query);
            int limit = query.GetInt("limit", 8, 1, 20);
            bool rankByPocket = query.GetString("shape") == "crescent";
            if (!rankByPocket && query.GetString("shape") != null)
            {
                throw new BadRequestException("The only supported shape is 'crescent'.");
            }
            int minRock = query.GetInt("min_rock_pct", 0, 0, 100);
            int maxRock = query.GetInt("max_rock_pct", 100, 0, 100);

            List<SurfaceTile> matches = Find.WorldGrid.Tiles.Where(filter.Matches).ToList();

            json.BeginObject();
            json.Prop("matched", matches.Count);
            if (rankByPocket || minRock > 0 || maxRock < 100)
            {
                WritePredictedResults(json, matches, limit, rankByPocket, query.GetInt("min_pocket_score", 0, 0, 100), minRock, maxRock);
            }
            else
            {
                json.Prop("sorted_by", "growing days, then mildest climate");
                json.Key("sites").BeginArray();
                foreach (SurfaceTile tile in matches.OrderByDescending(GrowingDays).ThenBy(t => Math.Abs(t.temperature - 18f)).Take(limit))
                {
                    WriteSite(json, tile, null, detailed: false);
                }
                json.EndArray();
            }
            json.EndObject();
        }

        private static void WritePredictedResults(JsonWriter json, List<SurfaceTile> matches, int limit, bool rankByPocket, int minScore, int minRock, int maxRock)
        {
            int mapSize = MapSize();
            var evaluated = new List<(SurfaceTile Tile, MountainPredictor.Prediction Prediction, PocketResult Pocket)>();
            var stopwatch = Stopwatch.StartNew();
            bool budgetExhausted = false;

            // A shuffle spreads the time budget across the whole planet. It uses its own generator
            // so that the game's random numbers stay untouched.
            var shuffler = new System.Random(Find.World.info.Seed);
            foreach (SurfaceTile tile in matches.OrderBy(_ => shuffler.Next()))
            {
                if (stopwatch.ElapsedMilliseconds > ShapeSearchBudgetMs)
                {
                    budgetExhausted = true;
                    break;
                }
                MountainPredictor.Prediction prediction = MountainPredictor.Predict(tile, mapSize, SearchCellsPerSample);
                evaluated.Add((tile, prediction, PocketAnalyzer.Analyze(prediction.Rock, SearchCellsPerSample)));
            }

            json.Prop("evaluated", evaluated.Count);
            json.Prop("search_ms", stopwatch.ElapsedMilliseconds);
            json.Prop("stopped_at_time_budget", budgetExhausted);
            json.Prop("sorted_by", rankByPocket ? "pocket score" : "growing days");

            var kept = evaluated.Where(e => e.Pocket.Score >= minScore && e.Pocket.RockPercent >= minRock && e.Pocket.RockPercent <= maxRock);
            var ranked = rankByPocket
                ? kept.OrderByDescending(e => e.Pocket.Score)
                : kept.OrderByDescending(e => GrowingDays(e.Tile));

            json.Key("sites").BeginArray();
            foreach (var entry in ranked.Take(limit))
            {
                WriteSite(json, entry.Tile, entry, detailed: false);
            }
            json.EndArray();
        }

        public static void Get(QueryParams query, JsonWriter json)
        {
            RequireWorld();
            SurfaceTile tile = ResolveTile(query);
            int mapSize = MapSize();
            MountainPredictor.Prediction prediction = MountainPredictor.Predict(tile, mapSize, PreviewCellsPerSample);
            PocketResult pocket = PocketAnalyzer.Analyze(prediction.Rock, PreviewCellsPerSample);

            json.BeginObject();
            WriteSiteFields(json, tile, (tile, prediction, pocket), detailed: true);
            WritePreview(json, prediction.Rock, pocket);
            json.EndObject();
        }

        private static SurfaceTile ResolveTile(QueryParams query)
        {
            int count = Find.WorldGrid.TilesCount;
            int id;
            if (query.GetString("tile") != null)
            {
                id = query.GetInt("tile", 0, 0, count - 1);
            }
            else
            {
                PlanetTile selected = Find.WorldInterface.SelectedTile;
                if (!selected.Valid)
                {
                    throw new BadRequestException("No tile is selected in the game. Pass a tile id.");
                }
                id = selected.tileId;
            }
            return Find.WorldGrid[id];
        }

        /// <summary>
        /// Moves the camera to a tile and selects it. This is the bridge's one exception to being
        /// read-only: it changes only the camera and selection, on a screen where no game is running
        /// yet, and the player still confirms the choice themselves.
        /// </summary>
        public static void Show(QueryParams query, JsonWriter json)
        {
            RequireWorld();
            if (!IsOnSiteSelection())
            {
                throw new BridgeException(409, "not_selecting_site", "The player is not on the site selection screen.");
            }
            SurfaceTile tile = Find.WorldGrid[query.RequireInt("tile", 0, Find.WorldGrid.TilesCount - 1)];
            if (!TileFinder.IsValidTileForNewSettlement(tile.tile))
            {
                throw new BadRequestException("Nobody can settle on that tile.");
            }

            Find.WorldSelector.ClearSelection();
            Find.WorldInterface.SelectedTile = tile.tile;
            Find.WorldCameraDriver.JumpTo(tile.tile);

            json.BeginObject();
            json.Prop("tile", tile.tile.tileId);
            json.Prop("biome", tile.PrimaryBiome.label);
            json.Prop("shown", true);
            json.EndObject();
        }

        private static int GrowingDays(SurfaceTile tile)
        {
            return GenTemperature.TwelfthsInAverageTemperatureRange(tile.tile, MinGrowingTemperature, MaxGrowingTemperature).Count * DaysPerTwelfth;
        }

        private static void WriteSite(JsonWriter json, SurfaceTile tile, (SurfaceTile, MountainPredictor.Prediction, PocketResult)? pocket, bool detailed)
        {
            json.BeginObject();
            WriteSiteFields(json, tile, pocket, detailed);
            json.EndObject();
        }

        private static void WriteSiteFields(JsonWriter json, SurfaceTile tile, (SurfaceTile, MountainPredictor.Prediction Prediction, PocketResult Pocket)? pocket, bool detailed)
        {
            PlanetTile id = tile.tile;
            Vector2 longLat = Find.WorldGrid.LongLatOf(id);

            json.Prop("tile", id.tileId);
            json.Prop("latitude", longLat.y);
            json.Prop("longitude", longLat.x);
            json.Prop("biome", tile.PrimaryBiome.label);
            json.Prop("hilliness", tile.hilliness.ToString());
            json.Prop("average_temperature_c", tile.temperature);
            json.Prop("coldest_c", tile.MinTemperature);
            json.Prop("hottest_c", tile.MaxTemperature);
            json.Prop("rainfall_mm", tile.rainfall);
            json.Prop("growing_days_per_60", GrowingDays(tile));
            json.Prop("diseases_per_year", 60f / tile.PrimaryBiome.diseaseMtbDays);

            WriteLabels(json, "features", tile.Mutators.OrderBy(m => -m.displayPriority).Select(m => GameAccess.Clean(m.Label(id))));
            WriteLabels(json, "stones", Find.World.NaturalRockTypesIn(id).Select(r => r.label));

            // Cave tunnels are where the game places insect hives when a map is generated.
            if (Find.World.HasCaves(id))
            {
                json.Prop("has_caves", true);
            }
            if (tile.Mutators.Any(m => m.defName == "InsectMegahive"))
            {
                json.Prop("insect_megahive", true);
            }
            if (tile.IsCoastal)
            {
                json.Prop("coastal", true);
            }
            if (Find.World.LakeDirectionAt(id) != Rot4.Invalid)
            {
                json.Prop("lakeside", true);
            }
            if (tile.Rivers != null)
            {
                json.Prop("river", tile.Rivers.MaxBy(r => r.river.degradeThreshold).river.label);
            }
            if (tile.Roads != null)
            {
                json.Prop("road", tile.Roads.MaxBy(r => r.road.priority).road.label);
            }
            if (tile.Landmark != null)
            {
                json.Prop("landmark", tile.Landmark.name);
            }

            if (detailed)
            {
                json.Prop("elevation_m", tile.elevation);
                json.Prop("swampiness", tile.swampiness);
                json.Prop("pollution_pct", GameAccess.Percent(tile.pollution));
                json.Prop("forageability_pct", GameAccess.Percent(tile.PrimaryBiome.forageability));
                json.Prop("animal_density", tile.AnimalDensity);
                json.Prop("region", tile.feature?.name);
                json.Prop("can_settle", TileFinder.IsValidTileForNewSettlement(id));
            }

            if (pocket != null)
            {
                WritePocket(json, pocket.Value.Prediction, pocket.Value.Pocket);
            }
        }

        private static void WriteLabels(JsonWriter json, string key, IEnumerable<string> labels)
        {
            json.Key(key).BeginArray();
            foreach (string label in labels)
            {
                json.Value(label);
            }
            json.EndArray();
        }

        private static void WritePocket(JsonWriter json, MountainPredictor.Prediction prediction, PocketResult pocket)
        {
            json.Key("pocket").BeginObject();
            json.Prop("score", pocket.Score);
            json.Prop("rock_pct", pocket.RockPercent);
            if (pocket.Score > 0)
            {
                json.Prop("center_x", pocket.CenterX);
                json.Prop("center_z", pocket.CenterZ);
                json.Prop("enclosure_pct", pocket.EnclosurePercent);
                json.Prop("clear_radius_cells", pocket.ClearRadius);
                json.Prop("openings", pocket.Openings);
                json.Prop("opening_direction", pocket.OpeningDirection);
                json.Prop("opening_degrees", pocket.OpeningDegrees);
            }
            json.Prop("prediction_exact", prediction.IsExact);
            if (!prediction.IsExact)
            {
                WriteLabels(json, "not_modeled", prediction.Unmodeled);
            }
            json.EndObject();
        }

        private static void WritePreview(JsonWriter json, bool[,] rock, PocketResult pocket)
        {
            int samples = rock.GetLength(0);
            int centerX = pocket.Score > 0 ? pocket.CenterX / PreviewCellsPerSample : -1;
            int centerZ = pocket.Score > 0 ? pocket.CenterZ / PreviewCellsPerSample : -1;

            json.Prop("preview_note", $"Predicted rock. # is rock, . is open ground, X is the pocket centre. The first row is north, and each character is {PreviewCellsPerSample} cells. Water, caves, ores and ruins are not shown.");
            json.Key("preview").BeginArray();
            var row = new StringBuilder(samples);
            for (int z = samples - 1; z >= 0; z--)
            {
                row.Clear();
                for (int x = 0; x < samples; x++)
                {
                    row.Append(x == centerX && z == centerZ ? 'X' : rock[x, z] ? '#' : '.');
                }
                json.Value(row.ToString());
            }
            json.EndArray();
        }

        /// <summary>Parses the search parameters, and checks the cheapest conditions first.</summary>
        private sealed class SiteFilter
        {
            private enum Presence
            {
                Any,
                Require,
                Exclude,
            }

            private readonly HashSet<string> biomes;
            private readonly HashSet<string> hilliness;
            private readonly HashSet<string> excludedFeatures;
            private readonly HashSet<string> requiredFeatures;
            private readonly float minRainfall;
            private readonly float maxRainfall;
            private readonly float minColdest;
            private readonly float maxHottest;
            private readonly float maxDiseases;
            private readonly int minGrowingDays;
            private readonly Presence caves;
            private readonly Presence coast;
            private readonly Presence lake;
            private readonly Presence river;
            private readonly Presence road;

            public SiteFilter(QueryParams query)
            {
                biomes = ReadSet(query, "biomes");
                hilliness = ReadSet(query, "hilliness");
                excludedFeatures = ReadSet(query, "exclude_features");
                requiredFeatures = ReadSet(query, "require_features");
                minRainfall = query.GetInt("min_rainfall", 0, 0, int.MaxValue);
                maxRainfall = query.GetInt("max_rainfall", int.MaxValue, 0, int.MaxValue);
                minColdest = query.GetInt("min_coldest_c", -1000, -1000, 1000);
                maxHottest = query.GetInt("max_hottest_c", 1000, -1000, 1000);
                maxDiseases = query.GetInt("max_diseases_per_year", int.MaxValue, 0, int.MaxValue);
                minGrowingDays = query.GetInt("min_growing_days", 0, 0, 60);
                caves = ReadPresence(query, "caves");
                coast = ReadPresence(query, "coast");
                lake = ReadPresence(query, "lake");
                river = ReadPresence(query, "river");
                road = ReadPresence(query, "road");
            }

            private static HashSet<string> ReadSet(QueryParams query, string name)
            {
                string raw = query.GetString(name);
                return raw == null
                    ? null
                    : new HashSet<string>(raw.Split(',').Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
            }

            private static Presence ReadPresence(QueryParams query, string name)
            {
                switch (query.GetString(name, "any"))
                {
                    case "any":
                        return Presence.Any;
                    case "require":
                        return Presence.Require;
                    case "exclude":
                        return Presence.Exclude;
                    default:
                        throw new BadRequestException($"Parameter '{name}' must be any, require or exclude.");
                }
            }

            private static bool Passes(Presence wanted, bool actual)
            {
                return wanted == Presence.Any || (wanted == Presence.Require) == actual;
            }

            public bool Matches(SurfaceTile tile)
            {
                if (tile.WaterCovered || !tile.PrimaryBiome.canBuildBase)
                {
                    return false;
                }
                if (biomes != null && !biomes.Contains(tile.PrimaryBiome.defName))
                {
                    return false;
                }
                if (hilliness != null && !hilliness.Contains(tile.hilliness.ToString()))
                {
                    return false;
                }
                if (tile.rainfall < minRainfall || tile.rainfall > maxRainfall)
                {
                    return false;
                }
                if (60f / tile.PrimaryBiome.diseaseMtbDays > maxDiseases)
                {
                    return false;
                }

                IList<TileMutatorDef> mutators = tile.Mutators;
                if (excludedFeatures != null && mutators.Any(m => excludedFeatures.Contains(m.defName)))
                {
                    return false;
                }
                if (requiredFeatures != null && !requiredFeatures.All(f => mutators.Any(m => string.Equals(m.defName, f, StringComparison.OrdinalIgnoreCase))))
                {
                    return false;
                }

                PlanetTile id = tile.tile;
                if (!Passes(caves, Find.World.HasCaves(id))
                    || !Passes(river, tile.Rivers != null)
                    || !Passes(road, tile.Roads != null)
                    || !Passes(coast, tile.IsCoastal)
                    || !Passes(lake, Find.World.LakeDirectionAt(id) != Rot4.Invalid))
                {
                    return false;
                }

                if (tile.MinTemperature < minColdest || tile.MaxTemperature > maxHottest)
                {
                    return false;
                }
                if (minGrowingDays > 0 && GrowingDays(tile) < minGrowingDays)
                {
                    return false;
                }
                return TileFinder.IsValidTileForNewSettlement(id);
            }
        }
    }
}
