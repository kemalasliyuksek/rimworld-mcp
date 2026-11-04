using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorldMcp.Bridge.Core;
using UnityEngine;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>
    /// Everything on the game's Architect menu, on the player's request: building, mining, cutting, zones
    /// and the rest, including what mods add. It uses the game's own designators, so the game's checks and
    /// mods that patch them still apply. Plans are written directly, because the plan designator works
    /// through the player's selection.
    /// </summary>
    internal static class ArchitectEndpoints
    {
        private const int MaxRejectionsListed = 10;

        private static readonly FieldInfo PlacingRotField = typeof(Designator_Place).GetField("placingRot", BindingFlags.Instance | BindingFlags.NonPublic);

        private static IEnumerable<Designator> DesignatorsIn(DesignationCategoryDef category)
        {
            foreach (Designator designator in category.ResolvedAllowedDesignators)
            {
                if (designator is Designator_Dropdown dropdown)
                {
                    foreach (Designator element in dropdown.Elements.Where(e => e.Visible))
                    {
                        yield return element;
                    }
                }
                else if (designator.Visible)
                {
                    yield return designator;
                }
            }
        }

        private static string IdOf(Designator designator)
        {
            return designator is Designator_Build build ? build.PlacingDef.defName : designator.GetType().Name;
        }

        public static void List(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            string categoryName = query.GetString("category");

            json.BeginObject();
            if (categoryName == null)
            {
                json.Key("categories").BeginArray();
                foreach (DesignationCategoryDef category in DefDatabase<DesignationCategoryDef>.AllDefsListForReading.OrderBy(c => c.order))
                {
                    json.BeginObject();
                    json.Prop("category", category.defName);
                    json.Prop("label", category.label);
                    json.Prop("count", DesignatorsIn(category).Count());
                    json.EndObject();
                }
                json.EndArray();
            }
            else
            {
                DesignationCategoryDef category = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(categoryName)
                    ?? throw new NotFoundException($"No category named '{categoryName}'. Call this without a category for the list.");
                json.Key("designators").BeginArray();
                foreach (Designator designator in DesignatorsIn(category))
                {
                    WriteDesignator(json, designator);
                }
                json.EndArray();
            }
            json.EndObject();
        }

        private static void WriteDesignator(JsonWriter json, Designator designator)
        {
            json.BeginObject();
            json.Prop("id", IdOf(designator));
            json.Prop("label", GameAccess.Clean(designator.LabelCap));
            if (designator is Designator_Build build)
            {
                BuildableDef def = build.PlacingDef;
                json.Prop("size", $"{def.Size.x}x{def.Size.z}");
                if (def.MadeFromStuff)
                {
                    json.Prop("stuff_needed", def.CostStuffCount);
                }
                if (def.CostList != null && def.CostList.Count > 0)
                {
                    json.Prop("cost", string.Join(", ", def.CostList.Select(c => $"{c.thingDef.defName}:{c.count}")));
                }
            }
            json.EndObject();
        }

        private static Designator FindDesignator(string id)
        {
            return DefDatabase<DesignationCategoryDef>.AllDefsListForReading
                .SelectMany(DesignatorsIn)
                .FirstOrDefault(d => IdOf(d) == id)
                ?? throw new NotFoundException($"No available designator '{id}'. It may need research. Find ids with list_designators.");
        }

        // A new one each time, so that the player's own build menu keeps its chosen material and rotation.
        private static Designator_Build PrepareBuild(Designator_Build original, QueryParams query)
        {
            BuildableDef def = original.PlacingDef;
            var build = new Designator_Build(def) { sourcePrecept = original.sourcePrecept };

            string stuffName = query.GetString("stuff");
            if (stuffName != null)
            {
                ThingDef stuff = DefDatabase<ThingDef>.GetNamedSilentFail(stuffName);
                if (stuff == null || !def.MadeFromStuff || !stuff.IsStuff || !stuff.stuffProps.CanMake(def))
                {
                    throw new BadRequestException($"'{def.defName}' cannot be made from '{stuffName}'.");
                }
                build.SetStuffDef(stuff);
            }

            string rotationName = query.GetString("rotation");
            if (rotationName != null)
            {
                if (PlacingRotField == null)
                {
                    throw new InvalidOperationException("The game's build tool has changed. This version of the game may need an update of the mod.");
                }
                PlacingRotField.SetValue(build, ParseRotation(rotationName));
            }
            return build;
        }

        private static Rot4 ParseRotation(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "north":
                    return Rot4.North;
                case "east":
                    return Rot4.East;
                case "south":
                    return Rot4.South;
                case "west":
                    return Rot4.West;
                default:
                    throw new BadRequestException("Parameter 'rotation' must be North, East, South or West.");
            }
        }

        public static void Use(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            Map map = Find.CurrentMap;
            Designator designator = FindDesignator(query.RequireString("designator"));
            List<(int X, int Z)> cells = CellSpec.Parse(query.RequireString("cells"));

            if (designator is Designator_Build build)
            {
                designator = PrepareBuild(build, query);
            }
            else if (query.GetString("stuff") != null || query.GetString("rotation") != null)
            {
                throw new BadRequestException("Only buildings take 'stuff' and 'rotation'.");
            }

            int done = 0;
            var rejections = new List<(int X, int Z, string Reason)>();
            PlayerInputGuard.Outcome outcome = PlayerInputGuard.Run(null, () =>
            {
                var accepted = new List<IntVec3>();
                foreach ((int x, int z) in cells)
                {
                    var cell = new IntVec3(x, 0, z);
                    AcceptanceReport report = cell.InBounds(map) ? designator.CanDesignateCell(cell) : "outside the map";
                    if (report.Accepted)
                    {
                        accepted.Add(cell);
                    }
                    else
                    {
                        rejections.Add((x, z, GameAccess.Clean(report.Reason)));
                    }
                }

                // Zones and the like have to see all their cells at once. For the rest, each placed cell
                // can rule out the next, so each one is checked again right before it is placed.
                if (designator.GetType().GetMethod(nameof(Designator.DesignateMultiCell)).DeclaringType != typeof(Designator))
                {
                    if (accepted.Count > 0)
                    {
                        designator.DesignateMultiCell(accepted);
                        done = accepted.Count;
                    }
                    return;
                }
                foreach (IntVec3 cell in accepted)
                {
                    AcceptanceReport report = designator.CanDesignateCell(cell);
                    if (report.Accepted)
                    {
                        designator.DesignateSingleCell(cell);
                        done++;
                    }
                    else
                    {
                        rejections.Add((cell.x, cell.z, GameAccess.Clean(report.Reason)));
                    }
                }
                if (done > 0)
                {
                    designator.Finalize(true);
                }
            });

            json.BeginObject();
            json.Prop("designator", IdOf(designator));
            json.Prop("cells_done", done);
            json.Prop("cells_rejected", rejections.Count);
            json.Key("rejected").BeginArray();
            foreach ((int x, int z, string reason) in rejections.Take(MaxRejectionsListed))
            {
                json.BeginObject();
                json.Prop("x", x);
                json.Prop("z", z);
                json.Prop("reason", reason);
                json.EndObject();
            }
            json.EndArray();
            if (outcome.NeedsPlayer != null)
            {
                json.Prop("needs_player", outcome.NeedsPlayer);
            }
            json.EndObject();
        }

        public static void ListPlans(QueryParams query, JsonWriter json)
        {
            Map map = GameAccess.ResolveMap(query);

            json.BeginObject();
            json.Key("plans").BeginArray();
            foreach (Plan plan in map.planManager.AllPlans)
            {
                WritePlan(json, plan);
            }
            json.EndArray();
            json.Key("colors").BeginArray();
            foreach (ColorDef color in Designator_Plan_Add.Colors)
            {
                json.Value(color.defName);
            }
            json.EndArray();
            json.EndObject();
        }

        private static void WritePlan(JsonWriter json, Plan plan)
        {
            json.BeginObject();
            json.Prop("id", plan.GetUniqueLoadID());
            json.Prop("label", GameAccess.Clean(plan.RenamableLabel));
            json.Prop("color", plan.Color?.defName);
            json.Prop("cells", plan.CellCount);
            json.Prop("hidden", plan.Hidden);
            if (plan.CellCount > 0)
            {
                json.Prop("min_x", plan.Cells.Min(c => c.x));
                json.Prop("min_z", plan.Cells.Min(c => c.z));
                json.Prop("max_x", plan.Cells.Max(c => c.x));
                json.Prop("max_z", plan.Cells.Max(c => c.z));
            }
            json.EndObject();
        }

        public static void DrawPlan(QueryParams query, JsonWriter json)
        {
            Map map = GameAccess.ResolveMap(query);
            List<(int X, int Z)> cells = CellSpec.Parse(query.RequireString("cells"));

            string colorName = query.GetString("color");
            ColorDef color = colorName == null
                ? Designator_Plan_Add.Colors.FirstOrDefault()
                : Designator_Plan_Add.Colors.FirstOrDefault(c => c.defName == colorName)
                    ?? throw new NotFoundException($"No plan color '{colorName}'. Use: {string.Join(", ", Designator_Plan_Add.Colors.Select(c => c.defName))}.");

            List<IntVec3> valid = cells.Select(c => new IntVec3(c.X, 0, c.Z)).Where(c => c.InBounds(map) && !c.InNoBuildEdgeArea(map)).ToList();
            if (valid.Count == 0)
            {
                throw new BadRequestException("None of those cells can hold a plan. Plans cannot go outside the map or right next to its edge.");
            }

            var plan = new Plan(color, map.planManager);
            string label = query.GetString("label");
            if (label != null)
            {
                plan.label = label;
            }
            foreach (IntVec3 cell in valid)
            {
                // A cell belongs to one plan, so the older plan gives it up as the game's own tool does.
                map.planManager.PlanAt(cell)?.RemoveCell(cell);
                plan.AddCell(cell);
            }

            json.BeginObject();
            json.Key("plan");
            WritePlan(json, plan);
            json.Prop("cells_skipped", cells.Count - valid.Count);
            json.EndObject();
        }

        public static void RemovePlan(QueryParams query, JsonWriter json)
        {
            Map map = GameAccess.ResolveMap(query);
            string id = query.RequireString("plan");
            List<Plan> plans = id == "all"
                ? map.planManager.AllPlans.ToList()
                : map.planManager.AllPlans.Where(p => p.GetUniqueLoadID() == id).ToList();
            if (plans.Count == 0 && id != "all")
            {
                throw new NotFoundException($"No plan with the id '{id}'. Find ids with list_plans.");
            }

            PlayerInputGuard.Run(null, () =>
            {
                foreach (Plan plan in plans)
                {
                    plan.Delete(playSound: false);
                }
            });

            json.BeginObject();
            json.Prop("removed", plans.Count);
            json.EndObject();
        }
    }
}
