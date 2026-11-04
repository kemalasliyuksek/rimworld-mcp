using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>Psycasts and every other ability a pawn has, on the player's request, whichever mod adds them.</summary>
    internal static class AbilityEndpoints
    {
        private static Pawn ResolveCaster(QueryParams query)
        {
            Pawn pawn = GameAccess.FindPawn(query.RequireString("pawn"));
            if (pawn.abilities == null)
            {
                throw new BadRequestException($"{pawn.LabelShort} has no abilities.");
            }
            return pawn;
        }

        public static void List(QueryParams query, JsonWriter json)
        {
            Pawn pawn = ResolveCaster(query);

            json.BeginObject();
            json.Prop("pawn", pawn.LabelShort);
            if (pawn.psychicEntropy != null && pawn.HasPsylink)
            {
                json.Prop("psyfocus_pct", GameAccess.Percent(pawn.psychicEntropy.CurrentPsyfocus));
                json.Prop("neural_heat", pawn.psychicEntropy.EntropyValue);
                json.Prop("neural_heat_limit", pawn.psychicEntropy.MaxEntropy);
            }
            json.Key("abilities").BeginArray();
            foreach (Ability ability in pawn.abilities.AllAbilitiesForReading)
            {
                json.BeginObject();
                json.Prop("def", ability.def.defName);
                json.Prop("label", GameAccess.Clean(ability.def.LabelCap));
                json.Prop("needs_target", ability.def.targetRequired);
                if (ability.def.targetWorldCell)
                {
                    json.Prop("targets_world_tile", true);
                }
                if (ability.def.IsPsycast)
                {
                    json.Prop("psycast_level", ability.def.level);
                    json.Prop("psyfocus_cost", ability.def.PsyfocusCost);
                    json.Prop("neural_heat", ability.def.EntropyGain);
                }
                if (ability.UsesCharges)
                {
                    json.Prop("charges", ability.RemainingCharges);
                }
                if (ability.GizmoDisabled(out string reason))
                {
                    json.Prop("disabled", GameAccess.Clean(reason) ?? "");
                }
                json.EndObject();
            }
            json.EndArray();
            json.EndObject();
        }

        public static void Use(QueryParams query, JsonWriter json)
        {
            Pawn pawn = ResolveCaster(query);
            string name = query.RequireString("ability");
            Ability ability = pawn.abilities.AllAbilitiesForReading.FirstOrDefault(a => a.def.defName == name)
                ?? throw new NotFoundException($"{pawn.LabelShort} has no ability '{name}'. Find names with list_abilities.");
            if (ability.GizmoDisabled(out string reason))
            {
                throw new BadRequestException(GameAccess.Clean(reason) ?? "The game does not allow this ability right now.");
            }
            if (!ability.CanQueueCast)
            {
                throw new BadRequestException("The game does not allow this ability right now, or it is already queued.");
            }

            bool confirm = query.GetString("confirm") == "true";
            PlayerInputGuard.Outcome outcome;
            if (ability.def.targetWorldCell)
            {
                int tileId = query.RequireInt("tile", 0, Find.WorldGrid.TilesCount - 1);
                var target = new GlobalTargetInfo(Find.WorldGrid[tileId].tile);
                if (!ability.ValidateGlobalTarget(target))
                {
                    throw new BadRequestException("That ability cannot reach that tile.");
                }
                outcome = PlayerInputGuard.Run(pawn, () => ability.QueueCastingJob(target), confirm);
            }
            else
            {
                LocalTargetInfo target = ability.def.targetRequired ? ResolveTarget(query, pawn) : new LocalTargetInfo(pawn);
                if (ability.def.targetRequired && (!ability.verb.ValidateTarget(target, showMessages: false) || !ability.CanApplyOn(target)))
                {
                    throw new BadRequestException("That ability cannot target that.");
                }
                outcome = PlayerInputGuard.Run(pawn, () => ability.QueueCastingJob(target, LocalTargetInfo.Invalid), confirm);
            }

            json.BeginObject();
            json.Prop("pawn", pawn.LabelShort);
            json.Prop("ability", ability.def.defName);
            OrderEndpoints.WriteOutcome(json, outcome);
            json.EndObject();
        }

        private static LocalTargetInfo ResolveTarget(QueryParams query, Pawn pawn)
        {
            string id = query.GetString("target");
            if (id != null)
            {
                return new LocalTargetInfo(OrderEndpoints.FindThing(id));
            }
            Map map = pawn.MapHeld ?? throw new BadRequestException($"{pawn.LabelShort} is not on a map.");
            return new LocalTargetInfo(new IntVec3(query.RequireInt("x", 0, map.Size.x - 1), 0, query.RequireInt("z", 0, map.Size.z - 1)));
        }
    }
}
