using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>Policies, stockpiles, trade and caravans, on the player's request.</summary>
    internal static class ManagementEndpoints
    {
        public static void ListPolicies(QueryParams query, JsonWriter json)
        {
            Map map = GameAccess.ResolveMap(query);
            Verse.Game game = Current.Game;

            json.BeginObject();
            WriteLabels(json, "areas", map.areaManager.AllAreas.Where(a => a.AssignableAsAllowed()).Select(a => a.Label));
            WriteLabels(json, "outfits", game.outfitDatabase.AllOutfits.Select(p => p.label));
            WriteLabels(json, "drug_policies", game.drugPolicyDatabase.AllPolicies.Select(p => p.label));
            WriteLabels(json, "food_policies", game.foodRestrictionDatabase.AllFoodRestrictions.Select(p => p.label));
            WriteLabels(json, "hostility_responses", Enum.GetNames(typeof(HostilityResponseMode)));
            WriteLabels(json, "medical_care", Enum.GetNames(typeof(MedicalCareCategory)));
            json.EndObject();
        }

        private static void WriteLabels(JsonWriter json, string key, IEnumerable<string> labels)
        {
            json.Key(key).BeginArray();
            foreach (string label in labels)
            {
                json.Value(GameAccess.Clean(label));
            }
            json.EndArray();
        }

        private static T ResolveEnum<T>(string text, string what)
            where T : struct
        {
            if (Enum.TryParse(text, ignoreCase: true, out T value) && Enum.IsDefined(typeof(T), value))
            {
                return value;
            }
            throw new BadRequestException($"Unknown {what} '{text}'. Use: {string.Join(", ", Enum.GetNames(typeof(T)))}.");
        }

        private static T ResolveLabeled<T>(string text, IEnumerable<T> items, Func<T, string> label, string what)
            where T : class
        {
            List<T> list = items.ToList();
            return list.FirstOrDefault(item => string.Equals(label(item), text, StringComparison.OrdinalIgnoreCase))
                ?? throw new NotFoundException($"No {what} named '{text}'. Use: {string.Join(", ", list.Select(label))}.");
        }

        public static void SetPawnPolicies(QueryParams query, JsonWriter json)
        {
            Pawn pawn = GameAccess.FindPawn(query.RequireString("pawn"));
            if (pawn.playerSettings == null)
            {
                throw new BadRequestException($"'{pawn.LabelShort}' has no such settings.");
            }
            Verse.Game game = Current.Game;

            // Everything is checked first, so that a typo cannot leave half the changes applied.
            string areaName = query.GetString("area");
            Area area = null;
            if (areaName != null && areaName != "none")
            {
                if (pawn.MapHeld == null)
                {
                    throw new BadRequestException($"{pawn.LabelShort} is not on a map.");
                }
                area = ResolveLabeled(areaName, pawn.MapHeld.areaManager.AllAreas.Where(a => a.AssignableAsAllowed()), a => a.Label, "area");
            }
            string outfitName = query.GetString("outfit");
            ApparelPolicy outfit = outfitName == null ? null : ResolveLabeled(outfitName, game.outfitDatabase.AllOutfits, p => p.label, "outfit");
            string drugName = query.GetString("drug_policy");
            DrugPolicy drug = drugName == null ? null : ResolveLabeled(drugName, game.drugPolicyDatabase.AllPolicies, p => p.label, "drug policy");
            string foodName = query.GetString("food_policy");
            FoodPolicy food = foodName == null ? null : ResolveLabeled(foodName, game.foodRestrictionDatabase.AllFoodRestrictions, p => p.label, "food policy");
            string hostilityName = query.GetString("hostility_response");
            HostilityResponseMode? hostility = hostilityName == null ? (HostilityResponseMode?)null : ResolveEnum<HostilityResponseMode>(hostilityName, "hostility response");
            string medicalName = query.GetString("medical_care");
            MedicalCareCategory? medical = medicalName == null ? (MedicalCareCategory?)null : ResolveEnum<MedicalCareCategory>(medicalName, "medical care");

            if (outfit != null && pawn.outfits == null || drug != null && pawn.drugs == null || food != null && pawn.foodRestriction == null)
            {
                throw new BadRequestException($"'{pawn.LabelShort}' cannot have that policy.");
            }

            if (areaName != null)
            {
                pawn.playerSettings.AreaRestrictionInPawnCurrentMap = area;
            }
            if (outfit != null)
            {
                pawn.outfits.CurrentApparelPolicy = outfit;
            }
            if (drug != null)
            {
                pawn.drugs.CurrentPolicy = drug;
            }
            if (food != null)
            {
                pawn.foodRestriction.CurrentFoodPolicy = food;
            }
            if (hostility != null)
            {
                pawn.playerSettings.hostilityResponse = hostility.Value;
            }
            if (medical != null)
            {
                pawn.playerSettings.medCare = medical.Value;
            }

            json.BeginObject();
            json.Prop("pawn", pawn.LabelShort);
            json.Prop("area", GameAccess.Clean(pawn.playerSettings.AreaRestrictionInPawnCurrentMap?.Label));
            json.Prop("outfit", GameAccess.Clean(pawn.outfits?.CurrentApparelPolicy?.label));
            json.Prop("drug_policy", GameAccess.Clean(pawn.drugs?.CurrentPolicy?.label));
            json.Prop("food_policy", GameAccess.Clean(pawn.foodRestriction?.CurrentFoodPolicy?.label));
            json.Prop("hostility_response", pawn.playerSettings.hostilityResponse.ToString());
            json.Prop("medical_care", pawn.playerSettings.medCare.ToString());
            json.EndObject();
        }

        public static void SetStockpile(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            Map map = Find.CurrentMap;
            var cell = new IntVec3(query.RequireInt("x", 0, map.Size.x - 1), 0, query.RequireInt("z", 0, map.Size.z - 1));
            if (!(map.zoneManager.ZoneAt(cell) is Zone_Stockpile zone))
            {
                throw new BadRequestException("That cell is not in a stockpile.");
            }

            string priorityName = query.GetString("priority");
            StoragePriority? priority = priorityName == null ? (StoragePriority?)null : ResolveEnum<StoragePriority>(priorityName, "priority");
            string preset = query.GetString("preset");
            if (preset != null && preset != "all" && preset != "none")
            {
                throw new BadRequestException("Parameter 'preset' must be all or none.");
            }
            List<Action<ThingFilter>> changes = query.GetList("allow").Select(n => FilterChange(n, true))
                .Concat(query.GetList("disallow").Select(n => FilterChange(n, false)))
                .ToList();

            ThingFilter filter = zone.settings.filter;
            if (preset == "all")
            {
                filter.SetAllowAll(zone.GetParentStoreSettings()?.filter);
            }
            else if (preset == "none")
            {
                filter.SetDisallowAll();
            }
            changes.ForEach(change => change(filter));
            if (priority != null)
            {
                zone.settings.Priority = priority.Value;
            }

            json.BeginObject();
            json.Prop("zone", GameAccess.Clean(zone.label));
            json.Prop("priority", zone.settings.Priority.ToString());
            json.Prop("allowed_def_count", filter.AllowedDefCount);
            json.EndObject();
        }

        private static Action<ThingFilter> FilterChange(string name, bool allow)
        {
            ThingCategoryDef category = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(name);
            if (category != null)
            {
                return filter => filter.SetAllow(category, allow);
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(name)
                ?? throw new NotFoundException($"No thing or thing category named '{name}'. Find names with search_defs.");
            return filter => filter.SetAllow(def, allow);
        }

        private static IEnumerable<ITrader> TradersOn(Map map)
        {
            foreach (Pawn pawn in GameAccess.VisibleSpawnedPawns(map).Where(p => p.CanTradeNow))
            {
                yield return pawn;
            }
            foreach (TradeShip ship in map.passingShipManager.passingShips.OfType<TradeShip>().Where(s => s.CanTradeNow))
            {
                yield return ship;
            }
        }

        private static string TraderId(ITrader trader)
        {
            return trader is Pawn pawn ? pawn.ThingID : trader.TraderName;
        }

        public static void ListTraders(QueryParams query, JsonWriter json)
        {
            Map map = GameAccess.ResolveMap(query);

            json.BeginObject();
            json.Key("traders").BeginArray();
            foreach (ITrader trader in TradersOn(map))
            {
                json.BeginObject();
                json.Prop("id", TraderId(trader));
                json.Prop("name", GameAccess.Clean(trader.TraderName));
                json.Prop("kind", GameAccess.Clean(trader.TraderKind?.label));
                json.Prop("faction", trader.Faction?.Name);
                json.Prop("orbital", trader is TradeShip);
                json.EndObject();
            }
            json.EndArray();
            json.EndObject();
        }

        // The game's trade code reads this global session, so it only exists for the length of one call.
        private static void WithTrade(QueryParams query, Action<ITrader, Pawn> action)
        {
            GameAccess.RequireGame();
            if (TradeSession.Active || Find.WindowStack.IsOpen<Dialog_Trade>())
            {
                throw new BridgeException(409, "trade_open", "The player already has a trade open.");
            }

            Map map = Find.CurrentMap;
            string id = query.RequireString("trader");
            ITrader trader = TradersOn(map).FirstOrDefault(t => TraderId(t) == id)
                ?? throw new NotFoundException($"No trader '{id}' on this map. Find ids with list_traders.");
            Pawn negotiator = GameAccess.FindPawn(query.RequireString("negotiator"));
            if (!negotiator.IsColonist || negotiator.MapHeld != map || negotiator.skills?.GetSkill(SkillDefOf.Social)?.TotallyDisabled != false)
            {
                throw new BadRequestException($"{negotiator.LabelShort} cannot trade here.");
            }
            AcceptanceReport allowed = negotiator.CanTradeWith(trader.Faction, trader.TraderKind);
            if (!allowed.Accepted)
            {
                throw new BadRequestException(GameAccess.Clean(allowed.Reason) ?? $"{negotiator.LabelShort} cannot trade with them.");
            }

            TradeSession.SetupWith(trader, negotiator, giftMode: false);
            try
            {
                action(trader, negotiator);
            }
            finally
            {
                TradeSession.Close();
            }
        }

        public static void TraderGoods(QueryParams query, JsonWriter json)
        {
            WithTrade(query, (trader, negotiator) =>
            {
                json.BeginObject();
                json.Prop("colony_silver", TradeSession.deal.CurrencyTradeable?.CountHeldBy(Transactor.Colony) ?? 0);
                json.Prop("trader_silver", TradeSession.deal.CurrencyTradeable?.CountHeldBy(Transactor.Trader) ?? 0);
                json.Key("goods").BeginArray();
                foreach (Tradeable tradeable in TradeSession.deal.AllTradeables.Where(t => !t.IsCurrency && t.HasAnyThing && t.Interactive))
                {
                    json.BeginObject();
                    json.Prop("def", tradeable.ThingDef?.defName);
                    json.Prop("label", GameAccess.Clean(tradeable.Label));
                    json.Prop("trader_has", tradeable.CountHeldBy(Transactor.Trader));
                    json.Prop("colony_has", tradeable.CountHeldBy(Transactor.Colony));
                    if (tradeable.CountHeldBy(Transactor.Trader) > 0)
                    {
                        json.Prop("buy_price", tradeable.GetPriceFor(TradeAction.PlayerBuys));
                    }
                    if (tradeable.CountHeldBy(Transactor.Colony) > 0 && tradeable.TraderWillTrade)
                    {
                        json.Prop("sell_price", tradeable.GetPriceFor(TradeAction.PlayerSells));
                    }
                    json.EndObject();
                }
                json.EndArray();
                json.EndObject();
            });
        }

        public static void Trade(QueryParams query, JsonWriter json)
        {
            List<(string Name, string Value)> items = query.GetPairs("deal");
            if (items.Count == 0)
            {
                throw new BadRequestException("Pass 'deal', such as Steel:50,WoodLog:-20, where a positive count buys and a negative one sells.");
            }

            WithTrade(query, (trader, negotiator) =>
            {
                TradeDeal deal = TradeSession.deal;
                foreach ((string name, string value) in items)
                {
                    if (!int.TryParse(value, out int count) || count == 0)
                    {
                        throw new BadRequestException($"The count for '{name}' must be a whole number, positive to buy and negative to sell.");
                    }
                    Apply(deal, name, count);
                }

                // The game checks these in its trade window, and TryExecute expects that window to be there.
                deal.UpdateCurrencyCount();
                if (deal.CurrencyTradeable == null || deal.CurrencyTradeable.CountPostDealFor(Transactor.Colony) < 0)
                {
                    throw new BadRequestException("The colony cannot afford this.");
                }
                if (!deal.DoesTraderHaveEnoughSilver())
                {
                    throw new BadRequestException("The trader cannot afford this.");
                }

                bool executed = deal.TryExecute(out bool traded);
                json.BeginObject();
                json.Prop("done", executed && traded);
                json.Prop("colony_silver_after", deal.CurrencyTradeable.CountHeldBy(Transactor.Colony));
                json.EndObject();
            });
        }

        // The same def can appear several times, such as for each quality, so a count can span entries.
        private static void Apply(TradeDeal deal, string name, int count)
        {
            int left = Math.Abs(count);
            foreach (Tradeable tradeable in deal.AllTradeables.Where(t => !t.IsCurrency && t.HasAnyThing && t.Interactive && t.ThingDef?.defName == name))
            {
                int limit = count > 0 ? tradeable.GetMaximumToTransfer() : -tradeable.GetMinimumToTransfer();
                int amount = Math.Min(left, Math.Max(0, limit - Math.Abs(tradeable.CountToTransfer)));
                if (amount > 0)
                {
                    tradeable.AdjustTo(tradeable.CountToTransfer + (count > 0 ? amount : -amount));
                    left -= amount;
                }
            }
            if (left > 0)
            {
                throw new BadRequestException($"Only {Math.Abs(count) - left} of '{name}' can change hands here.");
            }
        }

        public static void FormCaravan(QueryParams query, JsonWriter json)
        {
            List<Pawn> pawns = query.GetList("pawns").Select(GameAccess.FindPawn).Distinct().ToList();
            if (pawns.Count == 0)
            {
                throw new BadRequestException("Pass 'pawns', a comma-separated list of ids or names.");
            }
            Map map = pawns[0].MapHeld;
            if (map == null || pawns.Any(p => p.MapHeld != map || !p.Spawned || p.Downed || !p.IsColonist))
            {
                throw new BadRequestException("Every pawn must be a colonist on the same map who is able to walk.");
            }

            PlanetTile destination = PlanetTile.Invalid;
            if (query.GetString("tile") != null)
            {
                destination = Find.WorldGrid[query.RequireInt("tile", 0, Find.WorldGrid.TilesCount - 1)].tile;
                if (!Find.WorldReachability.CanReach(map.Tile, destination))
                {
                    throw new BadRequestException("A caravan cannot reach that tile from here.");
                }
            }

            var transferables = new List<TransferableOneWay>();
            foreach ((string name, string value) in query.GetPairs("items"))
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(name)
                    ?? throw new NotFoundException($"No thing named '{name}'. Find names with search_defs.");
                List<Thing> available = map.listerThings.ThingsOfDef(def).Where(t => GameAccess.IsVisible(t) && !t.IsForbidden(Faction.OfPlayer)).ToList();
                if (!int.TryParse(value, out int count) || count < 1 || available.Sum(t => t.stackCount) < count)
                {
                    throw new BadRequestException($"The colony has {available.Sum(t => t.stackCount)} of '{name}' available, and that count cannot be packed.");
                }
                var transferable = new TransferableOneWay();
                transferable.things.AddRange(available);
                transferable.AdjustTo(count);
                transferables.Add(transferable);
            }

            IntVec3 middle = pawns.Aggregate(IntVec3.Zero, (sum, p) => sum + p.Position) / pawns.Count;
            if (!RCellFinder.TryFindClosestEdgeCellTo(middle, map, out IntVec3 exitSpot))
            {
                throw new BadRequestException("No way off the map was found.");
            }
            if (!RCellFinder.TryFindRandomSpotJustOutsideColony(pawns[0], out IntVec3 meetingPoint))
            {
                throw new BadRequestException("No place to pack was found.");
            }

            CaravanFormingUtility.StartFormingCaravan(pawns, new List<Pawn>(), Faction.OfPlayer, transferables, meetingPoint, exitSpot, map.Tile, destination);

            json.BeginObject();
            json.Prop("forming", true);
            json.Prop("pawns", pawns.Count);
            json.Prop("item_kinds", transferables.Count);
            json.EndObject();
        }

        public static void SendCaravan(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            string name = query.RequireString("caravan");
            Caravan caravan = Find.WorldObjects.Caravans.FirstOrDefault(c => c.IsPlayerControlled && GameAccess.Clean(c.Label) == name)
                ?? throw new NotFoundException($"No caravan named '{name}'. Find names with get_world.");
            PlanetTile tile = Find.WorldGrid[query.RequireInt("tile", 0, Find.WorldGrid.TilesCount - 1)].tile;
            if (!Find.WorldReachability.CanReach(caravan.Tile, tile))
            {
                throw new BadRequestException("That caravan cannot reach that tile.");
            }
            bool started = caravan.pather.StartPath(tile, null, repathImmediately: true);

            json.BeginObject();
            json.Prop("caravan", name);
            json.Prop("moving", started);
            json.Prop("tile", tile.tileId);
            json.EndObject();
        }
    }
}
