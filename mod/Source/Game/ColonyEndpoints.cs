using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>Running the colony on the player's request: game speed, research, bills, crops, dialogs and letters.</summary>
    internal static class ColonyEndpoints
    {
        private const int MaxText = 1500;

        // Options keep this protected, because the game only runs it from the dialog's own button.
        private static readonly MethodInfo ActivateOption = typeof(DiaOption).GetMethod("Activate", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void SetSpeed(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            TickManager ticks = Find.TickManager;
            switch (query.RequireString("speed"))
            {
                case "paused":
                    ticks.Pause();
                    break;
                case "normal":
                    ticks.CurTimeSpeed = TimeSpeed.Normal;
                    break;
                case "fast":
                    ticks.CurTimeSpeed = TimeSpeed.Fast;
                    break;
                case "superfast":
                    ticks.CurTimeSpeed = TimeSpeed.Superfast;
                    break;
                default:
                    throw new BadRequestException("Parameter 'speed' must be paused, normal, fast or superfast.");
            }

            json.BeginObject();
            json.Prop("speed", ticks.CurTimeSpeed.ToString());
            json.Prop("paused", ticks.Paused);
            json.EndObject();
        }

        public static void SetResearch(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            string name = query.RequireString("project");
            ResearchManager research = Find.ResearchManager;

            if (name == "none")
            {
                ResearchProjectDef current = research.GetProject();
                if (current != null)
                {
                    research.StopProject(current);
                }
            }
            else
            {
                ResearchProjectDef project = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name)
                    ?? throw new NotFoundException($"No research project named '{name}'. Find names with get_research.");
                if (project.baseCost <= 0f || !project.CanStartNow)
                {
                    throw new BadRequestException($"'{name}' cannot be started now. It may be finished, or need other research, a certain bench or a techprint.");
                }
                research.SetCurrentProject(project);
            }

            json.BeginObject();
            json.Prop("current", research.GetProject()?.defName);
            json.EndObject();
        }

        private static (Thing Thing, BillStack Bills) ResolveBench(QueryParams query)
        {
            Thing thing = OrderEndpoints.FindThing(query.RequireString("thing"));
            if (!(thing is IBillGiver giver) || giver.BillStack == null || thing is Pawn)
            {
                throw new BadRequestException($"{GameAccess.Clean(thing.LabelShort)} does not take bills.");
            }
            return (thing, giver.BillStack);
        }

        public static void ListRecipes(QueryParams query, JsonWriter json)
        {
            (Thing bench, BillStack _) = ResolveBench(query);

            json.BeginObject();
            json.Key("recipes").BeginArray();
            foreach (RecipeDef recipe in bench.def.AllRecipes.Where(r => r.AvailableNow))
            {
                json.BeginObject();
                json.Prop("recipe", recipe.defName);
                json.Prop("label", GameAccess.Clean(recipe.LabelCap));
                json.EndObject();
            }
            json.EndArray();
            json.EndObject();
        }

        public static void AddBill(QueryParams query, JsonWriter json)
        {
            (Thing bench, BillStack bills) = ResolveBench(query);
            string name = query.RequireString("recipe");
            RecipeDef recipe = bench.def.AllRecipes.FirstOrDefault(r => r.defName == name && r.AvailableNow)
                ?? throw new NotFoundException($"{GameAccess.Clean(bench.LabelShort)} has no available recipe '{name}'. Find names with list_recipes.");

            Bill bill = recipe.MakeNewBill();
            if (bill is Bill_Production production)
            {
                ApplyRepeat(production, query);
            }
            bills.AddBill(bill);

            json.BeginObject();
            json.Prop("thing", GameAccess.Clean(bench.LabelShort));
            json.Prop("bill", GameAccess.Clean(bill.LabelCap));
            json.Prop("bills", bills.Count);
            json.EndObject();
        }

        private static void ApplyRepeat(Bill_Production bill, QueryParams query)
        {
            string mode = query.GetString("repeat", "count");
            int count = query.GetInt("count", 1, 1, 100000);
            switch (mode)
            {
                case "count":
                    bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                    bill.repeatCount = count;
                    break;
                case "forever":
                    bill.repeatMode = BillRepeatModeDefOf.Forever;
                    break;
                case "until":
                    bill.repeatMode = BillRepeatModeDefOf.TargetCount;
                    bill.targetCount = count;
                    break;
                default:
                    throw new BadRequestException("Parameter 'repeat' must be count, forever or until.");
            }
        }

        public static void RemoveBill(QueryParams query, JsonWriter json)
        {
            (Thing bench, BillStack bills) = ResolveBench(query);
            if (bills.Count == 0)
            {
                throw new BadRequestException($"{GameAccess.Clean(bench.LabelShort)} has no bills.");
            }
            Bill bill = bills.Bills[query.RequireInt("index", 0, bills.Count - 1)];
            bills.Delete(bill);

            json.BeginObject();
            json.Prop("removed", GameAccess.Clean(bill.LabelCap));
            json.Prop("bills", bills.Count);
            json.EndObject();
        }

        public static void SetCrop(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            Map map = Find.CurrentMap;
            var cell = new IntVec3(query.RequireInt("x", 0, map.Size.x - 1), 0, query.RequireInt("z", 0, map.Size.z - 1));
            if (!(map.zoneManager.ZoneAt(cell) is Zone_Growing zone))
            {
                throw new BadRequestException("That cell is not in a growing zone.");
            }

            List<ThingDef> valid = PlantUtility.ValidPlantTypesForGrowers(new List<IPlantToGrowSettable> { zone }).ToList();
            string name = query.RequireString("plant");
            ThingDef plant = valid.FirstOrDefault(p => p.defName == name)
                ?? throw new NotFoundException($"'{name}' cannot be sown there now. Plants: {string.Join(", ", valid.Select(p => p.defName))}.");
            zone.SetPlantDefToGrow(plant);

            json.BeginObject();
            json.Prop("zone", GameAccess.Clean(zone.label));
            json.Prop("plant", plant.defName);
            json.EndObject();
        }

        public static void AnswerDialog(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            string choice = query.RequireString("choice");
            Window window = Find.WindowStack.Windows.LastOrDefault(w => w is Dialog_NodeTree || w is Dialog_MessageBox)
                ?? throw new BridgeException(409, "no_dialog", "No dialog with choices is open.");

            switch (window)
            {
                case Dialog_NodeTree tree:
                    Choose(tree, choice);
                    break;
                case Dialog_MessageBox box:
                    Choose(box, choice);
                    break;
            }

            json.BeginObject();
            json.Prop("answered", choice);
            json.Prop("dialog_still_open", Find.WindowStack.Windows.Contains(window));
            json.EndObject();
        }

        private static void Choose(Dialog_NodeTree tree, string choice)
        {
            if (!(FocusEndpoint.CurrentNodeField?.GetValue(tree) is DiaNode node))
            {
                throw new InvalidOperationException("The game's dialogs have changed. This version of the game may need an update of the mod.");
            }
            DiaOption option = node.options.FirstOrDefault(o => string.Equals(FocusEndpoint.OptionText(o), choice, StringComparison.OrdinalIgnoreCase))
                ?? throw new NotFoundException($"No such choice. Choices: {string.Join(" | ", node.options.Select(FocusEndpoint.OptionText))}.");
            if (option.disabled)
            {
                throw new BadRequestException($"That choice is disabled: {GameAccess.Clean(option.disabledReason)}");
            }
            if (ActivateOption == null)
            {
                throw new InvalidOperationException("The game's dialogs have changed. This version of the game may need an update of the mod.");
            }
            ActivateOption.Invoke(option, null);
        }

        private static void Choose(Dialog_MessageBox box, string choice)
        {
            bool Matches(string text) => text != null && string.Equals(GameAccess.Clean(text), choice, StringComparison.OrdinalIgnoreCase);
            if (Matches(box.buttonAText))
            {
                box.buttonAAction?.Invoke();
                box.Close();
            }
            else if (Matches(box.buttonBText))
            {
                box.buttonBAction?.Invoke();
                box.Close();
            }
            else if (Matches(box.buttonCText))
            {
                box.buttonCAction?.Invoke();
                if (box.buttonCClose)
                {
                    box.Close();
                }
            }
            else
            {
                throw new NotFoundException($"No such choice. Choices: {string.Join(" | ", new[] { box.buttonAText, box.buttonBText, box.buttonCText }.Where(t => t != null).Select(GameAccess.Clean))}.");
            }
        }

        public static void ListLetters(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();

            json.BeginObject();
            json.Key("letters").BeginArray();
            foreach (Letter letter in Find.LetterStack.LettersListForReading)
            {
                json.BeginObject();
                json.Prop("id", letter.GetUniqueLoadID());
                json.Prop("label", GameAccess.Clean(letter.Label));
                json.Prop("type", letter.def?.defName);
                if (letter is ChoiceLetter choiceLetter)
                {
                    json.Prop("text", GameAccess.Truncate(GameAccess.Clean(choiceLetter.Text), MaxText));
                    json.Key("choices").BeginArray();
                    foreach (DiaOption option in choiceLetter.Choices)
                    {
                        json.Value(FocusEndpoint.OptionText(option));
                    }
                    json.EndArray();
                }
                json.EndObject();
            }
            json.EndArray();
            json.EndObject();
        }

        // A letter's choices only work inside its dialog, which opens and closes within this call when
        // the choice ends it. A choice that leads further leaves it open for answer_dialog.
        public static void AnswerLetter(QueryParams query, JsonWriter json)
        {
            GameAccess.RequireGame();
            string id = query.RequireString("letter");
            string choice = query.RequireString("choice");
            Letter letter = Find.LetterStack.LettersListForReading.FirstOrDefault(l => l.GetUniqueLoadID() == id)
                ?? throw new NotFoundException($"No letter with the id '{id}'. Find ids with list_letters.");
            if (!(letter is ChoiceLetter))
            {
                throw new BadRequestException("That letter has no choices.");
            }

            letter.OpenLetter();
            Dialog_NodeTree tree = Find.WindowStack.Windows.OfType<Dialog_NodeTree>().LastOrDefault()
                ?? throw new InvalidOperationException("The letter did not open a dialog.");
            try
            {
                Choose(tree, choice);
            }
            catch (NotFoundException)
            {
                tree.Close(doCloseSound: false);
                throw;
            }

            json.BeginObject();
            json.Prop("answered", choice);
            json.Prop("dialog_still_open", Find.WindowStack.Windows.Contains(tree));
            json.EndObject();
        }
    }
}
