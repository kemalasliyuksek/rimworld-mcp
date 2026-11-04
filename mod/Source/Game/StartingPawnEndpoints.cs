using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorldMcp.Bridge.Core;
using Verse;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>
    /// Reads and changes the colonists on the new game screen, when the player asks. Results appear on
    /// screen right away, and the player still presses Start themselves.
    /// </summary>
    internal static class StartingPawnEndpoints
    {
        // Generating pawns freezes the screen while it runs, and generating fewer is not a problem
        // because the best candidate so far is kept.
        private const long SearchBudgetMs = 3000;

        private const int MinAdultAge = 18;

        // Below this age the game gives a pawn no adulthood backstory.
        private const int AdulthoodBackstoryAge = 20;

        private const long TicksPerYear = 3600000;

        private static Page_ConfigureStartingPawns RequireScreen()
        {
            Page_ConfigureStartingPawns page = Find.WindowStack?.WindowOfType<Page_ConfigureStartingPawns>();
            if (page == null || Find.GameInitData == null)
            {
                throw new BridgeException(409, "not_choosing_colonists", "The player is not on the screen for choosing colonists.");
            }
            return page;
        }

        private static List<Pawn> Pawns => Find.GameInitData.startingAndOptionalPawns;

        private static int ResolveIndex(QueryParams query, string name = "index")
        {
            return query.RequireInt(name, 0, Pawns.Count - 1);
        }

        public static void List(QueryParams query, JsonWriter json)
        {
            RequireScreen();
            json.BeginObject();
            json.Prop("starting_count", Find.GameInitData.startingPawnCount);
            json.Key("pawns").BeginArray();
            for (int i = 0; i < Pawns.Count; i++)
            {
                WritePawn(json, i);
            }
            json.EndArray();
            WriteTeamProblems(json);
            json.EndObject();
        }

        public static void FindCandidate(QueryParams query, JsonWriter json)
        {
            Page_ConfigureStartingPawns page = RequireScreen();
            int index = ResolveIndex(query);
            var criteria = new PawnCriteria(query);

            PawnGenerationRequest original = StartingPawnUtility.GetGenerationRequest(index);
            StartingPawnUtility.SetGenerationRequest(index, criteria.Apply(original));

            Pawn best = null;
            int bestShortfall = int.MaxValue;
            int attempts = 0;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                while (bestShortfall > 0 && (attempts == 0 || stopwatch.ElapsedMilliseconds < SearchBudgetMs))
                {
                    attempts++;
                    Pawn candidate = StartingPawnUtility.NewGeneratedStartingPawn(index);
                    int shortfall = criteria.Shortfall(candidate);
                    if (shortfall < bestShortfall)
                    {
                        if (best != null)
                        {
                            Discard(best);
                        }
                        best = candidate;
                        bestShortfall = shortfall;
                    }
                    else
                    {
                        Discard(candidate);
                    }
                }
            }
            finally
            {
                // Otherwise the game's own Randomize button would keep using these criteria.
                StartingPawnUtility.SetGenerationRequest(index, original);
            }

            Replace(index, best);
            page.SelectPawn(best);

            json.BeginObject();
            json.Prop("attempts", attempts);
            json.Prop("all_criteria_met", bestShortfall == 0);
            json.Key("unmet").BeginArray();
            foreach (string unmet in criteria.Unmet(best))
            {
                json.Value(unmet);
            }
            json.EndArray();
            json.Key("pawn");
            WritePawn(json, index);
            WriteTeamProblems(json);
            json.EndObject();
        }

        // The same steps as the game's own regeneration, which cannot take an already generated pawn.
        private static void Replace(int index, Pawn replacement)
        {
            Pawn current = Pawns[index];
            SpouseRelationUtility.Notify_PawnRegenerated(current);
            Discard(current);
            Pawns[index] = null;
            foreach (Pawn other in Pawns.Where(p => p != null))
            {
                PawnUtility.TryDestroyStartingColonistFamily(other);
            }
            Pawns[index] = replacement;
        }

        private static void Discard(Pawn pawn)
        {
            PawnUtility.TryDestroyStartingColonistFamily(pawn);
            pawn.relations.ClearAllRelations();
            PawnComponentsUtility.RemoveComponentsOnDespawned(pawn);
            Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
            Find.GameInitData.startingPossessions.Remove(pawn);
        }

        public static void Swap(QueryParams query, JsonWriter json)
        {
            Page_ConfigureStartingPawns page = RequireScreen();
            int first = ResolveIndex(query, "first");
            int second = ResolveIndex(query, "second");

            Pawn pawn = Pawns[first];
            Pawns[first] = Pawns[second];
            Pawns[second] = pawn;

            PawnGenerationRequest request = StartingPawnUtility.GetGenerationRequest(first);
            StartingPawnUtility.SetGenerationRequest(first, StartingPawnUtility.GetGenerationRequest(second));
            StartingPawnUtility.SetGenerationRequest(second, request);
            page.SelectPawn(Pawns[first]);

            json.BeginObject();
            json.Prop("starting_count", Find.GameInitData.startingPawnCount);
            json.Key("pawns").BeginArray();
            WritePawn(json, first);
            WritePawn(json, second);
            json.EndArray();
            WriteTeamProblems(json);
            json.EndObject();
        }

        public static void Edit(QueryParams query, JsonWriter json)
        {
            Page_ConfigureStartingPawns page = RequireScreen();
            int index = ResolveIndex(query);
            Pawn pawn = Pawns[index];
            PawnEdit edit = PawnEdit.Validate(pawn, query);

            edit.Apply(pawn);
            page.SelectPawn(pawn);

            json.BeginObject();
            json.Key("pawn");
            WritePawn(json, index);
            WriteTeamProblems(json);
            json.EndObject();
        }

        private static void WritePawn(JsonWriter json, int index)
        {
            Pawn pawn = Pawns[index];
            json.BeginObject();
            json.Prop("index", index);
            json.Prop("selected", index < Find.GameInitData.startingPawnCount);
            if (pawn.Name is NameTriple name)
            {
                json.Prop("first_name", name.First);
                json.Prop("nickname", name.Nick);
                json.Prop("last_name", name.Last);
            }
            else
            {
                json.Prop("name", pawn.LabelShort);
            }
            json.Prop("gender", pawn.gender.ToString());
            json.Prop("age_biological", pawn.ageTracker.AgeBiologicalYears);
            json.Prop("age_chronological", pawn.ageTracker.AgeChronologicalYears);
            if (ModsConfig.BiotechActive && pawn.genes != null)
            {
                json.Prop("xenotype", pawn.genes.XenotypeLabelCap);
            }
            WriteBackstory(json, "childhood", pawn.story.Childhood, pawn);
            WriteBackstory(json, "adulthood", pawn.story.Adulthood, pawn);

            json.Key("traits").BeginArray();
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                json.BeginObject();
                json.Prop("def", trait.def.defName);
                json.Prop("degree", trait.Degree);
                json.Prop("label", GameAccess.Clean(trait.LabelCap));
                json.EndObject();
            }
            json.EndArray();

            json.Key("skills").BeginArray();
            foreach (SkillRecord skill in pawn.skills.skills)
            {
                json.BeginObject();
                json.Prop("def", skill.def.defName);
                json.Prop("label", skill.def.skillLabel);
                if (skill.TotallyDisabled)
                {
                    json.Prop("disabled", true);
                }
                else
                {
                    json.Prop("level", skill.Level);
                    json.Prop("passion", skill.passion.ToString());
                }
                json.EndObject();
            }
            json.EndArray();

            json.Prop("disabled_work", pawn.CombinedDisabledWorkTags.ToString());
            json.Prop("favorite_color", pawn.story.favoriteColor?.defName);
            json.Prop("hair", pawn.story.hairDef?.defName);
            json.Prop("beard", pawn.style?.beardDef?.defName);
            json.Prop("weapon", GameAccess.Clean(pawn.equipment?.Primary?.LabelCap));
            json.Key("apparel").BeginArray();
            if (pawn.apparel != null)
            {
                foreach (Apparel apparel in pawn.apparel.WornApparel)
                {
                    json.Value(GameAccess.Clean(apparel.LabelCap));
                }
            }
            json.EndArray();
            json.Key("possessions").BeginArray();
            if (Find.GameInitData.startingPossessions.TryGetValue(pawn, out List<ThingDefCount> possessions))
            {
                foreach (ThingDefCount possession in possessions)
                {
                    json.BeginObject();
                    json.Prop("def", possession.ThingDef.defName);
                    json.Prop("label", possession.ThingDef.label);
                    json.Prop("count", possession.Count);
                    json.EndObject();
                }
            }
            json.EndArray();
            json.Key("health").BeginArray();
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs.Where(h => h.Visible))
            {
                json.BeginObject();
                json.Prop("def", hediff.def.defName);
                json.Prop("label", GameAccess.Clean(hediff.LabelCap));
                json.Prop("part", hediff.Part?.Label);
                json.EndObject();
            }
            json.EndArray();
            json.EndObject();
        }

        private static void WriteBackstory(JsonWriter json, string key, BackstoryDef backstory, Pawn pawn)
        {
            if (backstory == null)
            {
                return;
            }
            json.Key(key).BeginObject();
            json.Prop("def", backstory.defName);
            json.Prop("label", GameAccess.Clean(backstory.TitleFor(pawn.gender)));
            json.EndObject();
        }

        private static void WriteTeamProblems(JsonWriter json)
        {
            json.Key("work_nobody_selected_can_do").BeginArray();
            foreach (WorkTypeDef work in StartingPawnUtility.RequiredWorkTypesDisabledForEveryone())
            {
                json.Value(work.labelShort);
            }
            json.EndArray();
        }

        private static SkillDef ResolveSkill(string name)
        {
            return DefDatabase<SkillDef>.GetNamedSilentFail(name)
                ?? throw new NotFoundException($"No skill named '{name}'. Use: {string.Join(", ", DefDatabase<SkillDef>.AllDefsListForReading.Select(s => s.defName))}.");
        }

        private static TraitDef ResolveTrait(string name)
        {
            return DefDatabase<TraitDef>.GetNamedSilentFail(name)
                ?? throw new NotFoundException($"No trait named '{name}'. Find names with search_defs and type TraitDef.");
        }

        private static Passion ResolvePassion(string name)
        {
            if (Enum.TryParse(name, ignoreCase: true, out Passion passion) && Enum.IsDefined(typeof(Passion), passion))
            {
                return passion;
            }
            throw new BadRequestException($"Unknown passion '{name}'. Use None, Minor or Major.");
        }

        /// <summary>What the player wants from a generated pawn.</summary>
        private sealed class PawnCriteria
        {
            private readonly Gender? gender;
            private readonly int? minAge;
            private readonly int? maxAge;
            private readonly List<TraitDef> requiredTraits;
            private readonly List<TraitDef> forbiddenTraits;
            private readonly List<(SkillDef Skill, int Level)> minSkills = new List<(SkillDef, int)>();
            private readonly List<(SkillDef Skill, Passion Passion)> passions = new List<(SkillDef, Passion)>();
            private readonly List<WorkTags> forbiddenDisabledWork = new List<WorkTags>();
            private readonly bool noDisabledWork;

            public PawnCriteria(QueryParams query)
            {
                string genderName = query.GetString("gender");
                if (genderName != null)
                {
                    if (!Enum.TryParse(genderName, ignoreCase: true, out Gender parsed) || parsed == Gender.None)
                    {
                        throw new BadRequestException("Parameter 'gender' must be Male or Female.");
                    }
                    gender = parsed;
                }
                if (query.GetString("min_age") != null)
                {
                    minAge = query.GetInt("min_age", 0, MinAdultAge, 120);
                }
                if (query.GetString("max_age") != null)
                {
                    maxAge = query.GetInt("max_age", 0, MinAdultAge, 120);
                }

                requiredTraits = query.GetList("require_traits").Select(ResolveTrait).ToList();
                forbiddenTraits = query.GetList("forbid_traits").Select(ResolveTrait).ToList();

                foreach ((string name, string value) in query.GetPairs("min_skills"))
                {
                    if (!int.TryParse(value, out int level) || level < 0 || level > SkillRecord.MaxLevel)
                    {
                        throw new BadRequestException($"The level for '{name}' must be between 0 and {SkillRecord.MaxLevel}.");
                    }
                    minSkills.Add((ResolveSkill(name), level));
                }
                foreach ((string name, string value) in query.GetPairs("passions"))
                {
                    passions.Add((ResolveSkill(name), ResolvePassion(value)));
                }

                foreach (string tag in query.GetList("must_be_capable_of"))
                {
                    if (string.Equals(tag, "everything", StringComparison.OrdinalIgnoreCase))
                    {
                        noDisabledWork = true;
                    }
                    else if (Enum.TryParse(tag, ignoreCase: true, out WorkTags parsed) && parsed != WorkTags.None)
                    {
                        forbiddenDisabledWork.Add(parsed);
                    }
                    else
                    {
                        throw new BadRequestException($"Unknown work type '{tag}'. Use 'everything', or a work tag such as Violent, Intellectual, Artistic, Caring, Social, Animals, ManualDumb, ManualSkilled, Mining, Cooking, Firefighting or Crafting.");
                    }
                }
            }

            public PawnGenerationRequest Apply(PawnGenerationRequest request)
            {
                if (gender != null)
                {
                    request.FixedGender = gender;
                }
                if (minAge != null || maxAge != null)
                {
                    request.BiologicalAgeRange = new FloatRange(minAge ?? MinAdultAge, maxAge ?? 120);
                }
                if (requiredTraits.Count > 0)
                {
                    request.ForcedTraits = requiredTraits;
                }
                if (forbiddenTraits.Count > 0)
                {
                    request.ProhibitedTraits = forbiddenTraits;
                }
                return request;
            }

            /// <summary>How far a pawn is from the criteria. Zero means it meets all of them.</summary>
            public int Shortfall(Pawn pawn)
            {
                int total = Unmet(pawn).Count;
                foreach ((SkillDef skill, int level) in minSkills)
                {
                    total += Math.Max(0, level - LevelOf(pawn, skill));
                }
                return total;
            }

            public List<string> Unmet(Pawn pawn)
            {
                var unmet = new List<string>();
                foreach ((SkillDef skill, int level) in minSkills)
                {
                    if (LevelOf(pawn, skill) < level)
                    {
                        unmet.Add($"{skill.defName} at least {level}, got {LevelOf(pawn, skill)}");
                    }
                }
                foreach ((SkillDef skill, Passion passion) in passions)
                {
                    if (pawn.skills.GetSkill(skill).passion < passion)
                    {
                        unmet.Add($"{passion} passion in {skill.defName}, got {pawn.skills.GetSkill(skill).passion}");
                    }
                }
                foreach (TraitDef trait in requiredTraits.Where(t => !pawn.story.traits.HasTrait(t)))
                {
                    unmet.Add($"has the trait {trait.defName}");
                }
                foreach (TraitDef trait in forbiddenTraits.Where(t => pawn.story.traits.HasTrait(t)))
                {
                    unmet.Add($"does not have the trait {trait.defName}");
                }
                WorkTags disabled = pawn.CombinedDisabledWorkTags;
                if (noDisabledWork && disabled != WorkTags.None)
                {
                    unmet.Add($"can do all work, but cannot do {disabled}");
                }
                foreach (WorkTags tag in forbiddenDisabledWork.Where(t => (disabled & t) != 0))
                {
                    unmet.Add($"capable of {tag}");
                }
                return unmet;
            }

            private static int LevelOf(Pawn pawn, SkillDef skill)
            {
                SkillRecord record = pawn.skills.GetSkill(skill);
                return record.TotallyDisabled ? 0 : record.Level;
            }
        }

        /// <summary>A checked set of changes. Validating first means a typo cannot leave a half-edited pawn.</summary>
        private sealed class PawnEdit
        {
            private string firstName;
            private string nickname;
            private string lastName;
            private int? age;
            private BackstoryDef childhood;
            private BackstoryDef adulthood;
            private readonly List<(SkillDef Skill, int Level)> skills = new List<(SkillDef, int)>();
            private readonly List<(SkillDef Skill, Passion Passion)> passions = new List<(SkillDef, Passion)>();
            private readonly List<TraitDef> removedTraits = new List<TraitDef>();
            private readonly List<(TraitDef Trait, int Degree)> addedTraits = new List<(TraitDef, int)>();
            private bool clearHealth;
            private Gender? gender;
            private HairDef hair;
            private BeardDef beard;
            private ColorDef favoriteColor;
            private XenotypeDef xenotype;
            private List<ThingDefCount> possessions;
            private HeadTypeDef head;
            private BodyTypeDef body;
            private ThingDef weapon;
            private ThingDef weaponStuff;
            private bool clearApparel;
            private readonly List<(ThingDef Apparel, ThingDef Stuff)> wear = new List<(ThingDef, ThingDef)>();
            private readonly List<HediffDef> implants = new List<HediffDef>();
            private UnityEngine.Color? hairColor;
            private UnityEngine.Color? skinColor;

            public static PawnEdit Validate(Pawn pawn, QueryParams query)
            {
                var edit = new PawnEdit
                {
                    firstName = query.GetString("first_name"),
                    nickname = query.GetString("nickname"),
                    lastName = query.GetString("last_name"),
                    childhood = ResolveBackstory(query.GetString("childhood"), BackstorySlot.Childhood),
                    adulthood = ResolveBackstory(query.GetString("adulthood"), BackstorySlot.Adulthood),
                    clearHealth = query.GetString("clear_health") == "true",
                };

                if (query.GetString("age") != null)
                {
                    edit.age = query.GetInt("age", 0, MinAdultAge, 120);
                    if (edit.age >= AdulthoodBackstoryAge && pawn.story.Adulthood == null && edit.adulthood == null)
                    {
                        throw new BadRequestException($"A pawn aged {AdulthoodBackstoryAge} or older needs an adulthood backstory, and this one has none. Pass 'adulthood' too. Find names with search_defs and type BackstoryDef.");
                    }
                }
                foreach ((string name, string value) in query.GetPairs("skills"))
                {
                    if (!int.TryParse(value, out int level) || level < 0 || level > SkillRecord.MaxLevel)
                    {
                        throw new BadRequestException($"The level for '{name}' must be between 0 and {SkillRecord.MaxLevel}.");
                    }
                    edit.skills.Add((ResolveSkill(name), level));
                }
                foreach ((string name, string value) in query.GetPairs("passions"))
                {
                    edit.passions.Add((ResolveSkill(name), ResolvePassion(value)));
                }
                edit.removedTraits.AddRange(query.GetList("remove_traits").Select(ResolveTrait));
                foreach (string entry in query.GetList("add_traits"))
                {
                    edit.addedTraits.Add(ResolveAddedTrait(entry));
                }
                edit.CheckTraits(pawn);

                string genderName = query.GetString("gender");
                if (genderName != null)
                {
                    if (!Enum.TryParse(genderName, ignoreCase: true, out Gender parsed) || parsed == Gender.None)
                    {
                        throw new BadRequestException("Parameter 'gender' must be Male or Female.");
                    }
                    edit.gender = parsed;
                }
                edit.hair = ResolveStyleDef<HairDef>(query.GetString("hair"));
                edit.beard = ResolveStyleDef<BeardDef>(query.GetString("beard"));
                edit.favoriteColor = ResolveStyleDef<ColorDef>(query.GetString("favorite_color"));
                edit.xenotype = ResolveStyleDef<XenotypeDef>(query.GetString("xenotype"));
                if (edit.xenotype != null && !ModsConfig.BiotechActive)
                {
                    throw new BadRequestException("Changing xenotype needs the Biotech expansion.");
                }

                edit.hairColor = ParseColor(query, "hair_color");
                edit.skinColor = ParseColor(query, "skin_color");
                edit.head = ResolveStyleDef<HeadTypeDef>(query.GetString("head"));
                edit.body = ResolveStyleDef<BodyTypeDef>(query.GetString("body"));
                edit.clearApparel = query.GetString("clear_apparel") == "true";
                foreach (string entry in query.GetList("wear"))
                {
                    edit.wear.Add(ResolveMadeThing(entry, t => t.IsApparel, "apparel"));
                }
                string weaponName = query.GetString("weapon");
                if (weaponName != null)
                {
                    (edit.weapon, edit.weaponStuff) = ResolveMadeThing(weaponName, t => t.IsWeapon && t.equipmentType == EquipmentType.Primary, "weapon");
                }
                foreach (string name in query.GetList("add_implants"))
                {
                    HediffDef implant = ResolveStyleDef<HediffDef>(name);
                    if (FindImplantRecipe(implant) == null)
                    {
                        throw new BadRequestException($"'{name}' is not an implant that surgery can install.");
                    }
                    edit.implants.Add(implant);
                }

                List<(string Name, string Value)> items = query.GetPairs("possessions");
                if (items.Count > 0)
                {
                    edit.possessions = new List<ThingDefCount>();
                    foreach ((string name, string value) in items)
                    {
                        ThingDef item = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                        if (item == null || item.category != ThingCategory.Item)
                        {
                            throw new NotFoundException($"No item named '{name}'. Find names with search_defs and type ThingDef.");
                        }
                        if (!int.TryParse(value, out int count) || count < 1)
                        {
                            throw new BadRequestException($"The count for '{name}' must be a positive number.");
                        }
                        edit.possessions.Add(new ThingDefCount(item, count));
                    }
                }
                return edit;
            }

            private static T ResolveStyleDef<T>(string name) where T : Def
            {
                if (name == null)
                {
                    return null;
                }
                return DefDatabase<T>.GetNamedSilentFail(name)
                    ?? throw new NotFoundException($"No {typeof(T).Name} named '{name}'. Find names with search_defs.");
            }

            private static UnityEngine.Color? ParseColor(QueryParams query, string name)
            {
                string raw = query.GetString(name);
                if (raw == null)
                {
                    return null;
                }
                int[] parts = raw.Split(',').Select(p => int.TryParse(p.Trim(), out int v) ? v : -1).ToArray();
                if (parts.Length != 3 || parts.Any(v => v < 0 || v > 255))
                {
                    throw new BadRequestException($"Parameter '{name}' must be three numbers from 0 to 255, such as 120,80,40.");
                }
                return new UnityEngine.Color(parts[0] / 255f, parts[1] / 255f, parts[2] / 255f);
            }

            private static (ThingDef, ThingDef) ResolveMadeThing(string entry, Func<ThingDef, bool> isRightKind, string kind)
            {
                string[] parts = entry.Split(':');
                ThingDef thing = ResolveStyleDef<ThingDef>(parts[0].Trim());
                if (!isRightKind(thing))
                {
                    throw new BadRequestException($"'{thing.defName}' is not {kind}.");
                }
                ThingDef stuff = parts.Length > 1 ? ResolveStyleDef<ThingDef>(parts[1].Trim()) : null;
                if (thing.MadeFromStuff)
                {
                    stuff = stuff ?? GenStuff.DefaultStuffFor(thing);
                    if (!stuff.IsStuff || !stuff.stuffProps.CanMake(thing))
                    {
                        throw new BadRequestException($"'{thing.defName}' cannot be made from '{stuff.defName}'.");
                    }
                }
                else if (stuff != null)
                {
                    throw new BadRequestException($"'{thing.defName}' is not made from a material.");
                }
                return (thing, stuff);
            }

            private static RecipeDef FindImplantRecipe(HediffDef implant)
            {
                return DefDatabase<RecipeDef>.AllDefs.FirstOrDefault(r => r.addsHediff == implant && r.targetsBodyPart);
            }

            private static void InstallImplant(Pawn pawn, HediffDef implant)
            {
                RecipeDef recipe = FindImplantRecipe(implant);
                BodyPartRecord part = pawn.health.hediffSet.GetNotMissingParts()
                    .FirstOrDefault(p => recipe.appliedOnFixedBodyParts.Contains(p.def)
                        && !pawn.health.hediffSet.hediffs.Any(h => h.def == implant && h.Part == p));
                if (part == null && recipe.appliedOnFixedBodyParts.Count > 0)
                {
                    return;
                }
                pawn.health.AddHediff(implant, part);
            }

            private static BackstoryDef ResolveBackstory(string name, BackstorySlot slot)
            {
                if (name == null)
                {
                    return null;
                }
                BackstoryDef backstory = DefDatabase<BackstoryDef>.GetNamedSilentFail(name)
                    ?? throw new NotFoundException($"No backstory named '{name}'. Find names with search_defs and type BackstoryDef.");
                if (backstory.slot != slot)
                {
                    throw new BadRequestException($"'{name}' is a {backstory.slot} backstory, not a {slot} one.");
                }
                return backstory;
            }

            private static (TraitDef, int) ResolveAddedTrait(string entry)
            {
                string[] parts = entry.Split(':');
                TraitDef trait = ResolveTrait(parts[0].Trim());
                string degrees = string.Join(", ", trait.degreeDatas.Select(d => $"{d.degree} ({d.label})"));
                if (parts.Length == 1)
                {
                    if (trait.degreeDatas.Count != 1)
                    {
                        throw new BadRequestException($"The trait '{trait.defName}' has several levels. Write it as {trait.defName}:level, using one of: {degrees}.");
                    }
                    return (trait, trait.degreeDatas[0].degree);
                }
                if (!int.TryParse(parts[1], out int degree) || trait.degreeDatas.All(d => d.degree != degree))
                {
                    throw new BadRequestException($"The trait '{trait.defName}' has no level '{parts[1]}'. Use one of: {degrees}.");
                }
                return (trait, degree);
            }

            private void CheckTraits(Pawn pawn)
            {
                List<TraitDef> kept = pawn.story.traits.allTraits.Select(t => t.def).Where(d => !removedTraits.Contains(d)).ToList();
                foreach ((TraitDef trait, int _) in addedTraits)
                {
                    foreach (TraitDef other in kept)
                    {
                        if (other == trait)
                        {
                            throw new BadRequestException($"The pawn already has the trait '{trait.defName}'. To change its level, remove it as well.");
                        }
                        if (trait.ConflictsWith(other))
                        {
                            throw new BadRequestException($"The trait '{trait.defName}' conflicts with '{other.defName}'. Remove that one as well.");
                        }
                    }
                    kept.Add(trait);
                }
            }

            // A pawn's body, head and hair are chosen for their gender when they are generated.
            private static void ChangeGender(Pawn pawn, Gender newGender)
            {
                pawn.gender = newGender;
                if (pawn.story.bodyType == BodyTypeDefOf.Male || pawn.story.bodyType == BodyTypeDefOf.Female)
                {
                    pawn.story.bodyType = newGender == Gender.Female ? BodyTypeDefOf.Female : BodyTypeDefOf.Male;
                }
                pawn.story.TryGetRandomHeadFromSet(DefDatabase<HeadTypeDef>.AllDefs.Where(h => h.randomChosen && (h.gender == Gender.None || h.gender == newGender)));
                pawn.story.hairDef = PawnStyleItemChooser.RandomHairFor(pawn);
                if (newGender == Gender.Female && pawn.style != null)
                {
                    pawn.style.beardDef = BeardDefOf.NoBeard;
                }
            }

            public void Apply(Pawn pawn)
            {
                if (firstName != null || nickname != null || lastName != null)
                {
                    var current = pawn.Name as NameTriple;
                    pawn.Name = new NameTriple(
                        firstName ?? current?.First ?? pawn.LabelShort,
                        nickname ?? current?.Nick ?? pawn.LabelShort,
                        lastName ?? current?.Last ?? "");
                }

                if (childhood != null)
                {
                    pawn.story.Childhood = childhood;
                }
                if (adulthood != null)
                {
                    pawn.story.Adulthood = adulthood;
                }

                if (age != null)
                {
                    pawn.ageTracker.AgeBiologicalTicks = age.Value * TicksPerYear;
                    if (pawn.ageTracker.AgeChronologicalTicks < pawn.ageTracker.AgeBiologicalTicks)
                    {
                        pawn.ageTracker.AgeChronologicalTicks = pawn.ageTracker.AgeBiologicalTicks;
                    }
                }

                foreach (TraitDef removed in removedTraits)
                {
                    Trait trait = pawn.story.traits.GetTrait(removed);
                    if (trait != null)
                    {
                        pawn.story.traits.RemoveTrait(trait);
                    }
                }
                foreach ((TraitDef trait, int degree) in addedTraits)
                {
                    pawn.story.traits.GainTrait(new Trait(trait, degree));
                }

                if (clearHealth)
                {
                    foreach (Hediff hediff in pawn.health.hediffSet.hediffs.Where(h => h.Visible).ToList())
                    {
                        pawn.health.RemoveHediff(hediff);
                    }
                }

                if (gender != null && gender != pawn.gender)
                {
                    ChangeGender(pawn, gender.Value);
                }
                if (hair != null)
                {
                    pawn.story.hairDef = hair;
                }
                if (beard != null && pawn.style != null)
                {
                    pawn.style.beardDef = beard;
                }
                if (favoriteColor != null)
                {
                    pawn.story.favoriteColor = favoriteColor;
                }
                if (xenotype != null)
                {
                    pawn.genes?.SetXenotype(xenotype);
                }
                if (possessions != null)
                {
                    Find.GameInitData.startingPossessions[pawn] = possessions;
                }
                if (hairColor != null)
                {
                    pawn.story.HairColor = hairColor.Value;
                }
                if (skinColor != null)
                {
                    pawn.story.skinColorOverride = skinColor;
                }
                if (head != null)
                {
                    pawn.story.headType = head;
                }
                if (body != null)
                {
                    pawn.story.bodyType = body;
                }
                foreach (HediffDef implant in implants)
                {
                    InstallImplant(pawn, implant);
                }
                if (clearApparel)
                {
                    pawn.apparel?.DestroyAll();
                }
                foreach ((ThingDef thing, ThingDef stuff) in wear)
                {
                    var apparel = (Apparel)ThingMaker.MakeThing(thing, stuff);
                    foreach (Apparel worn in pawn.apparel.WornApparel.Where(a => !ApparelUtility.CanWearTogether(a.def, thing, pawn.RaceProps.body)).ToList())
                    {
                        pawn.apparel.Remove(worn);
                        worn.Destroy();
                    }
                    pawn.apparel.Wear(apparel, dropReplacedApparel: false);
                }
                if (weapon != null)
                {
                    pawn.equipment.DestroyAllEquipment();
                    pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(weapon, weaponStuff));
                }

                // Backstories and traits can change which work and skills are disabled.
                pawn.Notify_DisabledWorkTypesChanged();
                pawn.skills.Notify_SkillDisablesChanged();

                foreach ((SkillDef skill, int level) in skills)
                {
                    SkillRecord record = pawn.skills.GetSkill(skill);
                    record.Level = level;
                    record.xpSinceLastLevel = 0f;
                }
                foreach ((SkillDef skill, Passion passion) in passions)
                {
                    pawn.skills.GetSkill(skill).passion = passion;
                }

                pawn.Drawer?.renderer?.SetAllGraphicsDirty();
                PortraitsCache.SetDirty(pawn);
            }
        }
    }
}
