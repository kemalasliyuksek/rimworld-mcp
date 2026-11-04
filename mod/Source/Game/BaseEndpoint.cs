using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>The colony's infrastructure: rooms, power, zones, production and storage.</summary>
    internal static class BaseEndpoint
    {
        private static readonly string[] AllSections = { "rooms", "power", "zones", "production", "beds" };

        private const int MaxRooms = 80;

        public static void Base(QueryParams query, JsonWriter json)
        {
            Map map = GameAccess.ResolveMap(query);
            var sections = new HashSet<string>(query.GetList("sections"));
            if (sections.Count == 0)
            {
                sections.UnionWith(AllSections);
            }
            string unknown = sections.FirstOrDefault(s => !AllSections.Contains(s));
            if (unknown != null)
            {
                throw new BadRequestException($"Unknown section '{unknown}'. Use: {string.Join(", ", AllSections)}.");
            }

            json.BeginObject();
            if (sections.Contains("rooms"))
            {
                WriteRooms(json, map);
            }
            if (sections.Contains("power"))
            {
                WritePower(json, map);
            }
            if (sections.Contains("zones"))
            {
                WriteZones(json, map);
            }
            if (sections.Contains("production"))
            {
                WriteProduction(json, map);
            }
            if (sections.Contains("beds"))
            {
                WriteBeds(json, map);
            }
            json.EndObject();
        }

        private static void WriteRooms(JsonWriter json, Map map)
        {
            List<Room> rooms = map.regionGrid.AllRooms
                .Where(r => r.ProperRoom && !r.PsychologicallyOutdoors && !r.Fogged)
                .OrderByDescending(r => r.CellCount)
                .ToList();

            json.Key("rooms").BeginArray();
            foreach (Room room in rooms.Take(MaxRooms))
            {
                json.BeginObject();
                json.Prop("role", room.GetRoomRoleLabel());
                json.Prop("cells", room.CellCount);
                json.Prop("temperature_c", room.Temperature);
                json.Prop("impressiveness", room.GetStat(RoomStatDefOf.Impressiveness));
                json.Prop("beauty", room.GetStat(RoomStatDefOf.Beauty));
                json.Prop("cleanliness", room.GetStat(RoomStatDefOf.Cleanliness));
                json.Prop("space", room.GetStat(RoomStatDefOf.Space));
                if (room.OpenRoofCount > 0)
                {
                    json.Prop("unroofed_cells", room.OpenRoofCount);
                }
                string owners = string.Join(", ", room.Owners.Select(p => p.LabelShort));
                if (owners.Length > 0)
                {
                    json.Prop("owners", owners);
                }
                IntVec3 sample = room.Cells.First();
                GameAccess.WriteCell(json, sample);
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WritePower(JsonWriter json, Map map)
        {
            json.Key("power_grids").BeginArray();
            foreach (PowerNet net in map.powerNetManager.AllNetsListForReading)
            {
                if (!net.hasPowerSource && net.batteryComps.Count == 0)
                {
                    continue;
                }
                // Per-tick values are multiplied up to the watts shown in the game's own readout.
                json.BeginObject();
                json.Prop("net_power_watts", net.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick);
                json.Prop("stored_watt_days", net.CurrentStoredEnergy());
                json.Prop("batteries", net.batteryComps.Count);
                json.Prop("consumers_and_producers", net.powerComps.Count);
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WriteZones(JsonWriter json, Map map)
        {
            json.Key("zones").BeginArray();
            foreach (Zone zone in map.zoneManager.AllZones)
            {
                json.BeginObject();
                json.Prop("label", GameAccess.Clean(zone.RenamableLabel));
                json.Prop("cells", zone.CellCount);
                GameAccess.WriteCell(json, zone.Position);
                if (zone is Zone_Growing growing)
                {
                    WriteGrowing(json, map, growing);
                }
                else if (zone is Zone_Stockpile stockpile)
                {
                    json.Prop("type", "stockpile");
                    json.Prop("priority", stockpile.GetStoreSettings().Priority.ToString());
                }
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WriteGrowing(JsonWriter json, Map map, Zone_Growing zone)
        {
            ThingDef plantDef = zone.GetPlantDefToGrow();
            List<Plant> plants = zone.cells
                .Select(c => c.GetPlant(map))
                .Where(p => p != null && p.def == plantDef)
                .ToList();

            json.Prop("type", "growing");
            json.Prop("plant", plantDef?.label);
            json.Prop("sowing_allowed", zone.allowSow);
            json.Prop("planted_cells", plants.Count);
            if (plants.Count > 0)
            {
                json.Prop("average_growth_pct", GameAccess.Percent(plants.Average(p => p.Growth)));
                json.Prop("harvestable", plants.Count(p => p.HarvestableNow));
            }
        }

        private static void WriteProduction(JsonWriter json, Map map)
        {
            json.Key("work_benches").BeginArray();
            foreach (Building table in map.listerBuildings.allBuildingsColonist.Where(b => b is IBillGiver && b.def.AllRecipes.Count > 0))
            {
                json.BeginObject();
                json.Prop("id", table.ThingID);
                json.Prop("label", GameAccess.Clean(table.LabelCap));
                GameAccess.WriteCell(json, table.Position);
                CompPowerTrader power = table.GetComp<CompPowerTrader>();
                if (power != null)
                {
                    json.Prop("powered", power.PowerOn);
                }
                json.Key("bills").BeginArray();
                foreach (Bill bill in ((IBillGiver)table).BillStack.Bills)
                {
                    WriteBill(json, bill);
                }
                json.EndArray();
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WriteBill(JsonWriter json, Bill bill)
        {
            json.BeginObject();
            json.Prop("label", GameAccess.Clean(bill.LabelCap));
            json.Prop("suspended", bill.suspended);
            if (bill is Bill_Production production)
            {
                json.Prop("mode", production.repeatMode?.defName);
                json.Prop("repeat_info", GameAccess.Clean(production.RepeatInfoText));
                json.Prop("paused", production.paused);
            }
            json.EndObject();
        }

        private static void WriteBeds(JsonWriter json, Map map)
        {
            List<Building_Bed> beds = map.listerBuildings.allBuildingsColonist
                .OfType<Building_Bed>()
                .Where(b => b.def.building.bed_humanlike)
                .ToList();

            json.Key("beds").BeginObject();
            json.Prop("humanlike_total", beds.Count);
            json.Prop("medical", beds.Count(b => b.Medical));
            json.Prop("for_prisoners", beds.Count(b => b.ForPrisoners));
            json.Prop("unassigned", beds.Count(b => !b.Medical && !b.ForPrisoners && !b.OwnersForReading.Any()));
            json.EndObject();
        }
    }
}
