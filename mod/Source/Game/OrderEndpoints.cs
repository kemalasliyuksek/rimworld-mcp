using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>
    /// Orders for pawns, on the player's request. The menus and buttons here are the game's own, so what
    /// mods add to them comes along with no code for those mods. Nothing here touches the mouse or the
    /// player's selection. A failure for one pawn is reported for that pawn and does not stop the others.
    /// </summary>
    internal static class OrderEndpoints
    {
        private const int MaxDescription = 120;

        private static List<Pawn> ResolvePawns(QueryParams query)
        {
            List<string> names = query.GetList("pawns");
            if (names.Count == 0)
            {
                throw new BadRequestException("Pass 'pawns', a comma-separated list of ids or names.");
            }
            return names.Select(GameAccess.FindPawn).Distinct().ToList();
        }

        private static IntVec3 ResolveCell(QueryParams query, Map map)
        {
            return new IntVec3(query.RequireInt("x", 0, map.Size.x - 1), 0, query.RequireInt("z", 0, map.Size.z - 1));
        }

        private static string WhyCannotBeDrafted(Pawn pawn)
        {
            if (pawn.drafter == null || !pawn.drafter.ShowDraftGizmo || !pawn.CanTakeOrder)
            {
                return "cannot be drafted";
            }
            if (pawn.Downed)
            {
                return "is downed";
            }
            if (pawn.Deathresting)
            {
                return "is in deathrest";
            }
            if (ModsConfig.BiotechActive && pawn.IsColonyMech)
            {
                AcceptanceReport report = MechanitorUtility.CanDraftMech(pawn);
                if (!report.Accepted)
                {
                    return report.Reason ?? "cannot be drafted";
                }
            }
            return null;
        }

        private static void WriteResult(JsonWriter json, Pawn pawn, Action<JsonWriter> details)
        {
            json.BeginObject();
            json.Prop("pawn", pawn.LabelShort);
            try
            {
                details(json);
            }
            catch (BridgeException ex)
            {
                json.Prop("error", ex.Message);
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimWorld MCP] An order for {pawn.LabelShort} failed: {ex}");
                json.Prop("error", ex.GetType().Name + ": " + ex.Message);
            }
            json.EndObject();
        }

        public static void SetDrafted(QueryParams query, JsonWriter json)
        {
            List<Pawn> pawns = ResolvePawns(query);
            bool? drafted = ParseBool(query, "drafted");
            bool? fireAtWill = ParseBool(query, "fire_at_will");
            if (drafted == null && fireAtWill == null)
            {
                throw new BadRequestException("Pass 'drafted', 'fire_at_will' or both.");
            }

            json.BeginObject();
            json.Key("results").BeginArray();
            foreach (Pawn pawn in pawns)
            {
                WriteResult(json, pawn, _ =>
                {
                    string problem = WhyCannotBeDrafted(pawn);
                    if (problem != null)
                    {
                        throw new BadRequestException($"{pawn.LabelShort} {problem}.");
                    }
                    if (drafted != null)
                    {
                        pawn.drafter.Drafted = drafted.Value;
                    }
                    if (fireAtWill != null)
                    {
                        pawn.drafter.FireAtWill = fireAtWill.Value;
                    }
                    json.Prop("drafted", pawn.drafter.Drafted);
                    json.Prop("fire_at_will", pawn.drafter.FireAtWill);
                });
            }
            json.EndArray();
            json.EndObject();
        }

        private static bool? ParseBool(QueryParams query, string name)
        {
            switch (query.GetString(name))
            {
                case null:
                    return null;
                case "true":
                    return true;
                case "false":
                    return false;
                default:
                    throw new BadRequestException($"Parameter '{name}' must be true or false.");
            }
        }

        /// <summary>The steps behind the game's "go here" order. Each pawn claims a cell, so a group spreads out.</summary>
        public static void Move(QueryParams query, JsonWriter json)
        {
            List<Pawn> pawns = ResolvePawns(query);
            json.BeginObject();
            json.Key("results").BeginArray();
            foreach (Pawn pawn in pawns)
            {
                WriteResult(json, pawn, _ =>
                {
                    if (!pawn.Spawned)
                    {
                        throw new BadRequestException($"{pawn.LabelShort} is not on a map.");
                    }
                    string problem = WhyCannotBeDrafted(pawn);
                    if (problem != null)
                    {
                        throw new BadRequestException($"{pawn.LabelShort} {problem}.");
                    }
                    IntVec3 target = ResolveCell(query, pawn.Map);
                    IntVec3 standable = CellFinder.StandableCellNear(target, pawn.Map, 2.9f);
                    if (!standable.IsValid)
                    {
                        throw new BadRequestException("Nobody can stand near that cell.");
                    }

                    AcceptanceReport reach = FloatMenuOptionProvider_DraftedMove.PawnCanGoto(pawn, standable);
                    if (!reach.Accepted)
                    {
                        throw new BadRequestException(GameAccess.Clean(reach.Reason));
                    }
                    pawn.drafter.Drafted = true;
                    IntVec3 destination = RCellFinder.BestOrderedGotoDestNear(standable, pawn);
                    FloatMenuOptionProvider_DraftedMove.PawnGotoAction(target, pawn, destination);
                    json.Prop("x", destination.x);
                    json.Prop("z", destination.z);
                    json.Prop("drafted", true);
                });
            }
            json.EndArray();
            json.EndObject();
        }

        // The game builds this menu for the map on screen, so a pawn elsewhere has none to offer.
        private static List<FloatMenuOption> OptionsFor(Pawn pawn, IntVec3 cell)
        {
            if (!pawn.Spawned || pawn.Map != Find.CurrentMap)
            {
                throw new BridgeException(409, "pawn_not_on_current_map", $"{pawn.LabelShort} is not on the map the player is looking at, and the game only offers these orders there.");
            }
            return FloatMenuMakerMap.GetOptions(new List<Pawn> { pawn }, cell.ToVector3Shifted(), out FloatMenuContext _);
        }

        public static void ListOrders(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            Pawn pawn = GameAccess.FindPawn(query.RequireString("pawn"));
            Map map = Find.CurrentMap;
            IntVec3 cell = ResolveCell(query, map);

            json.BeginObject();
            json.Prop("pawn", pawn.LabelShort);
            json.Key("orders").BeginArray();
            PlayerInputGuard.Run(pawn, () =>
            {
                foreach (FloatMenuOption option in OptionsFor(pawn, cell))
                {
                    json.BeginObject();
                    json.Prop("order", GameAccess.Clean(option.Label));
                    if (option.Disabled)
                    {
                        json.Prop("disabled", true);
                    }
                    json.EndObject();
                }
            });
            json.EndArray();
            json.EndObject();
        }

        public static void GiveOrder(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            List<Pawn> pawns = ResolvePawns(query);
            IntVec3 cell = ResolveCell(query, Find.CurrentMap);
            string order = query.RequireString("order");

            json.BeginObject();
            json.Key("results").BeginArray();
            foreach (Pawn pawn in pawns)
            {
                WriteResult(json, pawn, _ =>
                {
                    WriteOutcome(json, PlayerInputGuard.Run(pawn, () =>
                    {
                        List<FloatMenuOption> options = OptionsFor(pawn, cell);
                        FloatMenuOption option = options.FirstOrDefault(o => string.Equals(GameAccess.Clean(o.Label), order, StringComparison.OrdinalIgnoreCase))
                            ?? throw new NotFoundException($"{pawn.LabelShort} has no such order there. Orders: {string.Join(" | ", options.Select(o => GameAccess.Clean(o.Label)))}.");
                        if (option.Disabled)
                        {
                            throw new BadRequestException("The game does not allow this order right now.");
                        }
                        option.Chosen(colonistOrdering: false, floatMenu: null);
                    }));
                });
            }
            json.EndArray();
            json.EndObject();
        }

        private static void WriteOutcome(JsonWriter json, PlayerInputGuard.Outcome outcome)
        {
            json.Prop("done", outcome.NeedsPlayer == null);
            if (outcome.NeedsPlayer != null)
            {
                json.Prop("needs_player", outcome.NeedsPlayer);
            }
        }

        private static Thing FindThing(string id)
        {
            GameAccess.RequireGame();
            foreach (Map map in Find.Maps)
            {
                Thing thing = map.listerThings.AllThings.FirstOrDefault(t => t.ThingID == id);
                if (thing != null && GameAccess.IsVisible(thing))
                {
                    return thing;
                }
            }
            return GameAccess.FindPawn(id);
        }

        // A class that overrides ProcessInput may rely on steps that calling its action directly would skip.
        private static string KindOf(Command command)
        {
            Type declaring = command.GetType().GetMethod(nameof(Command.ProcessInput))?.DeclaringType;
            if (command is Command_Toggle toggle && toggle.toggleAction != null && declaring == typeof(Command_Toggle))
            {
                return "toggle";
            }
            if (command is Command_Action button && button.action != null && declaring == typeof(Command_Action))
            {
                return "button";
            }
            if (command is Command_Target target && target.action != null && declaring == typeof(Command_Target))
            {
                return "target";
            }
            return null;
        }

        // One broken gizmo from a mod ends the game's iterator, so the ones before it are still kept.
        private static List<Command> CommandsOf(Thing thing)
        {
            var commands = new List<Command>();
            try
            {
                foreach (Gizmo gizmo in thing.GetGizmos())
                {
                    if (gizmo is Command command && command.Visible)
                    {
                        commands.Add(command);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimWorld MCP] Reading the buttons of {thing.LabelShort} stopped early: {ex}");
            }
            return commands;
        }

        public static void ListGizmos(QueryParams query, JsonWriter json)
        {
            Thing thing = FindThing(query.RequireString("thing"));

            json.BeginObject();
            json.Prop("thing", GameAccess.Clean(thing.LabelShort));
            json.Key("buttons").BeginArray();
            PlayerInputGuard.Run(thing, () =>
            {
                foreach (Command command in CommandsOf(thing))
                {
                    json.BeginObject();
                    json.Prop("button", GameAccess.Clean(command.LabelCap));
                    json.Prop("description", GameAccess.Truncate(GameAccess.Clean(command.Desc), MaxDescription));
                    string kind = KindOf(command);
                    if (kind != null)
                    {
                        json.Prop("kind", kind);
                    }
                    else
                    {
                        json.Prop("usable", false);
                    }
                    if (command is Command_Toggle toggle && toggle.isActive != null)
                    {
                        json.Prop("on", toggle.isActive());
                    }
                    if (command.Disabled)
                    {
                        json.Prop("disabled", GameAccess.Clean(command.disabledReason) ?? "");
                    }
                    json.EndObject();
                }
            });
            json.EndArray();
            json.EndObject();
        }

        public static void UseGizmo(QueryParams query, JsonWriter json)
        {
            Thing thing = FindThing(query.RequireString("thing"));
            string name = query.RequireString("button");

            // Inside the guard, because buttons can look at the selection while being built as well.
            PlayerInputGuard.Outcome outcome = PlayerInputGuard.Run(thing, () =>
            {
                List<Command> commands = CommandsOf(thing);
                Command command = commands.FirstOrDefault(c => string.Equals(GameAccess.Clean(c.LabelCap), name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new NotFoundException($"{GameAccess.Clean(thing.LabelShort)} has no button named '{name}'. Buttons: {string.Join(" | ", commands.Select(c => GameAccess.Clean(c.LabelCap)))}.");
                if (command.Disabled)
                {
                    throw new BadRequestException($"That button is disabled: {GameAccess.Clean(command.disabledReason)}");
                }

                switch (KindOf(command))
                {
                    case "toggle":
                        ((Command_Toggle)command).toggleAction();
                        break;
                    case "button":
                        ((Command_Action)command).action();
                        break;
                    case "target":
                        ((Command_Target)command).action(ResolveTarget(query, thing, (Command_Target)command));
                        break;
                    default:
                        throw new BadRequestException("That button only works by hand, so the player has to press it.");
                }
            });

            json.BeginObject();
            json.Prop("thing", GameAccess.Clean(thing.LabelShort));
            json.Prop("button", name);
            WriteOutcome(json, outcome);
            json.EndObject();
        }

        private static LocalTargetInfo ResolveTarget(QueryParams query, Thing thing, Command_Target command)
        {
            string targetId = query.GetString("target");
            Map map = thing.MapHeld ?? Find.CurrentMap;
            LocalTargetInfo target = targetId != null ? new LocalTargetInfo(FindThing(targetId)) : new LocalTargetInfo(ResolveCell(query, map));
            if (command.targetingParams != null && !command.targetingParams.CanTarget(target.ToTargetInfo(map)))
            {
                throw new BadRequestException("That button cannot target that.");
            }
            return target;
        }
    }
}
