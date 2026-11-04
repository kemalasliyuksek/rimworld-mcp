using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    internal static class PawnEndpoints
    {
        private const int MaxThoughtDescription = 200;

        public static void List(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            string group = query.GetString("group", "colonists");
            int limit = query.GetInt("limit", 100, 1, 500);
            List<Pawn> pawns = PawnsInGroup(group, query);

            json.BeginObject();
            json.Prop("group", group);
            json.Prop("total", pawns.Count);
            json.Key("pawns").BeginArray();
            foreach (Pawn pawn in pawns.Take(limit))
            {
                WriteSummary(json, pawn);
            }
            json.EndArray();
            json.EndObject();
        }

        private static List<Pawn> PawnsInGroup(string group, QueryParams query)
        {
            if (group == "colonists")
            {
                return GameAccess.AllColonists();
            }

            Map map = GameAccess.ResolveMap(query);
            IEnumerable<Pawn> visible = GameAccess.VisibleSpawnedPawns(map);
            switch (group)
            {
                case "prisoners":
                    return visible.Where(p => p.IsPrisonerOfColony).ToList();
                case "slaves":
                    return visible.Where(p => p.IsSlaveOfColony).ToList();
                case "animals":
                    return visible.Where(p => p.Faction == Faction.OfPlayer && p.RaceProps.Animal).ToList();
                case "mechanoids":
                    return visible.Where(p => p.IsColonyMech).ToList();
                case "hostiles":
                    return ThreatEndpoint.VisibleHostiles(map);
                case "visitors":
                    return visible.Where(p => p.Faction != null && p.Faction != Faction.OfPlayer && p.RaceProps.Humanlike && !p.HostileTo(Faction.OfPlayer) && !p.IsPrisonerOfColony).ToList();
                case "wildlife":
                    return visible.Where(p => p.Faction == null && p.RaceProps.Animal).ToList();
                default:
                    throw new BadRequestException($"Unknown group '{group}'. Use colonists, prisoners, slaves, animals, mechanoids, hostiles, visitors or wildlife.");
            }
        }

        private static void WriteSummary(JsonWriter json, Pawn pawn)
        {
            json.BeginObject();
            json.Prop("id", pawn.ThingID);
            json.Prop("name", pawn.LabelShort);
            json.Prop("kind", pawn.KindLabel);
            json.Prop("age", pawn.ageTracker.AgeBiologicalYears);
            GameAccess.WritePosition(json, pawn);
            json.Prop("health_pct", GameAccess.Percent(pawn.health.summaryHealth.SummaryHealthPercent));
            if (pawn.needs?.mood != null)
            {
                json.Prop("mood_pct", GameAccess.Percent(pawn.needs.mood.CurLevelPercentage));
            }
            json.Prop("activity", CurrentActivity(pawn));
            if (pawn.Drafted)
            {
                json.Prop("drafted", true);
            }
            if (pawn.Downed)
            {
                json.Prop("downed", true);
            }
            if (pawn.InMentalState)
            {
                json.Prop("mental_state", pawn.MentalStateDef?.label);
            }
            if (pawn.skills != null)
            {
                json.Key("top_skills").BeginArray();
                foreach (SkillRecord skill in pawn.skills.skills.Where(s => !s.TotallyDisabled).OrderByDescending(s => s.Level).Take(4))
                {
                    json.Value($"{skill.def.skillLabel} {skill.Level}{PassionSuffix(skill.passion)}");
                }
                json.EndArray();
            }
            json.EndObject();
        }

        private static string PassionSuffix(Passion passion)
        {
            switch (passion)
            {
                case Passion.Minor:
                    return "*";
                case Passion.Major:
                    return "**";
                default:
                    return "";
            }
        }

        private static string CurrentActivity(Pawn pawn)
        {
            if (pawn.Dead)
            {
                return "dead";
            }
            try
            {
                return GameAccess.Clean(pawn.jobs?.curDriver?.GetReport()) ?? pawn.CurJobDef?.defName;
            }
            catch (System.Exception)
            {
                // Job reports can throw for jobs that mods have left in an odd state.
                return pawn.CurJobDef?.defName;
            }
        }

        public static void Detail(QueryParams query, JsonWriter json)
        {
            Pawn pawn = GameAccess.FindPawn(query.RequireString("id"));

            json.BeginObject();
            json.Prop("id", pawn.ThingID);
            json.Prop("name", pawn.Name?.ToStringFull ?? pawn.LabelShort);
            json.Prop("kind", pawn.KindLabel);
            json.Prop("race", pawn.def.label);
            json.Prop("gender", pawn.gender.ToString());
            json.Prop("age_biological", pawn.ageTracker.AgeBiologicalYears);
            json.Prop("age_chronological", pawn.ageTracker.AgeChronologicalYears);
            json.Prop("faction", pawn.Faction?.Name);
            json.Prop("activity", CurrentActivity(pawn));
            json.Prop("drafted", pawn.Drafted);
            json.Prop("downed", pawn.Downed);
            json.Prop("mental_state", pawn.MentalStateDef?.label);
            json.Prop("inspiration", pawn.Inspiration?.def.label);
            GameAccess.WritePosition(json, pawn);

            WriteBackground(json, pawn);
            WriteSkills(json, pawn);
            WriteNeeds(json, pawn);
            WriteThoughts(json, pawn);
            WriteHealth(json, pawn);
            WriteGear(json, pawn);
            WriteWork(json, pawn);
            GameAccess.WriteRelations(json, pawn);
            json.EndObject();
        }

        private static void WriteBackground(JsonWriter json, Pawn pawn)
        {
            if (pawn.story == null)
            {
                return;
            }
            json.Prop("title", GameAccess.Clean(pawn.story.TitleCap));
            json.Prop("childhood", GameAccess.Clean(pawn.story.Childhood?.TitleCapFor(pawn.gender)));
            json.Prop("adulthood", GameAccess.Clean(pawn.story.Adulthood?.TitleCapFor(pawn.gender)));
            json.Key("traits").BeginArray();
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                json.BeginObject();
                json.Prop("label", GameAccess.Clean(trait.LabelCap));
                json.Prop("def", trait.def.defName);
                json.EndObject();
            }
            json.EndArray();
            if (ModsConfig.IdeologyActive && pawn.Ideo != null)
            {
                json.Prop("ideoligion", pawn.Ideo.name);
                json.Prop("ideo_role", pawn.Ideo.GetRole(pawn)?.LabelCap);
            }
            if (ModsConfig.BiotechActive && pawn.genes != null)
            {
                json.Prop("xenotype", pawn.genes.XenotypeLabelCap);
            }
            if (pawn.royalty != null && pawn.royalty.MostSeniorTitle != null)
            {
                json.Prop("royal_title", pawn.royalty.MostSeniorTitle.def.GetLabelFor(pawn));
            }
        }

        private static void WriteSkills(JsonWriter json, Pawn pawn)
        {
            if (pawn.skills == null)
            {
                return;
            }
            json.Key("skills").BeginArray();
            foreach (SkillRecord skill in pawn.skills.skills)
            {
                json.BeginObject();
                json.Prop("skill", skill.def.skillLabel);
                json.Prop("level", skill.Level);
                json.Prop("passion", skill.passion.ToString());
                if (skill.TotallyDisabled)
                {
                    json.Prop("disabled", true);
                }
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WriteNeeds(JsonWriter json, Pawn pawn)
        {
            if (pawn.needs == null)
            {
                return;
            }
            json.Key("needs").BeginArray();
            foreach (Need need in pawn.needs.AllNeeds)
            {
                if (!need.ShowOnNeedList)
                {
                    continue;
                }
                json.BeginObject();
                json.Prop("need", GameAccess.Clean(need.LabelCap));
                json.Prop("def", need.def.defName);
                json.Prop("pct", GameAccess.Percent(need.CurLevelPercentage));
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WriteThoughts(JsonWriter json, Pawn pawn)
        {
            ThoughtHandler thoughts = pawn.needs?.mood?.thoughts;
            if (thoughts == null)
            {
                return;
            }
            var groups = new List<Thought>();
            thoughts.GetDistinctMoodThoughtGroups(groups);

            json.Key("mood_thoughts").BeginArray();
            // Some thoughts, such as an addiction that is satisfied, have no label and the game hides them.
            foreach (Thought group in groups.Where(t => !t.LabelCap.NullOrEmpty()).OrderBy(t => thoughts.MoodOffsetOfGroup(t)))
            {
                json.BeginObject();
                json.Prop("label", GameAccess.Clean(group.LabelCap));
                json.Prop("def", group.def.defName);
                json.Prop("mood", thoughts.MoodOffsetOfGroup(group));
                json.Prop("description", GameAccess.Truncate(GameAccess.Clean(group.Description), MaxThoughtDescription));
                json.EndObject();
            }
            json.EndArray();
            json.Prop("mental_break_threshold_minor_pct", GameAccess.Percent(pawn.mindState.mentalBreaker.BreakThresholdMinor));
        }

        private static bool IsCoveredByAddedPart(Hediff hediff, Pawn pawn)
        {
            return hediff is Hediff_MissingPart && hediff.Part != null && pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(hediff.Part);
        }

        private static void WriteHealth(JsonWriter json, Pawn pawn)
        {
            json.Key("health").BeginObject();
            json.Prop("summary_pct", GameAccess.Percent(pawn.health.summaryHealth.SummaryHealthPercent));
            json.Prop("bleed_rate_per_day", pawn.health.hediffSet.BleedRateTotal);
            json.Key("conditions").BeginArray();
            // The parts under a bionic limb count as missing, which is dozens of lines that say nothing.
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs.Where(h => h.Visible && !IsCoveredByAddedPart(h, pawn)))
            {
                json.BeginObject();
                json.Prop("label", GameAccess.Clean(hediff.LabelCap));
                json.Prop("def", hediff.def.defName);
                json.Prop("part", hediff.Part?.Label);
                json.Prop("severity", hediff.Severity);
                if (hediff.Bleeding)
                {
                    json.Prop("bleed_rate", hediff.BleedRate);
                }
                if (hediff.TendableNow())
                {
                    json.Prop("needs_tending", true);
                }
                json.EndObject();
            }
            json.EndArray();
            if (pawn.health.capacities != null)
            {
                json.Key("capacities").BeginArray();
                foreach (PawnCapacityDef capacity in DefDatabase<PawnCapacityDef>.AllDefsListForReading.Where(c => c.showOnHumanlikes == pawn.RaceProps.Humanlike || c.showOnAnimals == pawn.RaceProps.Animal))
                {
                    if (!PawnCapacityUtility.BodyCanEverDoCapacity(pawn.RaceProps.body, capacity))
                    {
                        continue;
                    }
                    json.BeginObject();
                    json.Prop("capacity", capacity.GetLabelFor(pawn));
                    json.Prop("pct", GameAccess.Percent(pawn.health.capacities.GetLevel(capacity)));
                    json.EndObject();
                }
                json.EndArray();
            }
            json.EndObject();
        }

        private static void WriteGear(JsonWriter json, Pawn pawn)
        {
            json.Key("gear").BeginObject();
            json.Prop("weapon", GameAccess.Clean(pawn.equipment?.Primary?.LabelCap));
            json.Key("apparel").BeginArray();
            if (pawn.apparel != null)
            {
                foreach (Apparel apparel in pawn.apparel.WornApparel)
                {
                    json.Value(GameAccess.Clean(apparel.LabelCap));
                }
            }
            json.EndArray();
            json.Key("inventory").BeginArray();
            if (pawn.inventory != null)
            {
                foreach (Thing item in pawn.inventory.innerContainer)
                {
                    json.Value(GameAccess.Clean(item.LabelCap));
                }
            }
            json.EndArray();
            json.EndObject();
        }

        internal static void WriteWork(JsonWriter json, Pawn pawn)
        {
            if (pawn.workSettings == null || !pawn.workSettings.EverWork)
            {
                return;
            }
            json.Key("work").BeginObject();
            json.Prop("manual_priorities", Find.PlaySettings.useWorkPriorities);
            json.Key("priorities").BeginArray();
            foreach (WorkTypeDef workType in WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder)
            {
                if (!workType.visible)
                {
                    continue;
                }
                json.BeginObject();
                json.Prop("work", workType.labelShort);
                json.Prop("def", workType.defName);
                if (pawn.WorkTypeIsDisabled(workType))
                {
                    json.Prop("disabled", true);
                }
                else
                {
                    json.Prop("priority", pawn.workSettings.GetPriority(workType));
                }
                json.EndObject();
            }
            json.EndArray();
            if (pawn.timetable != null)
            {
                json.Key("schedule_by_hour").BeginArray();
                foreach (TimeAssignmentDef assignment in pawn.timetable.times)
                {
                    json.Value(assignment.defName);
                }
                json.EndArray();
            }
            json.EndObject();
        }
    }
}
