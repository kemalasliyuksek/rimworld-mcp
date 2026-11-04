using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorldMcp.Bridge.Core;
using Verse;
using Verse.AI;

namespace RimWorldMcp.Bridge.Game
{
    internal static class ThreatEndpoint
    {
        public static List<Pawn> VisibleHostiles(Map map)
        {
            return GameAccess.VisibleSpawnedPawns(map)
                .Where(p => p.HostileTo(Faction.OfPlayer) && !p.Downed && !p.IsPrisonerOfColony)
                .ToList();
        }

        /// <summary>False for sleeping mechanoids, ancient dangers and other dormant enemies.</summary>
        public static bool IsActive(Pawn pawn)
        {
            return GenHostility.IsActiveThreatToPlayer(pawn);
        }

        // The same sum as the game's gear tab. The pawn's own stat leaves out what they are wearing.
        private static float OverallArmor(Pawn pawn, StatDef stat)
        {
            float natural = Clamp(pawn.GetStatValue(stat) / 2f, 1f);
            float total = 0f;
            foreach (BodyPartRecord part in pawn.RaceProps.body.AllParts)
            {
                float unprotected = 1f - natural;
                if (pawn.apparel != null)
                {
                    foreach (Apparel apparel in pawn.apparel.WornApparel.Where(a => a.def.apparel.CoversBodyPart(part)))
                    {
                        unprotected *= 1f - Clamp(apparel.GetStatValue(stat) / 2f, 1f);
                    }
                }
                total += part.coverageAbs * (1f - unprotected);
            }
            return Clamp(total * 2f, 2f);
        }

        private static float Clamp(float value, float max)
        {
            return Math.Max(0f, Math.Min(value, max));
        }

        public static void Threats(QueryParams query, JsonWriter json)
        {
            Map map = GameAccess.ResolveMap(query);
            List<Pawn> hostiles = VisibleHostiles(map);

            json.BeginObject();
            json.Prop("danger", map.dangerWatcher.DangerRating.ToString());
            json.Prop("visible_hostile_pawns", hostiles.Count);

            json.Key("groups").BeginArray();
            foreach (var group in hostiles.GroupBy(p => (Faction: p.Faction?.Name, Kind: p.KindLabel, Active: IsActive(p))))
            {
                List<Pawn> members = group.ToList();
                json.BeginObject();
                json.Prop("faction", group.Key.Faction);
                json.Prop("kind", group.Key.Kind);
                json.Prop("count", members.Count);
                json.Prop("active", group.Key.Active);
                json.Prop("center_x", (int)members.Average(p => p.Position.x));
                json.Prop("center_z", (int)members.Average(p => p.Position.z));
                json.Key("weapons").BeginArray();
                foreach (string weapon in members.Select(p => p.equipment?.Primary?.def.label).Where(w => w != null).Distinct())
                {
                    json.Value(weapon);
                }
                json.EndArray();
                json.EndObject();
            }
            json.EndArray();

            WriteDefenders(json);
            WriteTurrets(json, map);
            json.EndObject();
        }

        private static void WriteDefenders(JsonWriter json)
        {
            json.Key("colonist_fighters").BeginArray();
            foreach (Pawn pawn in GameAccess.AllColonists())
            {
                if (pawn.WorkTagIsDisabled(WorkTags.Violent))
                {
                    continue;
                }
                json.BeginObject();
                json.Prop("name", pawn.LabelShort);
                json.Prop("weapon", GameAccess.Clean(pawn.equipment?.Primary?.LabelCap));
                json.Prop("shooting", pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0);
                json.Prop("melee", pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0);
                json.Prop("health_pct", GameAccess.Percent(pawn.health.summaryHealth.SummaryHealthPercent));
                json.Prop("armor_sharp_pct", GameAccess.Percent(OverallArmor(pawn, StatDefOf.ArmorRating_Sharp)));
                json.Prop("drafted", pawn.Drafted);
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WriteTurrets(JsonWriter json, Map map)
        {
            json.Key("turrets").BeginArray();
            foreach (Building_Turret turret in map.listerBuildings.allBuildingsColonist.OfType<Building_Turret>())
            {
                json.BeginObject();
                json.Prop("label", GameAccess.Clean(turret.LabelCap));
                GameAccess.WriteCell(json, turret.Position);
                json.Prop("health_pct", GameAccess.Percent(turret.HitPoints / (float)turret.MaxHitPoints));
                json.Prop("powered", turret.GetComp<CompPowerTrader>()?.PowerOn ?? true);
                CompRefuelable fuel = turret.GetComp<CompRefuelable>();
                if (fuel != null)
                {
                    json.Prop("fuel_pct", GameAccess.Percent(fuel.FuelPercentOfMax));
                }
                json.EndObject();
            }
            json.EndArray();
        }
    }
}
