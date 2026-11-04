using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    internal static class WorldEndpoints
    {
        private const int MaxEventText = 800;

        private const int MaxQuestDescription = 1500;

        /// <summary>
        /// Recent letters and messages. The game already keeps this history in its archive, and
        /// saves it, so events from before the MCP server started are available too.
        /// </summary>
        public static void Events(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            int limit = query.GetInt("limit", 30, 1, 200);
            int now = Find.TickManager.TicksGame;

            List<IArchivable> recent = Find.Archive.ArchivablesListForReading
                .OrderByDescending(a => a.CreatedTicksGame)
                .Take(limit)
                .ToList();

            json.BeginObject();
            json.Key("events").BeginArray();
            foreach (IArchivable item in recent)
            {
                json.BeginObject();
                json.Prop("type", item is Letter ? "letter" : item is Message ? "message" : "dialog");
                json.Prop("label", GameAccess.Clean(item.ArchivedLabel));
                json.Prop("days_ago", (now - item.CreatedTicksGame) / (double)GameAccess.TicksPerDay);
                string text = GameAccess.Clean(EventText(item));
                if (!text.NullOrEmpty())
                {
                    json.Prop("text", GameAccess.Truncate(text, MaxEventText));
                }
                json.EndObject();
            }
            json.EndArray();
            json.EndObject();
        }

        private static string EventText(IArchivable item)
        {
            switch (item)
            {
                case ChoiceLetter choice:
                    return choice.Text;
                case ArchivedDialog dialog:
                    return dialog.text;
                default:
                    return null;
            }
        }

        public static void World(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            json.BeginObject();
            WriteFactions(json);
            WriteQuests(json);
            WriteCaravans(json);
            WritePlayerSettlements(json);
            WriteIdeology(json);
            json.EndObject();
        }

        private static void WriteFactions(JsonWriter json)
        {
            json.Key("factions").BeginArray();
            foreach (Faction faction in Find.FactionManager.AllFactionsVisibleInViewOrder.Where(f => !f.IsPlayer))
            {
                json.BeginObject();
                json.Prop("name", faction.Name);
                json.Prop("type", faction.def.label);
                json.Prop("goodwill", faction.PlayerGoodwill);
                json.Prop("relation", faction.PlayerRelationKind.ToString());
                json.Prop("tech_level", faction.def.techLevel.ToString());
                json.Prop("leader", faction.leader?.LabelShort);
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WriteQuests(JsonWriter json)
        {
            IEnumerable<Quest> quests = Find.QuestManager.QuestsListForReading
                .Where(q => !q.hidden && !q.hiddenInUI && !q.Historical);

            json.Key("quests").BeginArray();
            foreach (Quest quest in quests)
            {
                json.BeginObject();
                json.Prop("name", GameAccess.Clean(quest.name));
                json.Prop("state", quest.State.ToString());
                json.Prop("challenge_rating", quest.challengeRating);
                if (quest.TicksUntilExpiry > 0)
                {
                    json.Prop("days_until_expiry", quest.TicksUntilExpiry / (double)GameAccess.TicksPerDay);
                }
                json.Prop("description", GameAccess.Truncate(GameAccess.Clean(quest.description), MaxQuestDescription));
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WriteCaravans(JsonWriter json)
        {
            json.Key("caravans").BeginArray();
            foreach (Caravan caravan in Find.WorldObjects.Caravans.Where(c => c.IsPlayerControlled))
            {
                json.BeginObject();
                json.Prop("label", GameAccess.Clean(caravan.Label));
                json.Prop("tile", caravan.Tile.tileId);
                json.Prop("moving", caravan.pather.Moving);
                json.Prop("days_of_food", caravan.DaysWorthOfFood.days);
                json.Key("pawns").BeginArray();
                foreach (Pawn pawn in caravan.PawnsListForReading)
                {
                    json.Value(pawn.LabelShort);
                }
                json.EndArray();
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WritePlayerSettlements(JsonWriter json)
        {
            json.Key("player_maps").BeginArray();
            foreach (Map map in Find.Maps)
            {
                json.BeginObject();
                json.Prop("id", map.uniqueID);
                json.Prop("label", GameAccess.Clean(map.Parent?.LabelCap));
                json.Prop("is_home", map.IsPlayerHome);
                json.Prop("is_current", map == Find.CurrentMap);
                json.Prop("biome", map.Biome.label);
                json.Prop("colonists", map.mapPawns.FreeColonistsSpawnedCount);
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WriteIdeology(JsonWriter json)
        {
            if (!ModsConfig.IdeologyActive)
            {
                return;
            }
            Ideo ideo = Faction.OfPlayer.ideos?.PrimaryIdeo;
            if (ideo == null)
            {
                return;
            }
            json.Key("ideoligion").BeginObject();
            json.Prop("name", ideo.name);
            json.Key("precepts").BeginArray();
            foreach (Precept precept in ideo.PreceptsListForReading.Where(p => p.def.visible))
            {
                json.Value(GameAccess.Clean(precept.TipLabel));
            }
            json.EndArray();
            json.EndObject();
        }
    }
}
