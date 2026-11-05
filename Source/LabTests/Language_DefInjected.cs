using System.Collections.Generic;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// The mod's translations reach its own Def types. A DefInjected folder must name the Def type
    /// the way the game resolves it; a bare name only resolves inside Verse/RimWorld, so a folder
    /// for a mod type needs the full name. A wrong folder is skipped silently: the game only records
    /// it in the language's load errors, never in the log.
    /// </summary>
    [LabTest]
    public sealed class Language_DefInjected : LabTest
    {
        public override string Name => "language.definjected-applies";

        protected override void Run(TestContext t)
        {
            LoadedLanguage language = LanguageDatabase.activeLanguage;
            t.Measure("language", language.folderName);

            var errors = new List<string>();
            Collect(language.loadErrors, errors);
            foreach (DefInjectionPackage package in language.defInjections)
            {
                Collect(package.loadErrors, errors);
            }

            t.Note("ownLoadErrors", errors.Count);
            if (errors.Count > 0)
            {
                t.Measure("firstLoadError", errors[0]);
            }

            t.Expect(errors.Count == 0, "the active language reported load errors for this mod's translations");

            string pierce = TestContext.Glyph(SpellDefs.AugmentPierce).label;
            string bloodline = DefDatabase<Race.WotF_ForestBloodlineGeneDef>.AllDefsListForReading[0].label;
            t.Measure("pierceLabel", pierce);
            t.Measure("bloodlineLabel", bloodline);

            if (language.folderName == "ChineseSimplified")
            {
                t.Expect(pierce == "穿透", "pierce label is not the ChineseSimplified translation: " + pierce);
                t.Expect(bloodline == "林庭血脉", "bloodline gene label is not the ChineseSimplified translation: " + bloodline);
            }
        }

        private static void Collect(List<string> source, List<string> into)
        {
            foreach (string error in source)
            {
                if (error.Contains("Whisper-of-the-Forest") || error.Contains("WotF_"))
                {
                    into.Add(error);
                }
            }
        }
    }
}
