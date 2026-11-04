using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    internal static class ResearchEndpoint
    {
        private const int MaxDescription = 240;

        public static void Research(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            int limit = query.GetInt("limit", 60, 1, 500);

            List<ResearchProjectDef> projects = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            ResearchProjectDef current = Find.ResearchManager.GetProject();

            json.BeginObject();
            json.Key("current");
            if (current == null)
            {
                json.Null();
            }
            else
            {
                WriteProject(json, current);
            }

            json.Prop("finished_count", projects.Count(p => p.IsFinished));
            json.Prop("total_count", projects.Count);

            List<ResearchProjectDef> available = projects
                .Where(p => !p.IsFinished && p.CanStartNow && p != current)
                .OrderBy(p => p.CostApparent)
                .ToList();

            json.Prop("available_total", available.Count);
            json.Key("available").BeginArray();
            foreach (ResearchProjectDef project in available.Take(limit))
            {
                WriteProject(json, project);
            }
            json.EndArray();
            json.EndObject();
        }

        private static void WriteProject(JsonWriter json, ResearchProjectDef project)
        {
            json.BeginObject();
            json.Prop("def", project.defName);
            json.Prop("label", GameAccess.Clean(project.LabelCap));
            json.Prop("tab", project.tab?.label);
            json.Prop("tech_level", project.techLevel.ToString());
            json.Prop("cost", project.CostApparent);
            json.Prop("progress_pct", GameAccess.Percent(project.ProgressPercent));
            json.Prop("description", GameAccess.Truncate(GameAccess.Clean(project.description), MaxDescription));
            json.EndObject();
        }
    }
}
