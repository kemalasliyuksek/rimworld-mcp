using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>
    /// Changes work priorities and schedules during play, when the player asks. These are the settings
    /// on the game's own Work and Schedule tabs, with the same limits, and nothing here commands a pawn.
    /// </summary>
    internal static class WorkEndpoints
    {
        private const int MaxPriority = 4;

        private const int DefaultPriority = 3;

        // The same pawns as the game's tabs: not guests staying for a quest, whose work is not the player's.
        private static Pawn ResolveWorker(QueryParams query)
        {
            Pawn pawn = GameAccess.FindPawn(query.RequireString("pawn"));
            if (!(pawn.IsColonist || pawn.IsSlaveOfColony) || pawn.IsQuestLodger() || pawn.workSettings == null || !pawn.workSettings.EverWork)
            {
                throw new BadRequestException($"'{pawn.LabelShort}' does not take work from the player.");
            }
            return pawn;
        }

        public static void SetPriorities(QueryParams query, JsonWriter json)
        {
            Pawn pawn = ResolveWorker(query);
            string manualText = query.GetString("manual_priorities");
            if (manualText != null && manualText != "true" && manualText != "false")
            {
                throw new BadRequestException("Parameter 'manual_priorities' must be true or false.");
            }

            // Everything is checked first, so that a typo cannot leave half the changes applied.
            var changes = new List<(WorkTypeDef Work, int Priority)>();
            foreach ((string name, string value) in query.GetPairs("priorities"))
            {
                WorkTypeDef work = DefDatabase<WorkTypeDef>.AllDefsListForReading.FirstOrDefault(w => w.visible && w.defName == name)
                    ?? throw new NotFoundException($"No work type named '{name}'. Use: {string.Join(", ", DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(w => w.visible).Select(w => w.defName))}.");
                if (!int.TryParse(value, out int priority) || priority < 0 || priority > MaxPriority)
                {
                    throw new BadRequestException($"The priority for '{name}' must be from 0 to {MaxPriority}, where 1 is the highest and 0 is off.");
                }
                if (priority != 0 && pawn.WorkTypeIsDisabled(work))
                {
                    throw new BadRequestException($"{pawn.LabelShort} cannot do {name}.");
                }
                changes.Add((work, priority));
            }
            if (changes.Count == 0 && manualText == null)
            {
                throw new BadRequestException("Pass 'priorities', 'manual_priorities' or both.");
            }

            // Without manual priorities, the game treats every active job alike, so 1, 2 and 4 would do nothing.
            bool needsManual = changes.Any(c => c.Priority != 0 && c.Priority != DefaultPriority);
            if (manualText == "false" && needsManual)
            {
                throw new BadRequestException($"Priorities other than 0 and {DefaultPriority} need manual priorities to be on.");
            }
            if (manualText != null)
            {
                SetManualPriorities(manualText == "true");
            }
            else if (needsManual)
            {
                SetManualPriorities(true);
            }

            foreach ((WorkTypeDef work, int priority) in changes)
            {
                pawn.workSettings.SetPriority(work, priority);
            }

            json.BeginObject();
            json.Prop("pawn", pawn.LabelShort);
            PawnEndpoints.WriteWork(json, pawn);
            json.EndObject();
        }

        // The same steps as the checkbox on the game's Work tab.
        private static void SetManualPriorities(bool enabled)
        {
            PlaySettings settings = Find.PlaySettings;
            if (settings.useWorkPriorities == enabled)
            {
                return;
            }
            settings.useWorkPriorities = enabled;
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive)
            {
                if (pawn.Faction == Faction.OfPlayer && pawn.workSettings != null)
                {
                    pawn.workSettings.Notify_UseWorkPrioritiesChanged();
                }
            }
        }

        public static void SetSchedule(QueryParams query, JsonWriter json)
        {
            Pawn pawn = ResolveWorker(query);
            if (pawn.timetable == null)
            {
                throw new BadRequestException($"'{pawn.LabelShort}' has no schedule.");
            }

            var changes = new List<(int Hour, TimeAssignmentDef Assignment)>();
            foreach ((string name, string range) in query.GetPairs("hours"))
            {
                TimeAssignmentDef assignment = DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(name)
                    ?? throw new NotFoundException($"No schedule activity named '{name}'. Use: {string.Join(", ", DefDatabase<TimeAssignmentDef>.AllDefsListForReading.Select(a => a.defName))}.");
                changes.AddRange(HourRange.Parse(range).Select(hour => (hour, assignment)));
            }
            if (changes.Count == 0)
            {
                throw new BadRequestException("Pass 'hours', such as Sleep:22-6,Joy:20-22.");
            }

            foreach ((int hour, TimeAssignmentDef assignment) in changes)
            {
                pawn.timetable.SetAssignment(hour, assignment);
            }

            json.BeginObject();
            json.Prop("pawn", pawn.LabelShort);
            PawnEndpoints.WriteWork(json, pawn);
            json.EndObject();
        }
    }
}
