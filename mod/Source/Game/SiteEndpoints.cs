using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
        // Search and details use the same resolution so that a tile keeps the same score in both.
        private const int CellsPerSample = 5;

        private const int MaxNearbySettlements = 5;

        // Close enough for one tile to stand out on a planet at full coverage.
        private const float SiteViewAltitude = 180f;

        private static readonly FieldInfo DesiredAltitudeField =
            typeof(WorldCameraDriver).GetField("desiredAltitude", BindingFlags.Instance | BindingFlags.NonPublic);

        private const int DaysPerTwelfth = 5;

        // The game's own definition of the outdoor growing period, as shown on the terrain tab.
        private const float MinGrowingTemperature = 6f;
        private const float MaxGrowingTemperature = 42f;

        // A search runs on the main thread, so it freezes the game while it works. A large planet has
        // hundreds of thousands of tiles, far more than can be checked in full, so the time limit
        // covers the whole search and the result says how much of the world it reached.
        private const long SearchBudgetMs = 4000;

        // Without a shape to predict, checking this many tiles is plenty to rank by climate.
        private const int MaxCheckedWithoutPrediction = 3000;

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
            json.Prop("starting_season", Find.GameInitData?.startingSeason.ToString());
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
            int minScore = query.GetInt("min_pocket_score", 0, 0, 100);
            int minRock = query.GetInt("min_rock_pct", 0, 0, 100);
            int maxRock = query.GetInt("max_rock_pct", 100, 0, 100);
            int offset = query.GetInt("offset", 0, 0, int.MaxValue);
            bool needsPrediction = rankByPocket || minRock > 0 || maxRock < 100;

            Stopwatch stopwatch = Stopwatch.StartNew();
            List<SurfaceTile> candidates = Find.WorldGrid.Tiles
                .Where(t => filter.MatchesCheaply(t) && (!rankByPocket || CanHaveMountains(t)))
                .ToList();
            Shuffle(candidates);
            HashSet<int> blocked = TilesNextToSettlements();

            int mapSize = MapSize();
            var found = new List<SiteResult>();
            int visited = 0;
            foreach (SurfaceTile tile in candidates.Skip(offset))
            {
                if (stopwatch.ElapsedMilliseconds > SearchBudgetMs || (!needsPrediction && visited >= MaxCheckedWithoutPrediction))
                {
                    break;
                }
                visited++;
                if (blocked.Contains(tile.tile.tileId) || !filter.MatchesFully(tile))
                {
                    continue;
                }

                var result = new SiteResult { Tile = tile, GrowingDays = GrowingDays(tile) };
                if (needsPrediction)
                {
                    result.Prediction = MountainPredictor.Predict(tile, mapSize, CellsPerSample);
                    result.Pocket = PocketAnalyzer.Analyze(result.Prediction.Rock, CellsPerSample);
                    if (result.Pocket.Score < minScore || result.Pocket.RockPercent < minRock || result.Pocket.RockPercent > maxRock)
                    {
                        continue;
                    }
                }
                found.Add(result);
            }

            IEnumerable<SiteResult> ranked = rankByPocket
                ? found.OrderByDescending(r => r.Pocket.Score)
                : found.OrderByDescending(r => r.GrowingDays).ThenBy(r => Math.Abs(r.Tile.temperature - 18f));

            json.BeginObject();
            json.Prop("candidates", candidates.Count);
            json.Prop("checked", visited);
            bool finished = offset + visited >= candidates.Count;
            json.Prop("whole_world_checked", offset == 0 && finished);
            if (!finished)
            {
                json.Prop("next_offset", offset + visited);
            }
            json.Prop("found", found.Count);
            json.Prop("sorted_by", rankByPocket ? "pocket score" : "growing days, then mildest climate");
            json.Key("sites").BeginArray();
            // The game's own validity check loops over every settlement, so it only runs on the few
            // sites that are about to be returned.
            foreach (SiteResult site in ranked.Where(r => TileFinder.IsValidTileForNewSettlement(r.Tile.tile)).Take(limit))
            {
                WriteSite(json, site.Tile, site.Prediction == null ? ((SurfaceTile, MountainPredictor.Prediction, PocketResult)?)null : (site.Tile, site.Prediction, site.Pocket), detailed: false);
            }
            json.EndArray();
            json.Prop("search_ms", stopwatch.ElapsedMilliseconds);
            json.EndObject();
        }

        private sealed class SiteResult
        {
            public SurfaceTile Tile;
            public int GrowingDays;
            public MountainPredictor.Prediction Prediction;
            public PocketResult Pocket;
        }

        // Flat land only reaches rock height through a mountain feature. Skipping the rest leaves the
        // time budget for tiles that can actually have a pocket.
        private static bool CanHaveMountains(SurfaceTile tile)
        {
            return tile.hilliness != Hilliness.Flat || tile.Mutators.Any(m => m.Worker is TileMutatorWorker_Mountain);
        }

        // Visiting tiles in a random order spreads a limited search across the whole planet. It uses
        // its own generator so that the game's random numbers stay untouched.
        private static void Shuffle(List<SurfaceTile> tiles)
        {
            var random = new System.Random(Find.World.info.Seed);
            for (int i = tiles.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                SurfaceTile swapped = tiles[i];
                tiles[i] = tiles[other];
                tiles[other] = swapped;
            }
        }

        // The game cannot settle on or next to a settlement. Working this out once is what makes a
        // world-wide search possible: asking per tile would loop over every settlement every time.
        private static HashSet<int> TilesNextToSettlements()
        {
            var blocked = new HashSet<int>();
            var neighbors = new List<PlanetTile>();
            PlanetLayer surface = Find.WorldGrid.Surface;
            foreach (Settlement settlement in Find.WorldObjects.Settlements)
            {
                PlanetTile tile = settlement.Tile;
                if (tile.Layer != surface)
                {
                    continue;
                }
                blocked.Add(tile.tileId);
                neighbors.Clear();
                Find.WorldGrid.GetTileNeighbors(tile, neighbors);
                foreach (PlanetTile neighbor in neighbors)
                {
                    blocked.Add(neighbor.tileId);
                }
            }
            return blocked;
        }

        public static void Get(QueryParams query, JsonWriter json)
        {
            RequireWorld();
            SurfaceTile tile = ResolveTile(query);
            int mapSize = MapSize();
            MountainPredictor.Prediction prediction = MountainPredictor.Predict(tile, mapSize, CellsPerSample);
            PocketResult pocket = PocketAnalyzer.Analyze(prediction.Rock, CellsPerSample);

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

            // The order matters: jumping to a tile sets the selected planet layer, and that clears the
            // selected tile.
            Find.WorldCameraDriver.JumpTo(tile.tile);
            Find.WorldSelector.ClearSelection();
            Find.WorldInterface.SelectedTile = tile.tile;

            WorldCameraDriver camera = Find.WorldCameraDriver;
            if (camera.altitude > SiteViewAltitude)
            {
                camera.altitude = SiteViewAltitude;
                DesiredAltitudeField?.SetValue(camera, SiteViewAltitude);
            }

            json.BeginObject();
            json.Prop("tile", tile.tile.tileId);
            json.Prop("biome", tile.PrimaryBiome.label);
            json.Prop("selected", Find.WorldInterface.SelectedTile == tile.tile);
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
            json.Prop("pollution_pct", GameAccess.Percent(tile.pollution));

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
                json.Prop("movement_difficulty", WorldPathGrid.CalculatedMovementDifficultyAt(id, perceivedStatic: false));
                json.Prop("nearby_pollution_score", WorldPollutionUtility.CalculateNearbyPollutionScore(id));
                json.Prop("animals_can_graze_now", VirtualPlantsUtility.EnvironmentAllowsEatingVirtualPlantsNowAt(id));
                json.Prop("time_zone", GenDate.TimeZoneAt(longLat.x));
                WriteMonthlyTemperatures(json, id);
                WriteNearbySettlements(json, id);
                json.Prop("swampiness", tile.swampiness);
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

        private static void WriteMonthlyTemperatures(JsonWriter json, PlanetTile id)
        {
            json.Key("monthly_average_temperatures_c").BeginArray();
            foreach (Twelfth twelfth in Enum.GetValues(typeof(Twelfth)).Cast<Twelfth>().Where(t => t != Twelfth.Undefined))
            {
                json.Value(GenTemperature.AverageTemperatureAtTileForTwelfth(id, twelfth));
            }
            json.EndArray();
        }

        private static void WriteNearbySettlements(JsonWriter json, PlanetTile id)
        {
            var nearest = Find.WorldObjects.Settlements
                .Where(s => s.Faction != null && s.Faction != Faction.OfPlayer && s.Tile.Layer == id.Layer)
                .Select(s => (Settlement: s, Distance: Find.WorldGrid.ApproxDistanceInTiles(id, s.Tile)))
                .OrderBy(s => s.Distance)
                .Take(MaxNearbySettlements);

            json.Key("nearby_settlements").BeginArray();
            foreach ((Settlement settlement, float distance) in nearest)
            {
                json.BeginObject();
                json.Prop("name", settlement.LabelCap);
                json.Prop("faction", settlement.Faction.Name);
                json.Prop("relation", settlement.Faction.PlayerRelationKind.ToString());
                json.Prop("distance_tiles", distance);
                json.EndObject();
            }
            json.EndArray();
        }

        /// <summary>The map size and starting season from the screen's advanced settings.</summary>
        public static void SetOptions(QueryParams query, JsonWriter json)
        {
            RequireWorld();
            if (!IsOnSiteSelection())
            {
                throw new BridgeException(409, "not_selecting_site", "The player is not on the site selection screen.");
            }

            if (query.GetString("map_size") != null)
            {
                int size = query.GetInt("map_size", 0, 1, int.MaxValue);
                if (!Dialog_AdvancedGameConfig.MapSizes.Contains(size))
                {
                    throw new BadRequestException($"Map size must be one of: {string.Join(", ", Dialog_AdvancedGameConfig.MapSizes)}.");
                }
                Find.GameInitData.mapSize = size;
            }

            string season = query.GetString("season");
            if (season != null)
            {
                if (!Enum.TryParse(season, ignoreCase: true, out Season parsed) || parsed == Season.PermanentSummer || parsed == Season.PermanentWinter)
                {
                    throw new BadRequestException("Season must be Undefined (the game's default), Spring, Summer, Fall or Winter.");
                }
                Find.GameInitData.startingSeason = parsed;
            }

            json.BeginObject();
            json.Prop("map_size", Find.GameInitData.mapSize);
            json.Prop("starting_season", Find.GameInitData.startingSeason.ToString());
            json.EndObject();
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
            int centerX = pocket.Score > 0 ? pocket.CenterX / CellsPerSample : -1;
            int centerZ = pocket.Score > 0 ? pocket.CenterZ / CellsPerSample : -1;

            json.Prop("preview_note", $"Predicted rock. # is rock, . is open ground, X is the pocket centre. The first row is north, and each character is {CellsPerSample} cells. Water, caves, ores and ruins are not shown.");
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
            private const int NoTemperatureLimit = -1000;

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
            private readonly float maxPollution;
            private readonly int minStoneTypes;
            private readonly List<string> requiredStones;
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
                minColdest = query.GetInt("min_coldest_c", NoTemperatureLimit, NoTemperatureLimit, -NoTemperatureLimit);
                maxHottest = query.GetInt("max_hottest_c", -NoTemperatureLimit, NoTemperatureLimit, -NoTemperatureLimit);
                maxDiseases = query.GetInt("max_diseases_per_year", int.MaxValue, 0, int.MaxValue);
                minGrowingDays = query.GetInt("min_growing_days", 0, 0, 60);
                maxPollution = query.GetInt("max_pollution_pct", 100, 0, 100) / 100f;
                minStoneTypes = query.GetInt("min_stone_types", 0, 0, 10);
                requiredStones = query.GetList("require_stones");
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
                return (wanted == Presence.Require) == actual;
            }

            /// <summary>Only reads stored fields, so it is safe to run on every tile in the world.</summary>
            public bool MatchesCheaply(SurfaceTile tile)
            {
                BiomeDef biome = tile.PrimaryBiome;
                if (tile.WaterCovered || !biome.canBuildBase || !biome.implemented || tile.hilliness == Hilliness.Impassable)
                {
                    return false;
                }
                if (biomes != null && !biomes.Contains(biome.defName))
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
                if (60f / biome.diseaseMtbDays > maxDiseases || tile.pollution > maxPollution)
                {
                    return false;
                }

                // The yearly average lies between the extremes, so it can rule a tile out for free.
                if (tile.temperature < minColdest || tile.temperature > maxHottest)
                {
                    return false;
                }
                if (minGrowingDays > 0 && tile.temperature < MinGrowingTemperature)
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
                if (caves != Presence.Any && !Passes(caves, Find.World.HasCaves(tile.tile)))
                {
                    return false;
                }
                if (river != Presence.Any && !Passes(river, tile.Rivers != null))
                {
                    return false;
                }
                return road == Presence.Any || Passes(road, tile.Roads != null);
            }

            /// <summary>The checks that look at neighboring tiles or sample a whole year of weather.</summary>
            public bool MatchesFully(SurfaceTile tile)
            {
                if (coast != Presence.Any && !Passes(coast, tile.IsCoastal))
                {
                    return false;
                }
                if (lake != Presence.Any && !Passes(lake, Find.World.LakeDirectionAt(tile.tile) != Rot4.Invalid))
                {
                    return false;
                }
                if (minColdest > NoTemperatureLimit && tile.MinTemperature < minColdest)
                {
                    return false;
                }
                if (maxHottest < -NoTemperatureLimit && tile.MaxTemperature > maxHottest)
                {
                    return false;
                }
                if (minGrowingDays > 0 && GrowingDays(tile) < minGrowingDays)
                {
                    return false;
                }
                if (minStoneTypes == 0 && requiredStones.Count == 0)
                {
                    return true;
                }
                List<ThingDef> stones = Find.World.NaturalRockTypesIn(tile.tile).ToList();
                return stones.Count >= minStoneTypes
                    && requiredStones.All(r => stones.Any(s => string.Equals(s.defName, r, StringComparison.OrdinalIgnoreCase)));
            }
        }
    }
}
