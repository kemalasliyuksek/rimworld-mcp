using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>
    /// Sets up the player's ideoligion on request, on the new game screens. It takes the same steps as
    /// the game's own buttons and leaves the result on screen. The player always presses Next.
    /// </summary>
    internal static class IdeoEndpoints
    {
        private const int MaxDescription = 220;

        // The game has no public way to go from the preset screen to the customization screen.
        private static readonly MethodInfo DoCustomizeMethod =
            typeof(Page_ChooseIdeoPreset).GetMethod("DoCustomize", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void RequireIdeology()
        {
            if (!ModsConfig.IdeologyActive)
            {
                throw new BridgeException(409, "ideology_not_active", "This game does not have the Ideology expansion active.");
            }
            if (Current.Game?.World == null || Find.WindowStack == null)
            {
                throw new BridgeException(409, "no_world", "No world has been generated yet. The ideoligion comes after that.");
            }
        }

        private static string CurrentScreen()
        {
            if (Find.WindowStack.IsOpen<Page_ConfigureIdeo>())
            {
                return "customize";
            }
            return Find.WindowStack.IsOpen<Page_ChooseIdeoPreset>() ? "choose_preset" : null;
        }

        private static bool CanChoose(MemeDef meme)
        {
            return !meme.hiddenInChooseMemes && IdeoUtility.IsMemeAllowedFor(meme, Faction.OfPlayer.def);
        }

        public static void Options(QueryParams query, JsonWriter json)
        {
            RequireIdeology();
            string section = query.GetString("section", "presets");
            json.BeginObject();
            switch (section)
            {
                case "presets":
                    WritePresets(json);
                    break;
                case "memes":
                    WriteMemes(json);
                    break;
                case "issues":
                    WriteIssues(json);
                    break;
                case "precepts":
                    WritePrecepts(json, query.RequireString("issue"));
                    break;
                default:
                    throw new BadRequestException("Parameter 'section' must be presets, memes, issues or precepts.");
            }
            json.EndObject();
        }

        private static void WritePresets(JsonWriter json)
        {
            json.Key("presets").BeginArray();
            foreach (IdeoPresetDef preset in DefDatabase<IdeoPresetDef>.AllDefsListForReading)
            {
                json.BeginObject();
                json.Prop("def", preset.defName);
                json.Prop("label", preset.label);
                json.Prop("mod", preset.modContentPack?.Name);
                json.Prop("description", GameAccess.Truncate(GameAccess.Clean(preset.description), MaxDescription));
                WriteDefNames(json, "memes", preset.memes);
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WriteMemes(JsonWriter json)
        {
            json.Key("memes").BeginArray();
            foreach (MemeDef meme in DefDatabase<MemeDef>.AllDefsListForReading.Where(CanChoose).OrderBy(m => m.category))
            {
                json.BeginObject();
                json.Prop("def", meme.defName);
                json.Prop("label", meme.label);
                json.Prop("category", meme.category.ToString());
                json.Prop("impact", meme.impact);
                json.Prop("mod", meme.modContentPack?.Name);
                json.Prop("description", GameAccess.Truncate(GameAccess.Clean(meme.description), MaxDescription));
                if (!meme.exclusionTags.NullOrEmpty())
                {
                    json.Key("exclusion_tags").BeginArray();
                    foreach (string tag in meme.exclusionTags)
                    {
                        json.Value(tag);
                    }
                    json.EndArray();
                }
                json.EndObject();
            }
            json.EndArray();
        }

        private static IEnumerable<PreceptDef> SelectablePrecepts()
        {
            return DefDatabase<PreceptDef>.AllDefsListForReading.Where(p => p.issue != null && p.visible && p.visibleOnAddFloatMenu);
        }

        private static void WriteIssues(JsonWriter json)
        {
            json.Key("issues").BeginArray();
            foreach (IGrouping<IssueDef, PreceptDef> issue in SelectablePrecepts().GroupBy(p => p.issue).OrderBy(g => g.Key.defName))
            {
                json.BeginObject();
                json.Prop("def", issue.Key.defName);
                json.Prop("label", issue.Key.label);
                json.Prop("options", issue.Count());
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WritePrecepts(JsonWriter json, string issueName)
        {
            Ideo ideo = (Find.WindowStack.WindowOfType<Page_ConfigureIdeo>())?.ideo;
            List<PreceptDef> precepts = SelectablePrecepts().Where(p => p.issue.defName == issueName).ToList();
            if (precepts.Count == 0)
            {
                throw new NotFoundException($"No precepts for issue '{issueName}'. Find issues with section 'issues'.");
            }

            json.Key("precepts").BeginArray();
            foreach (PreceptDef precept in precepts)
            {
                json.BeginObject();
                json.Prop("def", precept.defName);
                json.Prop("label", precept.label);
                json.Prop("impact", precept.impact.ToString());
                json.Prop("description", GameAccess.Truncate(GameAccess.Clean(precept.description), MaxDescription));
                if (ideo != null && ideo.memes.Any())
                {
                    AcceptanceReport report = ideo.CanAddPreceptAllFactions(precept);
                    if (!report.Accepted && !ideo.HasPrecept(precept))
                    {
                        json.Prop("not_available", GameAccess.Clean(report.Reason) ?? "conflicts with the current ideoligion");
                    }
                    if (ideo.HasPrecept(precept))
                    {
                        json.Prop("current", true);
                    }
                }
                json.EndObject();
            }
            json.EndArray();
        }

        public static void Setup(QueryParams query, JsonWriter json)
        {
            RequireIdeology();
            string screen = CurrentScreen();
            json.BeginObject();
            json.Prop("screen", screen);
            Ideo ideo = Find.WindowStack.WindowOfType<Page_ConfigureIdeo>()?.ideo;
            if (ideo != null && ideo.memes.Any())
            {
                WriteIdeo(json, ideo);
            }
            json.EndObject();
        }

        public static void Configure(QueryParams query, JsonWriter json)
        {
            RequireIdeology();
            string screen = CurrentScreen();
            if (screen == null)
            {
                throw new BridgeException(409, "not_configuring_ideo", "The player is not on an ideoligion screen.");
            }

            // Everything is checked first, so that a typo cannot leave the screen half configured.
            IdeoPresetDef preset = ResolveDef<IdeoPresetDef>(query.GetString("preset"));
            List<MemeDef> memes = ResolveMemes(query, preset);
            List<PreceptDef> precepts = query.GetList("precepts").Select(n => ResolveDef<PreceptDef>(n)).ToList();
            CheckMemes(memes);

            Page_ConfigureIdeo page = OpenCustomizeScreen(screen);

            // An empty ideoligion waits behind this dialog when the player chose to build their own.
            Find.WindowStack.TryRemove(typeof(Dialog_ChooseMemes), doCloseSound: false);

            Ideo previous = page.ideo;
            Ideo ideo = IdeoGenerator.GenerateIdeo(new IdeoGenerationParms(
                Faction.OfPlayer.def,
                forceNoExpansionIdeo: false,
                disallowedPrecepts: null,
                disallowedMemes: null,
                forcedMemes: memes,
                classicExtra: preset != null && preset.classicPlus));
            page.SelectOrMakeNewIdeo(ideo);
            if (previous != null && previous != ideo)
            {
                Find.IdeoManager.Remove(previous);
            }

            var rejected = new List<(PreceptDef Precept, string Reason)>();
            foreach (PreceptDef precept in precepts)
            {
                string reason = ApplyPrecept(ideo, precept);
                if (reason != null)
                {
                    rejected.Add((precept, reason));
                }
            }

            string name = query.GetString("name");
            if (name != null)
            {
                ideo.name = name;
            }
            ideo.RegenerateDescription(force: true);

            json.BeginObject();
            json.Prop("screen", "customize");
            json.Prop("note", "The player still reviews this on screen and presses Next.");
            json.Key("rejected_precepts").BeginArray();
            foreach ((PreceptDef precept, string reason) in rejected)
            {
                json.BeginObject();
                json.Prop("def", precept.defName);
                json.Prop("reason", reason);
                json.EndObject();
            }
            json.EndArray();
            WriteIdeo(json, ideo);
            json.EndObject();
        }

        private static List<MemeDef> ResolveMemes(QueryParams query, IdeoPresetDef preset)
        {
            List<string> names = query.GetList("memes");
            if (preset != null && names.Count > 0)
            {
                throw new BadRequestException("Pass either 'preset' or 'memes', not both.");
            }
            if (preset == null && names.Count == 0)
            {
                throw new BadRequestException("Pass 'preset' or 'memes'. Find them with get_ideo_options.");
            }

            List<MemeDef> memes = preset != null
                ? preset.memes.ToList()
                : names.Select(n => ResolveDef<MemeDef>(n)).ToList();

            MemeDef structure = ResolveDef<MemeDef>(query.GetString("structure"));
            if (structure != null)
            {
                if (structure.category != MemeCategory.Structure)
                {
                    throw new BadRequestException($"'{structure.defName}' is not a structure meme.");
                }
                memes.RemoveAll(m => m.category == MemeCategory.Structure);
                memes.Add(structure);
            }
            else if (!memes.Any(m => m.category == MemeCategory.Structure))
            {
                memes = IdeoUtility.RandomizeStructureMeme(memes, Faction.OfPlayer.def);
            }
            return memes;
        }

        private static void CheckMemes(List<MemeDef> memes)
        {
            if (!memes.Any(m => m.category == MemeCategory.Normal))
            {
                throw new BadRequestException("An ideoligion needs at least one normal meme.");
            }
            if (memes.Count(m => m.category == MemeCategory.Structure) != 1)
            {
                throw new BadRequestException("An ideoligion needs exactly one structure meme.");
            }
            MemeDef notAllowed = memes.FirstOrDefault(m => !IdeoUtility.IsMemeAllowedFor(m, Faction.OfPlayer.def));
            if (notAllowed != null)
            {
                throw new BadRequestException($"The player's faction cannot use the meme '{notAllowed.defName}'.");
            }
            for (int i = 0; i < memes.Count; i++)
            {
                for (int j = i + 1; j < memes.Count; j++)
                {
                    if (memes[i].exclusionTags.Any(tag => memes[j].exclusionTags.Contains(tag)))
                    {
                        throw new BadRequestException($"The memes '{memes[i].defName}' and '{memes[j].defName}' cannot be combined.");
                    }
                }
            }
        }

        private static Page_ConfigureIdeo OpenCustomizeScreen(string screen)
        {
            if (screen == "choose_preset")
            {
                if (DoCustomizeMethod == null)
                {
                    throw new InvalidOperationException("Page_ChooseIdeoPreset.DoCustomize no longer exists. The ideoligion setup needs updating for this version of the game.");
                }
                DoCustomizeMethod.Invoke(Find.WindowStack.WindowOfType<Page_ChooseIdeoPreset>(), new object[] { false });
            }
            return Find.WindowStack.WindowOfType<Page_ConfigureIdeo>()
                ?? throw new InvalidOperationException("The game did not open the ideoligion customization screen.");
        }

        /// <returns>Why the precept could not be added, or null if it was.</returns>
        private static string ApplyPrecept(Ideo ideo, PreceptDef def)
        {
            if (ideo.HasPrecept(def))
            {
                return null;
            }

            // On issues that allow only one choice, the new precept replaces the current one.
            Precept replaced = ideo.HasMaxPreceptsForIssue(def.issue)
                ? ideo.PreceptsListForReading.FirstOrDefault(p => p.def.issue == def.issue)
                : null;
            if (replaced != null)
            {
                ideo.RemovePrecept(replaced, replacing: true);
            }

            AcceptanceReport report = ideo.CanAddPreceptAllFactions(def);
            if (!report.Accepted)
            {
                if (replaced != null)
                {
                    ideo.AddPrecept(replaced);
                }
                return GameAccess.Clean(report.Reason) ?? "It conflicts with the rest of the ideoligion.";
            }

            ideo.AddPrecept(PreceptMaker.MakePrecept(def), init: true, null, def.ritualPatternBase);
            ideo.anyPreceptEdited = true;
            return null;
        }

        private static void WriteIdeo(JsonWriter json, Ideo ideo)
        {
            json.Key("ideoligion").BeginObject();
            json.Prop("name", ideo.name);
            json.Prop("culture", ideo.culture?.label);
            json.Prop("description", GameAccess.Clean(ideo.description));
            WriteDefNames(json, "memes", ideo.memes);
            json.Key("precepts").BeginArray();
            foreach (Precept precept in ideo.PreceptsListForReading.Where(p => p.def.visible))
            {
                json.BeginObject();
                json.Prop("def", precept.def.defName);
                json.Prop("issue", precept.def.issue?.defName);
                json.Prop("label", GameAccess.Clean(precept.TipLabel));
                json.Prop("impact", precept.def.impact.ToString());
                json.EndObject();
            }
            json.EndArray();

            // These are the same checks the game runs when the player presses Next.
            var problems = new List<string>();
            if (ideo.name.NullOrEmpty())
            {
                problems.Add("The ideoligion has no name.");
            }
            Pair<Precept, Precept> incompatible = ideo.FirstIncompatiblePreceptPair();
            if (incompatible != default(Pair<Precept, Precept>))
            {
                problems.Add($"Incompatible precepts: {incompatible.First.def.defName} and {incompatible.Second.def.defName}.");
            }
            Tuple<Precept_Ritual, List<string>> ritual = ideo.FirstRitualMissingTarget();
            if (ritual != null)
            {
                problems.Add($"The ritual {ritual.Item1.def.defName} has no target: {string.Join(", ", ritual.Item2)}.");
            }
            Precept_Building building = ideo.FirstConsumableBuildingMissingRitual();
            if (building != null)
            {
                problems.Add($"The building {building.def.defName} has no ritual.");
            }
            json.Prop("ready_for_next", problems.Count == 0);
            json.Key("problems").BeginArray();
            foreach (string problem in problems)
            {
                json.Value(problem);
            }
            json.EndArray();
            json.EndObject();
        }

        private static void WriteDefNames(JsonWriter json, string key, IEnumerable<Def> defs)
        {
            json.Key(key).BeginArray();
            foreach (Def def in defs)
            {
                json.BeginObject();
                json.Prop("def", def.defName);
                json.Prop("label", def.label);
                json.EndObject();
            }
            json.EndArray();
        }

        private static T ResolveDef<T>(string name) where T : Def
        {
            if (name == null)
            {
                return null;
            }
            return DefDatabase<T>.GetNamedSilentFail(name)
                ?? throw new NotFoundException($"No {typeof(T).Name} named '{name}'. Find names with get_ideo_options.");
        }
    }
}
