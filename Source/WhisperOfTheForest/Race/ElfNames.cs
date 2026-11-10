using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Race
{
    internal static class ElfNames
    {
        internal static NameTriple Generate(RulePackDef maker, string lastName)
        {
            // A finite vocabulary cannot satisfy vanilla's globally unique Nick requirement.
            // Allow repeated names instead of exhausting NameGenerator's 150 attempts and logging an error.
            List<string> reserved = maker.GetModExtension<ElfNameRules>().reservedNames;
            NameTriple name;
            string clan;
            do
            {
                name = NameTriple.FromString(NameGenerator.GenerateName(maker));
                clan = lastName ?? name.Last;
            }
            while (reserved.Contains(name.First + " " + clan));
            return new NameTriple(name.First, name.First, clan);
        }
    }

    [HarmonyPatch(typeof(PawnBioAndNameGenerator), nameof(PawnBioAndNameGenerator.GeneratePawnName))]
    internal static class Patch_ElfBabyNameSuggestion
    {
        private static void Prefix(Pawn pawn, ref string forcedLastName)
        {
            if (pawn.def.defName == "WotF_Elf" && forcedLastName == null &&
                pawn.Name is NameTriple name && name.First == "Baby".Translate().CapitalizeFirst())
            {
                forcedLastName = name.Last;
            }
        }
    }

    [HarmonyPatch(typeof(PawnBioAndNameGenerator), nameof(PawnBioAndNameGenerator.GenerateFullPawnName))]
    internal static class Patch_ElfFullName
    {
        private static bool Prefix(ThingDef genFor, RulePackDef pawnKindNameMaker, string forcedLastName, ref Name __result)
        {
            if (genFor.defName != "WotF_Elf") return true;
            __result = ElfNames.Generate(DefDatabase<RulePackDef>.GetNamed("WotF_ElfNames"), forcedLastName);
            return false;
        }
    }

    [HarmonyPatch(typeof(PawnBioAndNameGenerator), nameof(PawnBioAndNameGenerator.GiveAppropriateBioAndNameTo))]
    internal static class Patch_ElfBioName
    {
        private static void Postfix(Pawn pawn, PawnGenerationRequest request)
        {
            if (pawn.def.defName != "WotF_Elf") return;
            // The birth letter recognizes this placeholder and asks GeneratePawnName for a suggestion.
            if (pawn.DevelopmentalStage.Baby() && pawn.Faction.IsPlayerSafe()) return;
            // Solid biographies bypass GenerateFullPawnName. Keep those biographies, not their names.
            RulePackDef maker = DefDatabase<RulePackDef>.GetNamed("WotF_ElfNames");
            Translator.TryGetTranslatedStringsForFile("Names/WotF_ElfFemale", out List<string> firstNames);
            Translator.TryGetTranslatedStringsForFile("Names/WotF_ElfClan", out List<string> clans);
            if (!(pawn.Name is NameTriple name) || !name.NickSet || name.Nick != name.First ||
                !firstNames.Contains(name.First) || !clans.Contains(name.Last) ||
                maker.GetModExtension<ElfNameRules>().reservedNames.Contains(name.First + " " + name.Last))
            {
                pawn.Name = ElfNames.Generate(maker, request.FixedLastName);
            }
        }
    }
}
