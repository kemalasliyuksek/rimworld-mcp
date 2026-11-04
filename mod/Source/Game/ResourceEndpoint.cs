using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    internal static class ResourceEndpoint
    {
        private const int MaxEquipmentPerGroup = 60;

        public static void Resources(QueryParams query, JsonWriter json)
        {
            Map map = GameAccess.ResolveMap(query);

            json.BeginObject();
            json.Prop("silver", map.resourceCounter.Silver);
            json.Prop("wealth_total", map.wealthWatcher.WealthTotal);
            json.Prop("wealth_items", map.wealthWatcher.WealthItems);
            json.Prop("wealth_buildings", map.wealthWatcher.WealthBuildings);
            json.Prop("wealth_pawns", map.wealthWatcher.WealthPawns);

            json.Key("stock").BeginArray();
            foreach (var category in map.resourceCounter.AllCountedAmounts
                .Where(pair => pair.Value > 0)
                .GroupBy(pair => pair.Key.FirstThingCategory?.LabelCap.ToString() ?? "Other")
                .OrderBy(group => group.Key))
            {
                json.BeginObject();
                json.Prop("category", category.Key);
                json.Key("items").BeginArray();
                foreach (KeyValuePair<ThingDef, int> item in category.OrderByDescending(pair => pair.Value))
                {
                    json.BeginObject();
                    json.Prop("label", item.Key.label);
                    json.Prop("def", item.Key.defName);
                    json.Prop("count", item.Value);
                    json.EndObject();
                }
                json.EndArray();
                json.EndObject();
            }
            json.EndArray();

            // The resource counter leaves these out because each one has its own quality and condition.
            WriteEquipment(json, "weapons", map, ThingRequestGroup.Weapon);
            WriteEquipment(json, "apparel", map, ThingRequestGroup.Apparel);
            json.EndObject();
        }

        private static void WriteEquipment(JsonWriter json, string key, Map map, ThingRequestGroup group)
        {
            List<Thing> items = map.listerThings.ThingsInGroup(group)
                .Where(t => GameAccess.IsVisible(t) && t.IsInAnyStorage())
                .ToList();

            json.Key(key).BeginObject();
            json.Prop("total_in_storage", items.Count);
            json.Key("items").BeginArray();
            foreach (var entry in items.GroupBy(t => t.LabelCapNoCount).OrderByDescending(g => g.Count()).Take(MaxEquipmentPerGroup))
            {
                json.BeginObject();
                json.Prop("label", GameAccess.Clean(entry.Key));
                json.Prop("count", entry.Count());
                json.EndObject();
            }
            json.EndArray();
            json.EndObject();
        }
    }
}
