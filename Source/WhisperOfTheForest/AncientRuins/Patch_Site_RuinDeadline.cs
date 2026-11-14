using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WhisperOfTheForest.AncientRuins
{
    /// <summary>
    /// Shows the ruin's forced-exit deadline on the site's inspect pane. The clock lives in
    /// <see cref="GameComponent_RuinDeadlines"/> and keeps running after the quest ended, so it cannot
    /// ride on a quest part: vanilla appends only site parts' threat labels there
    /// (Site.cs:514-539), hence this one-line postfix.
    /// </summary>
    [HarmonyPatch(typeof(Site), nameof(Site.GetInspectString))]
    internal static class Patch_Site_RuinDeadline
    {
        private static void Postfix(Site __instance, ref string __result)
        {
            RuinDeadline deadline = GameComponent_RuinDeadlines.Current?.For(__instance);
            if (deadline == null || !deadline.Started || deadline.Exited)
            {
                return;
            }

            string line = string.Format("WotF_Ruin_ExitCountdown".Translate(),
                deadline.TicksLeft.ToStringTicksToPeriod());
            __result = __result.NullOrEmpty() ? line : __result + "\n" + line;
        }
    }
}
