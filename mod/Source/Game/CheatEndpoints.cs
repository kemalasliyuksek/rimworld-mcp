using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>
    /// Things beyond what the game normally allows, for a player who asks. Everything created is an
    /// ordinary game thing, so a save stays fine after the mod is removed.
    /// </summary>
    internal static class CheatEndpoints
    {
        private const int MaxCount = 100000;

        private const int MaxStacks = 500;

        private static IntVec3 ResolveCell(QueryParams query, Map map)
        {
            return new IntVec3(query.RequireInt("x", 0, map.Size.x - 1), 0, query.RequireInt("z", 0, map.Size.z - 1));
        }

        public static void SpawnThing(QueryParams query, JsonWriter json)
        {
            Map map = GameAccess.ResolveMap(query);
            IntVec3 cell = ResolveCell(query, map);
            string name = query.RequireString("def");
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(name)
                ?? throw new NotFoundException($"No thing named '{name}'. Find names with search_defs and type ThingDef.");
            if (def.category != ThingCategory.Item && def.category != ThingCategory.Building && def.category != ThingCategory.Plant)
            {
                throw new BadRequestException($"'{name}' cannot be spawned this way. Use spawn_pawn for pawns.");
            }

            int count = query.GetInt("count", 1, 1, MaxCount);
            int stackLimit = Math.Max(1, def.stackLimit);
            if ((count + stackLimit - 1) / stackLimit > MaxStacks)
            {
                throw new BadRequestException($"That is more than {MaxStacks} stacks at once.");
            }
            ThingDef stuff = ResolveStuff(def, query.GetString("stuff"));
            QualityCategory? quality = ResolveQuality(query.GetString("quality"));

            int placed = 0;
            while (placed < count)
            {
                Thing thing = ThingMaker.MakeThing(def, stuff);
                thing.stackCount = Math.Min(stackLimit, count - placed);
                if (quality != null)
                {
                    thing.TryGetComp<CompQuality>()?.SetQuality(quality.Value, ArtGenerationContext.Colony);
                }

                int amount = thing.stackCount;
                if (def.category == ThingCategory.Item)
                {
                    if (!GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near))
                    {
                        break;
                    }
                }
                else
                {
                    if (def.CanHaveFaction)
                    {
                        thing.SetFaction(Faction.OfPlayer);
                    }
                    GenSpawn.Spawn(thing, cell, map);
                }
                placed += amount;
            }

            json.BeginObject();
            json.Prop("def", def.defName);
            json.Prop("spawned", placed);
            json.Prop("asked_for", count);
            json.EndObject();
        }

        private static ThingDef ResolveStuff(ThingDef def, string name)
        {
            if (name == null)
            {
                return def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
            }
            ThingDef stuff = DefDatabase<ThingDef>.GetNamedSilentFail(name);
            if (stuff == null || !def.MadeFromStuff || !stuff.IsStuff || !stuff.stuffProps.CanMake(def))
            {
                throw new BadRequestException($"'{def.defName}' cannot be made from '{name}'.");
            }
            return stuff;
        }

        private static QualityCategory? ResolveQuality(string name)
        {
            if (name == null)
            {
                return null;
            }
            if (Enum.TryParse(name, ignoreCase: true, out QualityCategory quality) && Enum.IsDefined(typeof(QualityCategory), quality))
            {
                return quality;
            }
            throw new BadRequestException($"Unknown quality '{name}'. Use: {string.Join(", ", Enum.GetNames(typeof(QualityCategory)))}.");
        }

        public static void SpawnPawn(QueryParams query, JsonWriter json)
        {
            Map map = GameAccess.ResolveMap(query);
            IntVec3 cell = ResolveCell(query, map);
            string name = query.RequireString("kind");
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(name)
                ?? throw new NotFoundException($"No pawn kind named '{name}'. Find names with search_defs and type PawnKindDef.");
            Faction faction = ResolveFaction(query.GetString("faction"));

            int count = query.GetInt("count", 1, 1, 100);
            var spawned = new List<Pawn>();
            for (int i = 0; i < count; i++)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(kind, faction);
                IntVec3 standable = CellFinder.StandableCellNear(cell, map, 8f);
                GenSpawn.Spawn(pawn, standable.IsValid ? standable : cell, map);
                spawned.Add(pawn);
            }

            json.BeginObject();
            json.Key("spawned").BeginArray();
            foreach (Pawn pawn in spawned)
            {
                json.BeginObject();
                json.Prop("id", pawn.ThingID);
                json.Prop("name", pawn.LabelShort);
                json.Prop("faction", pawn.Faction?.Name);
                json.EndObject();
            }
            json.EndArray();
            json.EndObject();
        }

        private static Faction ResolveFaction(string name)
        {
            switch (name)
            {
                case null:
                case "none":
                    return null;
                case "player":
                    return Faction.OfPlayer;
            }
            return Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.def.defName == name || f.Name == name)
                ?? throw new NotFoundException($"No faction '{name}'. Use player, none, or a faction from get_world.");
        }

        public static void TriggerIncident(QueryParams query, JsonWriter json)
        {
            Map map = GameAccess.ResolveMap(query);
            string name = query.RequireString("incident");
            IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail(name)
                ?? throw new NotFoundException($"No incident named '{name}'. Find names with search_defs and type IncidentDef.");

            IIncidentTarget target = incident.TargetAllowed(map) ? (IIncidentTarget)map : Find.World;
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, target);
            parms.forced = true;
            if (query.GetString("points") != null)
            {
                parms.points = query.GetInt("points", 0, 1, 1000000);
            }
            bool fired = incident.Worker.TryExecute(parms);

            json.BeginObject();
            json.Prop("incident", incident.defName);
            json.Prop("fired", fired);
            json.Prop("points", parms.points);
            json.EndObject();
        }

        public static void CompleteResearch(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            string name = query.RequireString("project");
            ResearchManager research = Find.ResearchManager;
            if (name == "all")
            {
                research.DebugSetAllProjectsFinished();
            }
            else
            {
                ResearchProjectDef project = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name)
                    ?? throw new NotFoundException($"No research project named '{name}'. Find names with get_research or search_defs.");
                research.FinishProject(project, doCompletionDialog: false, researcher: null, doCompletionLetter: false);
            }

            json.BeginObject();
            json.Prop("finished", name);
            json.EndObject();
        }

        public static void RevealMap(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireHiddenInformation();
            Map map = GameAccess.ResolveMap(query);
            map.fogGrid.ClearAllFog();

            json.BeginObject();
            json.Prop("map", map.uniqueID);
            json.Prop("revealed", true);
            json.EndObject();
        }
    }
}
