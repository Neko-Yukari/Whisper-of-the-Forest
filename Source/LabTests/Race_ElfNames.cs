using System.Collections.Generic;
using System.Linq;
using RimWorld;
using WhisperOfTheForest.Race;
using Verse;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    [LabTest(Reload = true)]
    public sealed class Race_ElfNames : LabTest
    {
        private readonly List<int> savedIds = new List<int>();
        private readonly List<string> savedNames = new List<string>();
        private readonly List<string> evidence = new List<string>();
        private List<string> firstNames;
        private List<string> clanNames;
        private List<string> reservedNames;

        public override string Name => "race.elf-names";

        protected override void Run(TestContext t)
        {
            t.Expect(Translator.TryGetTranslatedStringsForFile("Names/WotF_ElfFemale", out firstNames),
                "female name list did not load");
            t.Expect(Translator.TryGetTranslatedStringsForFile("Names/WotF_ElfClan", out clanNames),
                "clan name list did not load");
            reservedNames = DefDatabase<RulePackDef>.GetNamed("WotF_ElfNames")
                .GetModExtension<ElfNameRules>().reservedNames;
            if (firstNames == null || clanNames == null) return;
            t.Expect(firstNames.Count > 0 && clanNames.Count > 0,
                "name lists must not be empty, got " + firstNames.Count + "/" + clanNames.Count);

            PawnKindDef[] kinds = DefDatabase<PawnKindDef>.AllDefsListForReading
                .Where(k => k.race.defName == "WotF_Elf").OrderBy(k => k.defName).ToArray();
            Faction court = Find.FactionManager.AllFactionsListForReading
                .FirstOrDefault(f => f.def.defName == "WotF_GreenCourt");
            if (court == null)
            {
                court = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(
                    DefDatabase<FactionDef>.GetNamed("WotF_GreenCourt"), default(IdeoGenerationParms), true));
                Find.FactionManager.Add(court);
            }
            var pawns = new List<Pawn>();
            var nicknames = new HashSet<string>();
            for (int i = 0; i < 100; i++)
            {
                PawnKindDef kind = kinds[i % kinds.Length];
                Faction faction = kind.defName == "WotF_Elf_Colonist" ? Faction.OfPlayer : court;
                Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction,
                    forceGenerateNewPawn: true, canGeneratePawnRelations: false, allowPregnant: false,
                    fixedBiologicalAge: 40f));
                GenSpawn.Spawn(pawn, t.Arena.Cell(2 + i % 40, 2 + i / 40), t.Map);
                t.Arena.Track(pawn);
                pawns.Add(pawn);
                CheckName(t, pawn, kind.defName + " #" + i);
                nicknames.Add(((NameTriple)pawn.Name).Nick);
            }
            int females = pawns.Count(p => p.gender == Gender.Female);
            Record("generated", pawns.Count + ", female=" + females + ", male=" + (pawns.Count - females));
            Record("distinctNicknames", nicknames.Count.ToString());
            Record("kinds", string.Join(",", kinds.Select(k => k.defName)));

            Pawn mother = pawns.First(p => p.kindDef.defName == "WotF_Elf_Colonist");
            mother.drafter.Drafted = true;
            CheckReservedRedraw(t, mother);
            Pawn baby = PregnancyUtility.ApplyBirthOutcome(new RitualOutcomePossibility { positivityIndex = 2 },
                1f, null, null, mother, mother, preventLetter: true) as Pawn;
            t.Expect(baby != null, "ApplyBirthOutcome did not return a live pawn");
            if (baby == null) return;
            t.Arena.Track(baby);
            CheckBabyPlaceholder(t, baby);
            Rand.PushState(baby.thingIDNumber);
            Name suggestion;
            try
            {
                suggestion = PawnBioAndNameGenerator.GeneratePawnName(baby, NameStyle.Full,
                    null, forceNoNick: false, baby.genes?.Xenotype);
            }
            finally { Rand.PopState(); }
            CheckGeneratedName(t, suggestion as NameTriple, "birth letter suggestion");
            CheckBabyPlaceholder(t, baby);
            Record("newbornSuggestedName", suggestion.ToString());
            Record("newbornPlaceholder", baby.Name.ToString());
            CheckReservedBabySuggestion(t, baby);
            t.Expect(baby.DevelopmentalStage.Baby() && baby.ageTracker.AgeBiologicalYears == 0,
                "birth did not produce a newborn: " + baby.DevelopmentalStage);
            t.Expect(baby.GetMother() == mother, "newborn lost its biological mother");
            t.Expect(((NameTriple)baby.Name).Last == ((NameTriple)mother.Name).Last,
                "newborn did not inherit its mother's clan name");

            foreach (Pawn pawn in pawns.Where(p => p != mother)) pawn.Destroy(DestroyMode.Vanish);
            foreach (Pawn pawn in new[] { mother, baby })
            {
                savedIds.Add(pawn.thingIDNumber);
                savedNames.Add(pawn.Name.ToString());
            }
            Record("newbornRawName", baby.Name.ToString());
            Record("motherRawName", mother.Name.ToString());
        }

        protected override void AfterReload(TestContext t)
        {
            for (int i = 0; i < savedIds.Count; i++)
            {
                Pawn pawn = t.Arena.Find(savedIds[i]) as Pawn;
                t.Expect(pawn != null, "saved elf missing after reload: " + savedIds[i]);
                if (pawn == null) continue;
                t.Arena.Track(pawn);
                if (pawn.DevelopmentalStage.Baby()) CheckBabyPlaceholder(t, pawn);
                else CheckName(t, pawn, "reloaded " + savedIds[i]);
                t.Expect(pawn.Name.ToString() == savedNames[i],
                    "Name fields changed across save/load: " + savedNames[i] + " -> " + pawn.Name);
                t.Expect(pawn.story.birthLastName == ((NameTriple)pawn.Name).Last,
                    "birth surname no longer matches the clan name: " + pawn.story.birthLastName);
            }
            t.Note("reloaded", savedIds.Count);
            foreach (string entry in evidence)
            {
                int split = entry.IndexOf('=');
                t.Measure(entry.Substring(0, split), entry.Substring(split + 1));
            }
        }

        private void CheckName(TestContext t, Pawn pawn, string stage)
        {
            t.Expect(pawn.def.defName == "WotF_Elf" && pawn.gender == Gender.Female,
                stage + ": expected female elf; got " + pawn.def.defName + "/" + pawn.gender);
            CheckGeneratedName(t, pawn.Name as NameTriple, stage);
        }

        private void CheckBabyPlaceholder(TestContext t, Pawn baby)
        {
            t.Expect(baby.def.defName == "WotF_Elf" && baby.gender == Gender.Female,
                "newborn is not a female elf: " + baby.def.defName + "/" + baby.gender);
            NameTriple name = baby.Name as NameTriple;
            t.Expect(name != null && name.First == "Baby".Translate().CapitalizeFirst() &&
                clanNames.Contains(name.Last), "newborn lost its vanilla placeholder or clan: " + baby.Name);
        }

        private void CheckGeneratedName(TestContext t, NameTriple name, string stage)
        {
            t.Expect(name != null && name.IsValid, stage + ": invalid NameTriple: " + name);
            if (name == null) return;
            t.Expect(firstNames.Contains(name.First) && clanNames.Contains(name.Last),
                stage + ": name outside approved lists: " + name);
            t.Expect(name.Nick == name.First, stage + ": nickname was not the first name: " + name);
            t.Expect(!reservedNames.Contains(name.First + " " + name.Last),
                stage + ": randomly generated the reserved character: " + name);
        }

        private void CheckReservedBabySuggestion(TestContext t, Pawn baby)
        {
            Name original = baby.Name;
            try
            {
                foreach (string reserved in reservedNames)
                {
                    string[] parts = reserved.Split(' ');
                    baby.Name = new NameTriple("Baby".Translate().CapitalizeFirst(), null, parts[1]);
                    string otherClan = clanNames.First(clan => clan != parts[1]);
                    int matchingSeed = -1;
                    NameTriple baseline = null;
                    for (int seed = 0; seed < 10000; seed++)
                    {
                        Rand.PushState(seed);
                        try
                        {
                            baseline = PawnBioAndNameGenerator.GeneratePawnName(baby, NameStyle.Full,
                                otherClan, forceNoNick: false, baby.genes?.Xenotype) as NameTriple;
                        }
                        finally { Rand.PopState(); }
                        if (baseline != null && baseline.First == parts[0])
                        {
                            matchingSeed = seed;
                            break;
                        }
                    }
                    t.Expect(matchingSeed >= 0, "could not find a seed recommending " + parts[0]);
                    if (matchingSeed < 0) continue;
                    Rand.PushState(matchingSeed);
                    NameTriple suggestion;
                    try
                    {
                        suggestion = PawnBioAndNameGenerator.GeneratePawnName(baby, NameStyle.Full,
                            null, forceNoNick: false, baby.genes?.Xenotype) as NameTriple;
                    }
                    finally { Rand.PopState(); }
                    t.Expect(suggestion != null && suggestion.First != parts[0] && suggestion.Last == parts[1],
                        "birth letter suggested reserved first name or lost existing surname: " + suggestion);
                    CheckGeneratedName(t, suggestion, "reserved-clan birth suggestion");
                    CheckBabyPlaceholder(t, baby);
                    Record("babyReservedRedraw", matchingSeed + ": " + baseline + " -> " + suggestion);
                }
            }
            finally { baby.Name = original; }
        }

        private void CheckReservedRedraw(TestContext t, Pawn pawn)
        {
            RulePackDef maker = DefDatabase<RulePackDef>.GetNamed("WotF_ElfNames");
            foreach (string reserved in reservedNames)
            {
                string last = reserved.Split(' ')[1];
                int matchingSeed = -1;
                string unfiltered = null;
                for (int seed = 0; seed < 10000; seed++)
                {
                    Rand.PushState(seed);
                    try { unfiltered = NameGenerator.GenerateName(maker); }
                    finally { Rand.PopState(); }
                    if (unfiltered == reserved)
                    {
                        matchingSeed = seed;
                        break;
                    }
                }
                t.Expect(matchingSeed >= 0, "could not find a seed that draws reserved name " + reserved);
                if (matchingSeed < 0) continue;
                foreach (string fixedLast in new[] { (string)null, last })
                {
                    Rand.PushState(matchingSeed);
                    NameTriple filtered;
                    try
                    {
                        filtered = PawnBioAndNameGenerator.GenerateFullPawnName(pawn.def,
                            pawn.kindDef.GetNameMaker(pawn.gender), forcedLastName: fixedLast) as NameTriple;
                    }
                    finally { Rand.PopState(); }
                    t.Expect(filtered != null && !reservedNames.Contains(filtered.First + " " + filtered.Last),
                        "reserved name was not redrawn at seed " + matchingSeed + ": " + filtered);
                    Record("redraw." + (fixedLast ?? "random-clan"),
                        matchingSeed + ": " + unfiltered + " -> " + filtered);
                }
            }
        }

        private void Record(string name, string value)
        {
            evidence.Add(name + "=" + value);
        }
    }
}
