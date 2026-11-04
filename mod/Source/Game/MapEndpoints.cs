using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>Text-based views of the map, which give spatial awareness without screenshots.</summary>
    internal static class MapEndpoints
    {
        private const int DefaultViewSize = 60;

        private const int MaxViewSize = 120;

        private static readonly string[][] Legend =
        {
            new[] { "@", "colonist" },
            new[] { "H", "hostile pawn" },
            new[] { "P", "prisoner or slave" },
            new[] { "A", "colony animal" },
            new[] { "a", "other animal" },
            new[] { "V", "visitor or other pawn" },
            new[] { "#", "wall or other impassable building" },
            new[] { "+", "door" },
            new[] { "T", "turret" },
            new[] { "B", "bed" },
            new[] { "W", "work bench" },
            new[] { "F", "other building or furniture" },
            new[] { "%", "natural rock" },
            new[] { "i", "item on the ground" },
            new[] { "t", "tree" },
            new[] { "p", "other plant" },
            new[] { "s", "stockpile zone" },
            new[] { "g", "growing zone" },
            new[] { "~", "water" },
            new[] { "_", "built floor" },
            new[] { ".", "open ground" },
            new[] { "?", "unexplored" },
        };

        public static void View(QueryParams query, JsonWriter json)
        {
            Map map = GameAccess.ResolveMap(query);
            int width = query.GetInt("width", DefaultViewSize, 1, MaxViewSize);
            int height = query.GetInt("height", DefaultViewSize, 1, MaxViewSize);

            // By default, centre on what the player is looking at.
            IntVec3 center = map == Find.CurrentMap ? Find.CameraDriver.MapPosition : map.Center;
            int minX = query.GetInt("x", center.x - width / 2, int.MinValue, int.MaxValue);
            int minZ = query.GetInt("z", center.z - height / 2, int.MinValue, int.MaxValue);
            minX = Math.Max(0, Math.Min(minX, map.Size.x - width));
            minZ = Math.Max(0, Math.Min(minZ, map.Size.z - height));
            int maxX = Math.Min(minX + width - 1, map.Size.x - 1);
            int maxZ = Math.Min(minZ + height - 1, map.Size.z - 1);

            json.BeginObject();
            json.Prop("map", map.uniqueID);
            json.Prop("min_x", minX);
            json.Prop("min_z", minZ);
            json.Prop("max_x", maxX);
            json.Prop("max_z", maxZ);
            json.Prop("row_order", "The first row is max_z (north). Each row runs from min_x to max_x.");
            json.Key("rows").BeginArray();
            var row = new StringBuilder(width);
            for (int z = maxZ; z >= minZ; z--)
            {
                row.Clear();
                for (int x = minX; x <= maxX; x++)
                {
                    row.Append(SymbolAt(new IntVec3(x, 0, z), map));
                }
                json.Value(row.ToString());
            }
            json.EndArray();
            json.Key("legend").BeginObject();
            foreach (string[] entry in Legend)
            {
                json.Prop(entry[0], entry[1]);
            }
            json.EndObject();
            json.EndObject();
        }

        internal static char SymbolAt(IntVec3 cell, Map map)
        {
            if (cell.Fogged(map))
            {
                return '?';
            }

            List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
            char building = '\0';
            bool item = false;
            char plant = '\0';
            foreach (Thing thing in things)
            {
                if (thing is Pawn pawn)
                {
                    if (GameAccess.IsVisible(pawn))
                    {
                        return PawnSymbol(pawn);
                    }
                }
                else if (thing is Building || thing.def.building != null)
                {
                    building = BuildingSymbol(thing);
                }
                else if (thing is Plant)
                {
                    plant = thing.def.plant.IsTree ? 't' : 'p';
                }
                else if (thing.def.category == ThingCategory.Item)
                {
                    item = true;
                }
            }

            if (building != '\0')
            {
                return building;
            }
            if (item)
            {
                return 'i';
            }
            if (plant != '\0')
            {
                return plant;
            }

            Zone zone = map.zoneManager.ZoneAt(cell);
            if (zone is Zone_Stockpile)
            {
                return 's';
            }
            if (zone is Zone_Growing)
            {
                return 'g';
            }

            TerrainDef terrain = cell.GetTerrain(map);
            if (terrain.IsWater)
            {
                return '~';
            }
            return terrain.layerable ? '_' : '.';
        }

        private static char PawnSymbol(Pawn pawn)
        {
            if (pawn.IsFreeColonist)
            {
                return '@';
            }
            if (pawn.HostileTo(Faction.OfPlayer))
            {
                return 'H';
            }
            if (pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony)
            {
                return 'P';
            }
            if (pawn.RaceProps.Animal)
            {
                return pawn.Faction == Faction.OfPlayer ? 'A' : 'a';
            }
            return 'V';
        }

        private static char BuildingSymbol(Thing thing)
        {
            if (thing.def.building != null && thing.def.building.isNaturalRock)
            {
                return '%';
            }
            switch (thing)
            {
                case Building_Door _:
                    return '+';
                case Building_Turret _:
                    return 'T';
                case Building_Bed _:
                    return 'B';
                case Building_WorkTable _:
                    return 'W';
            }
            return thing.def.passability == Traversability.Impassable ? '#' : 'F';
        }

        public static void Cell(QueryParams query, JsonWriter json)
        {
            Map map = GameAccess.ResolveMap(query);
            var cell = new IntVec3(
                query.RequireInt("x", 0, map.Size.x - 1),
                0,
                query.RequireInt("z", 0, map.Size.z - 1));

            json.BeginObject();
            GameAccess.WriteCell(json, cell);
            if (cell.Fogged(map))
            {
                json.Prop("fogged", true);
                json.EndObject();
                return;
            }

            TerrainDef terrain = cell.GetTerrain(map);
            json.Prop("terrain", terrain.label);
            json.Prop("fertility", terrain.fertility);
            json.Prop("beauty", BeautyUtility.AverageBeautyPerceptible(cell, map));
            json.Prop("roof", cell.GetRoof(map)?.label);
            json.Prop("temperature_c", cell.GetTemperature(map));
            json.Prop("zone", GameAccess.Clean(map.zoneManager.ZoneAt(cell)?.RenamableLabel));

            Room room = cell.GetRoom(map);
            if (room != null && !room.PsychologicallyOutdoors)
            {
                json.Key("room").BeginObject();
                json.Prop("role", room.GetRoomRoleLabel());
                json.Prop("cells", room.CellCount);
                json.Prop("impressiveness", room.GetStat(RoomStatDefOf.Impressiveness));
                json.EndObject();
            }

            json.Key("things").BeginArray();
            foreach (Thing thing in map.thingGrid.ThingsListAtFast(cell).Where(GameAccess.IsVisible))
            {
                json.BeginObject();
                json.Prop("id", thing.ThingID);
                json.Prop("def", thing.def.defName);
                json.Prop("label", GameAccess.Clean(thing.LabelCap));
                json.Prop("category", thing.def.category.ToString());
                if (thing.Faction != null)
                {
                    json.Prop("faction", thing.Faction.Name);
                }
                if (thing.def.useHitPoints)
                {
                    json.Prop("hit_points", thing.HitPoints);
                    json.Prop("max_hit_points", thing.MaxHitPoints);
                }
                json.EndObject();
            }
            json.EndArray();
            json.EndObject();
        }
    }
}
