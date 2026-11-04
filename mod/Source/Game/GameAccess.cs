using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>
    /// Shared helpers for reading game state. The visibility checks here enforce the project's main
    /// rule: the bridge exposes what the player can see, never what the game hides from them.
    /// </summary>
    internal static class GameAccess
    {
        public const int TicksPerDay = 60000;

        public static void RequireGame()
        {
            if (Current.ProgramState != ProgramState.Playing || Current.Game == null)
            {
                throw new NoGameLoadedException();
            }
        }

        /// <summary>The map named by the "map" parameter, or the one the player is looking at.</summary>
        public static Map ResolveMap(QueryParams query)
        {
            RequireGame();
            string requested = query.GetString("map");
            if (requested == null)
            {
                return Find.CurrentMap ?? throw new BridgeException(409, "no_map", "The player has no map open.");
            }
            int id = query.GetInt("map", 0, 0, int.MaxValue);
            return Find.Maps.FirstOrDefault(m => m.uniqueID == id)
                ?? throw new NotFoundException($"No map with id {id}. Available: {string.Join(", ", Find.Maps.Select(m => m.uniqueID))}.");
        }

        /// <summary>Removes the game's rich-text markup, which is noise outside of its UI.</summary>
        public static string Clean(string text) => text.NullOrEmpty() ? text : text.StripTags();

        public static string Truncate(string text, int maxLength)
        {
            if (text == null || text.Length <= maxLength)
            {
                return text;
            }
            return text.Substring(0, maxLength) + "...";
        }

        public static bool IsVisible(Thing thing)
        {
            if (!thing.Spawned)
            {
                return true;
            }
            if (thing.Position.Fogged(thing.Map))
            {
                return false;
            }
            return !(thing is Pawn pawn) || !pawn.IsPsychologicallyInvisible();
        }

        public static IEnumerable<Pawn> VisibleSpawnedPawns(Map map)
        {
            return map.mapPawns.AllPawnsSpawned.Where(IsVisible);
        }

        public static void RequireCheats()
        {
            if (!McpMod.Settings.allowCheats)
            {
                throw new PermissionDisabledException("Cheats");
            }
        }

        public static void RequireHiddenInformation()
        {
            if (!McpMod.Settings.allowHiddenInformation)
            {
                throw new PermissionDisabledException("Hidden information");
            }
        }

        /// <summary>
        /// Items the player can see outside storage. The game's resource counter leaves these out, so
        /// without them a new colony's supplies, still lying where they landed, would look like nothing.
        /// </summary>
        public static IEnumerable<Thing> VisibleUnstoredItems(Map map)
        {
            return map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                .Where(t => t.def.category == ThingCategory.Item && IsVisible(t) && !t.IsInAnyStorage());
        }

        /// <summary>The same check as the game's social tab, which hides relatives the player has not met.</summary>
        public static bool RelationIsVisible(Pawn pawn, Pawn other)
        {
            if ((other.RaceProps.Animal && other.Dead && other.Corpse == null) || other.Name == null || other.Name.Numerical)
            {
                return false;
            }
            return !pawn.relations.hidePawnRelations && !other.relations.hidePawnRelations && other.relations.everSeenByPlayer;
        }

        /// <summary>
        /// Uses RelatedPawns and GetRelations rather than DirectRelations, because siblings, children
        /// and other implied relations are not stored directly.
        /// </summary>
        public static void WriteRelations(JsonWriter json, Pawn pawn)
        {
            if (pawn.relations == null)
            {
                return;
            }
            json.Key("relations").BeginArray();
            foreach (Pawn other in pawn.relations.RelatedPawns.ToList().Where(other => RelationIsVisible(pawn, other)))
            {
                json.BeginObject();
                json.Prop("relation", string.Join(", ", pawn.GetRelations(other).Select(def => def.GetGenderSpecificLabel(other))));
                json.Prop("pawn", other.LabelShort);
                if (other.Dead)
                {
                    json.Prop("dead", true);
                }
                if (pawn.needs?.mood != null && other.RaceProps.Humanlike)
                {
                    json.Prop("opinion", pawn.relations.OpinionOf(other));
                }
                json.EndObject();
            }
            json.EndArray();

            if (pawn.needs?.mood == null)
            {
                return;
            }
            json.Key("opinions").BeginArray();
            foreach (Pawn other in SocialCardUtility.PawnsForSocialInfo(pawn).ToList())
            {
                json.BeginObject();
                json.Prop("pawn", other.LabelShort);
                json.Prop("of_them", pawn.relations.OpinionOf(other));
                json.Prop("of_this_pawn", other.relations.OpinionOf(pawn));
                json.EndObject();
            }
            json.EndArray();
        }

        /// <summary>All the player's colonists, including those away in caravans or transporters.</summary>
        public static List<Pawn> AllColonists()
        {
            return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists;
        }

        public static Pawn FindPawn(string idOrName)
        {
            RequireGame();
            List<Pawn> pawns = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive
                .Where(p => IsVisible(p))
                .ToList();

            Pawn byId = pawns.FirstOrDefault(p => p.ThingID == idOrName);
            if (byId != null)
            {
                return byId;
            }

            List<Pawn> byName = pawns
                .Where(p => p.Name != null && (
                    string.Equals(p.Name.ToStringShort, idOrName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Name.ToStringFull, idOrName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Prefer the player's own pawn when a visitor or raider happens to share the name.
            Pawn colonist = byName.FirstOrDefault(p => p.Faction == Faction.OfPlayer);
            if (colonist != null)
            {
                return colonist;
            }
            if (byName.Count == 1)
            {
                return byName[0];
            }
            if (byName.Count > 1)
            {
                throw new BadRequestException($"Several pawns are named '{idOrName}'. Use one of these ids: {string.Join(", ", byName.Select(p => p.ThingID))}.");
            }
            throw new NotFoundException($"No visible pawn with id or name '{idOrName}'.");
        }

        public static void WritePosition(JsonWriter json, Thing thing)
        {
            if (thing.Spawned)
            {
                WriteCell(json, thing.Position);
                json.Prop("map", thing.Map.uniqueID);
            }
            else
            {
                json.Key("x").Null();
                json.Key("z").Null();
                Caravan caravan = (thing as Pawn)?.GetCaravan();
                if (caravan != null)
                {
                    json.Prop("caravan", caravan.Label);
                }
            }
        }

        public static void WriteCell(JsonWriter json, IntVec3 cell)
        {
            json.Prop("x", cell.x);
            json.Prop("z", cell.z);
        }

        public static double Percent(float fraction) => Math.Round(fraction * 100f, 1);

        public static string LocalDate(Map map)
        {
            long ticks = GenTicks.TicksAbs;
            return GenDate.DateFullStringWithHourAt(ticks, LongLatOf(map));
        }

        public static UnityEngine.Vector2 LongLatOf(Map map)
        {
            return map != null ? Find.WorldGrid.LongLatOf(map.Tile) : default;
        }
    }
}
