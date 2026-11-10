using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace WhisperOfTheForest
{
    public enum ElfNameDisplay { Original, Transliteration }

    internal static class ElfNameTranslations
    {
        private static LoadedLanguage language;
        private static readonly Dictionary<string, string> entries = new Dictionary<string, string>();

        internal static void Apply(NameTriple name, ref string result)
        {
            WotfSettings settings = WotfMod.Settings;
            if (settings == null || settings.elfNameDisplay == ElfNameDisplay.Original) return;
            Load();
            // Both halves must be in the table: a non-elf name that happens to match one entry stays as-is.
            if (entries.TryGetValue(name.First, out string first) && entries.TryGetValue(name.Last, out string last))
            {
                result = first + "·" + last;
                if (name.Nick != name.First && name.Nick != name.Last)
                    result = first + " '" + name.Nick + "'·" + last;
            }
        }

        private static void Load()
        {
            if (language == LanguageDatabase.activeLanguage) return;
            language = LanguageDatabase.activeLanguage;
            entries.Clear();
            if (!language.TryGetStringsFromFile("Names/WotF_ElfNameDisplay", out List<string> lines)) return;
            foreach (string line in lines)
            {
                string[] columns = line.Split('|');
                if (columns.Length != 2)
                    throw new FormatException("Invalid elf name translation: " + line);
                entries.Add(columns[0], columns[1]);
            }
        }
    }

    [HarmonyPatch(typeof(NameTriple), nameof(NameTriple.ToStringShort), MethodType.Getter)]
    internal static class Patch_ElfNameShort
    {
        private static void Postfix(NameTriple __instance, ref string __result)
        {
            ElfNameTranslations.Apply(__instance, ref __result);
        }
    }

    [HarmonyPatch(typeof(NameTriple), nameof(NameTriple.ToStringFull), MethodType.Getter)]
    internal static class Patch_ElfNameFull
    {
        private static void Postfix(NameTriple __instance, ref string __result)
        {
            ElfNameTranslations.Apply(__instance, ref __result);
        }
    }
}
