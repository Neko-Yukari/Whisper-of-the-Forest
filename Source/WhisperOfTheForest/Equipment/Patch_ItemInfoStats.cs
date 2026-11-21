using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using WhisperOfTheForest.Movement;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Equipment
{
    /// <summary>
    /// Surfaces custom Def-level item attributes that do not belong to a custom ThingComp
    /// (spellbook slot counts / super-rune capability, and terrain stride mantle) in vanilla's
    /// item info card.
    /// </summary>
    [HarmonyPatch(typeof(ThingDef), nameof(ThingDef.SpecialDisplayStats))]
    internal static class Patch_ThingDef_SpecialDisplayStats
    {
        private static IEnumerable<StatDrawEntry> Postfix(
            IEnumerable<StatDrawEntry> __result,
            ThingDef __instance,
            StatRequest req)
        {
            foreach (StatDrawEntry entry in __result)
            {
                yield return entry;
            }

            SpellbookExtension book = __instance.GetModExtension<SpellbookExtension>();
            if (book != null)
            {
                yield return new StatDrawEntry(
                    StatCategoryDefOf.Apparel,
                    "WotF_Stat_SpellbookMaxGlyphs".Translate(),
                    book.maxGlyphs.ToString(),
                    "WotF_Stat_SpellbookMaxGlyphs_Desc".Translate(),
                    2760);

                yield return new StatDrawEntry(
                    StatCategoryDefOf.Apparel,
                    "WotF_Stat_SpellbookSuperGlyphs".Translate(),
                    book.allowsSuperGlyphs ? "Yes".Translate() : "No".Translate(),
                    "WotF_Stat_SpellbookSuperGlyphs_Desc".Translate(),
                    2759);
            }

            if (__instance.defName == StrideApparel.TerrainStrideDefName)
            {
                yield return new StatDrawEntry(
                    StatCategoryDefOf.Apparel,
                    "WotF_Stat_TerrainStride".Translate(),
                    "WotF_Stat_TerrainStride_Val".Translate(),
                    "WotF_Stat_TerrainStride_Desc".Translate(),
                    2758);
            }
        }
    }
}
