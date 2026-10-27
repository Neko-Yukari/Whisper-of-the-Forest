using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Experimental
{
    /// <summary>
    /// Puts the lab entry into the vanilla play settings row - the toggle strip that sits directly
    /// above the bottom main-button bar - as a real WidgetRow element, so it follows the game's own
    /// icon size, spacing, direction and wrapping instead of a hardcoded screen position.
    ///
    /// GlobalControlsUtility.DoPlaySettings(WidgetRow rowVisibility, bool worldView, ref float curBaseY)
    /// initialises the row and stores rowVisibility.FinalY in curBaseY. The postfix appends an icon
    /// through the native row API and returns the recomputed row end in curBaseY,
    /// so a wrapped extra row still shifts the time controls instead of overlapping them.
    ///
    /// The same postfix records where the row ended up; the floating entry uses that as its default
    /// position (and as a cheap diagnostic for the lab: RowEntryDraws / LastRowLeftX / LastRowTopY).
    /// </summary>
    [HarmonyPatch(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DoPlaySettings))]
    public static class Patch_PlaySettingsRowEntry
    {
        public static bool HasRow { get; private set; }
        public static float LastRowLeftX { get; private set; }
        public static float LastRowTopY { get; private set; }
        public static int LastScreenWidth { get; private set; }
        public static int LastScreenHeight { get; private set; }

        /// <summary>GUI passes in which the native entry icon was drawn (several per frame).</summary>
        public static int RowEntryDraws { get; private set; }

        public static void Postfix(WidgetRow rowVisibility, bool worldView, ref float curBaseY)
        {
            if (worldView || rowVisibility == null)
            {
                return;
            }

            // Record the vanilla block end before appending our icon, so the floating entry default
            // stays tied to the vanilla controls even if our icon happens to wrap the row.
            LastRowLeftX = rowVisibility.FinalX;
            LastRowTopY = rowVisibility.FinalY;
            LastScreenWidth = UI.screenWidth;
            LastScreenHeight = UI.screenHeight;
            HasRow = true;

            Texture2D icon = TechLabStyle.Icon;
            if (icon != null)
            {
                TechLabSettings settings = TechLabMod.Settings;
                bool entryHidden = settings != null && !settings.showFloatingEntry;
                string tooltip = TechLabUi.T(entryHidden
                    ? "WotF_UI.RowEntryTooltipRestore"
                    : "WotF_UI.RowEntryTooltip");

                using (TechLabDrawScope.Begin())
                {
                    RowEntryDraws++;
                    if (rowVisibility.ButtonIcon(icon, tooltip, null, null, null, true, 0f))
                    {
                        TechLabUi.OpenWindowAndRestoreEntry();
                    }
                }
            }

            // DoPlaySettings assigned curBaseY before this postfix ran; our extra icon may have
            // wrapped the row upward, so hand the new end back to the caller (time controls, date).
            curBaseY = rowVisibility.FinalY;
        }
    }
}
