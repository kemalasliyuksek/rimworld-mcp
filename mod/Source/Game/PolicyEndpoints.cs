using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>Creating and changing policies, allowed areas and bill details, on the player's request.</summary>
    internal static class PolicyEndpoints
    {
        private static IEnumerable<Policy> PoliciesOf(string kind)
        {
            Verse.Game game = Current.Game;
            switch (kind)
            {
                case "outfit":
                    return game.outfitDatabase.AllOutfits;
                case "drug":
                    return game.drugPolicyDatabase.AllPolicies;
                case "food":
                    return game.foodRestrictionDatabase.AllFoodRestrictions;
                default:
                    throw new BadRequestException("Parameter 'kind' must be outfit, drug or food.");
            }
        }

        private static Policy ResolvePolicy(string kind, string name)
        {
            List<Policy> policies = PoliciesOf(kind).ToList();
            return policies.FirstOrDefault(p => string.Equals(p.label, name, StringComparison.OrdinalIgnoreCase))
                ?? throw new NotFoundException($"No {kind} policy named '{name}'. Use: {string.Join(", ", policies.Select(p => p.label))}.");
        }

        private static void WritePolicy(JsonWriter json, Policy policy)
        {
            json.Key("policy").BeginObject();
            json.Prop("name", GameAccess.Clean(policy.label));
            ThingFilter filter = FilterOf(policy);
            if (filter != null)
            {
                json.Prop("allowed_def_count", filter.AllowedDefCount);
            }
            json.EndObject();
        }

        private static ThingFilter FilterOf(Policy policy)
        {
            switch (policy)
            {
                case ApparelPolicy apparel:
                    return apparel.filter;
                case FoodPolicy food:
                    return food.filter;
                default:
                    return null;
            }
        }

        public static void CreatePolicy(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            string kind = query.RequireString("kind");
            string name = query.RequireString("name");
            if (PoliciesOf(kind).Any(p => string.Equals(p.label, name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new BadRequestException($"That kind of policy already has one named '{name}'.");
            }
            string copyName = query.GetString("copy_from");
            Policy source = copyName == null ? null : ResolvePolicy(kind, copyName);

            Verse.Game game = Current.Game;
            Policy policy;
            switch (kind)
            {
                case "outfit":
                    policy = game.outfitDatabase.MakeNewOutfit();
                    break;
                case "drug":
                    policy = game.drugPolicyDatabase.MakeNewDrugPolicy();
                    break;
                default:
                    policy = game.foodRestrictionDatabase.MakeNewFoodRestriction();
                    break;
            }
            if (source != null)
            {
                policy.CopyFrom(source);
            }
            policy.label = name;

            json.BeginObject();
            WritePolicy(json, policy);
            json.EndObject();
        }

        public static void EditPolicy(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            string kind = query.RequireString("kind");
            Policy policy = ResolvePolicy(kind, query.RequireString("policy"));
            string rename = query.GetString("name");

            ThingFilter filter = FilterOf(policy);
            string preset = query.GetString("preset");
            if (preset != null && preset != "all" && preset != "none")
            {
                throw new BadRequestException("Parameter 'preset' must be all or none.");
            }
            List<Action<ThingFilter>> changes = query.GetList("allow").Select(n => ManagementEndpoints.FilterChange(n, true))
                .Concat(query.GetList("disallow").Select(n => ManagementEndpoints.FilterChange(n, false)))
                .ToList();
            if (filter == null && (preset != null || changes.Count > 0))
            {
                throw new BadRequestException("A drug policy has no filter. Change it with 'drug' and its settings.");
            }
            Action drugChange = ReadDrugChange(query, policy);

            if (preset == "all")
            {
                filter.SetAllowAll(null);
            }
            else if (preset == "none")
            {
                filter.SetDisallowAll();
            }
            changes.ForEach(change => change(filter));
            drugChange?.Invoke();
            if (rename != null)
            {
                policy.label = rename;
            }

            json.BeginObject();
            WritePolicy(json, policy);
            json.EndObject();
        }

        // Everything is read before anything is set, so a mistake cannot leave half the changes applied.
        private static Action ReadDrugChange(QueryParams query, Policy policy)
        {
            string drugName = query.GetString("drug");
            if (drugName == null)
            {
                return null;
            }
            if (!(policy is DrugPolicy drugPolicy))
            {
                throw new BadRequestException("Only a drug policy has drug settings.");
            }
            ThingDef drug = DefDatabase<ThingDef>.GetNamedSilentFail(drugName) ?? throw new NotFoundException($"No thing named '{drugName}'.");
            DrugPolicyEntry entry = Enumerable.Range(0, drugPolicy.Count).Select(i => drugPolicy[i]).FirstOrDefault(e => e.drug == drug)
                ?? throw new NotFoundException($"That policy has no setting for '{drugName}'.");

            bool? addiction = Flag(query, "for_addiction");
            bool? joy = Flag(query, "for_joy");
            bool? scheduled = Flag(query, "scheduled");
            float? frequency = Number(query, "days_frequency", 0.01f, 1000f);
            float? mood = Number(query, "only_if_mood_below_pct", 0f, 100f);
            float? joyBelow = Number(query, "only_if_joy_below_pct", 0f, 100f);
            float? carry = Number(query, "take_to_inventory", 0f, 1000f);

            return () =>
            {
                entry.allowedForAddiction = addiction ?? entry.allowedForAddiction;
                entry.allowedForJoy = joy ?? entry.allowedForJoy;
                entry.allowScheduled = scheduled ?? entry.allowScheduled;
                entry.daysFrequency = frequency ?? entry.daysFrequency;
                entry.onlyIfMoodBelow = mood / 100f ?? entry.onlyIfMoodBelow;
                entry.onlyIfJoyBelow = joyBelow / 100f ?? entry.onlyIfJoyBelow;
                if (carry != null)
                {
                    entry.takeToInventory = (int)carry.Value;
                    entry.takeToInventoryTempBuffer = entry.takeToInventory.ToString();
                }
            };
        }

        private static bool? Flag(QueryParams query, string name)
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

        private static float? Number(QueryParams query, string name, float min, float max)
        {
            string text = query.GetString(name);
            if (text == null)
            {
                return null;
            }
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || value < min || value > max)
            {
                throw new BadRequestException($"Parameter '{name}' must be a number from {min} to {max}.");
            }
            return value;
        }

        public static void DeletePolicy(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            string kind = query.RequireString("kind");
            Policy policy = ResolvePolicy(kind, query.RequireString("policy"));
            Verse.Game game = Current.Game;

            AcceptanceReport report;
            switch (policy)
            {
                case ApparelPolicy apparel:
                    report = game.outfitDatabase.TryDelete(apparel);
                    break;
                case DrugPolicy drug:
                    report = game.drugPolicyDatabase.TryDelete(drug);
                    break;
                default:
                    report = game.foodRestrictionDatabase.TryDelete((FoodPolicy)policy);
                    break;
            }
            if (!report.Accepted)
            {
                throw new BadRequestException(GameAccess.Clean(report.Reason) ?? "The game does not allow deleting this policy.");
            }

            json.BeginObject();
            json.Prop("deleted", GameAccess.Clean(policy.label));
            json.EndObject();
        }

        public static void SetArea(QueryParams query, JsonWriter json)
        {
            Map map = GameAccess.ResolveMap(query);
            string name = query.RequireString("area");
            Area area = map.areaManager.AllAreas.FirstOrDefault(a => a.Mutable && string.Equals(a.Label, name, StringComparison.OrdinalIgnoreCase));
            if (query.GetString("delete") == "true")
            {
                if (area == null)
                {
                    throw new NotFoundException($"No editable area named '{name}'.");
                }
                area.Delete();
                json.BeginObject();
                json.Prop("deleted", name);
                json.EndObject();
                return;
            }

            List<(int X, int Z)> add = query.GetString("add") == null ? new List<(int X, int Z)>() : CellSpec.Parse(query.GetString("add"));
            List<(int X, int Z)> remove = query.GetString("remove") == null ? new List<(int X, int Z)>() : CellSpec.Parse(query.GetString("remove"));
            if (area == null)
            {
                if (!map.areaManager.TryMakeNewAllowed(out Area_Allowed created))
                {
                    throw new BadRequestException("The game does not allow more areas.");
                }
                created.RenamableLabel = name;
                area = created;
            }
            string rename = query.GetString("rename");
            if (rename != null)
            {
                area.RenamableLabel = rename;
            }
            foreach ((int x, int z) in add)
            {
                var cell = new IntVec3(x, 0, z);
                if (cell.InBounds(map))
                {
                    area[cell] = true;
                }
            }
            foreach ((int x, int z) in remove)
            {
                var cell = new IntVec3(x, 0, z);
                if (cell.InBounds(map))
                {
                    area[cell] = false;
                }
            }

            json.BeginObject();
            json.Prop("area", GameAccess.Clean(area.Label));
            json.Prop("cells", area.TrueCount);
            json.EndObject();
        }

        public static void EditBill(QueryParams query, JsonWriter json)
        {
            (Thing bench, BillStack bills) = ColonyEndpoints.ResolveBench(query);
            if (bills.Count == 0)
            {
                throw new BadRequestException($"{GameAccess.Clean(bench.LabelShort)} has no bills.");
            }
            Bill bill = bills.Bills[query.RequireInt("index", 0, bills.Count - 1)];
            var production = bill as Bill_Production;

            // Read all of it first, so that a mistake cannot leave the bill half changed.
            bool? suspended = Flag(query, "suspended");
            int? skillMin = query.GetString("skill_min") == null ? (int?)null : query.GetInt("skill_min", 0, 0, 20);
            int? skillMax = query.GetString("skill_max") == null ? (int?)null : query.GetInt("skill_max", 20, 0, 20);
            float? radius = Number(query, "radius", 3f, 999f);
            int? offset = query.GetString("move") == null ? (int?)null : query.GetInt("move", 0, -bills.Count, bills.Count);
            List<Action<ThingFilter>> changes = query.GetList("allow_ingredients").Select(n => ManagementEndpoints.FilterChange(n, true))
                .Concat(query.GetList("disallow_ingredients").Select(n => ManagementEndpoints.FilterChange(n, false)))
                .ToList();

            string pawnName = query.GetString("worker");
            Pawn worker = pawnName == null || pawnName == "any" ? null : GameAccess.FindPawn(pawnName);

            bool? pauseWhenSatisfied = Flag(query, "pause_when_satisfied");
            int? unpauseAt = query.GetString("unpause_at") == null ? (int?)null : query.GetInt("unpause_at", 0, 0, 100000);
            string minQuality = query.GetString("min_quality");
            string maxQuality = query.GetString("max_quality");
            QualityCategory? low = minQuality == null ? (QualityCategory?)null : ManagementEndpoints.ResolveEnum<QualityCategory>(minQuality, "quality");
            QualityCategory? high = maxQuality == null ? (QualityCategory?)null : ManagementEndpoints.ResolveEnum<QualityCategory>(maxQuality, "quality");

            string store = query.GetString("store");
            BillStoreModeDef storeMode = null;
            ISlotGroup storeGroup = null;
            if (store != null)
            {
                switch (store)
                {
                    case "best":
                        storeMode = BillStoreModeDefOf.BestStockpile;
                        break;
                    case "floor":
                        storeMode = BillStoreModeDefOf.DropOnFloor;
                        break;
                    default:
                        Zone_Stockpile zone = bench.Map.zoneManager.AllZones.OfType<Zone_Stockpile>().FirstOrDefault(z => string.Equals(z.label, store, StringComparison.OrdinalIgnoreCase))
                            ?? throw new NotFoundException($"No stockpile named '{store}'. Use best, floor or a stockpile's name.");
                        storeMode = BillStoreModeDefOf.SpecificStockpile;
                        storeGroup = zone.slotGroup;
                        break;
                }
            }
            if (production == null && (pauseWhenSatisfied != null || unpauseAt != null || low != null || high != null || store != null || query.GetString("repeat") != null))
            {
                throw new BadRequestException("Those settings only apply to a production bill.");
            }

            if (query.GetString("repeat") != null)
            {
                ColonyEndpoints.ApplyRepeat(production, query);
            }
            bill.suspended = suspended ?? bill.suspended;
            bill.allowedSkillRange = new IntRange(skillMin ?? bill.allowedSkillRange.min, skillMax ?? bill.allowedSkillRange.max);
            bill.ingredientSearchRadius = radius ?? bill.ingredientSearchRadius;
            changes.ForEach(change => change(bill.ingredientFilter));
            if (pawnName == "any")
            {
                bill.SetAnyPawnRestriction();
            }
            else if (worker != null)
            {
                bill.SetPawnRestriction(worker);
            }
            if (production != null)
            {
                production.pauseWhenSatisfied = pauseWhenSatisfied ?? production.pauseWhenSatisfied;
                production.unpauseWhenYouHave = unpauseAt ?? production.unpauseWhenYouHave;
                production.qualityRange = new QualityRange(low ?? production.qualityRange.min, high ?? production.qualityRange.max);
                if (storeMode != null)
                {
                    production.SetStoreMode(storeMode, storeGroup);
                }
            }
            if (offset != null)
            {
                bills.Reorder(bill, offset.Value);
            }

            BaseEndpoint.WriteBill(json, bill);
        }
    }
}
