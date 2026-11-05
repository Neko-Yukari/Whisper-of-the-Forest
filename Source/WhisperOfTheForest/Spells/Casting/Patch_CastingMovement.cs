using HarmonyLib;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// Channelling slows movement; windup does not. 1.7's <c>Pawn.TicksPerMove(bool)</c> is private
    /// and returns ticks per cell, so a larger result is slower: the Def's factor divides it. Same
    /// patch point as RunAndGun - Continued, which scales the result for its own stance.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), "TicksPerMove")]
    internal static class Patch_Pawn_TicksPerMove_Casting
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, ref float __result)
        {
            float factor = SpellCastingService.MoveFactorFor(__instance);
            if (factor < 1f)
            {
                __result /= factor;
            }
        }
    }

    /// <summary>
    /// Recovery roots the caster: the pather does not advance while the last phase runs, and the
    /// existing path continues by itself afterwards. A stance cannot express this - StanceBusy only
    /// blocks new jobs and attacks and never stops a pawn that is already walking.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.PatherTick))]
    internal static class Patch_Pawn_PathFollower_PatherTick_Casting
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn_PathFollower __instance, Pawn ___pawn)
        {
            return !SpellCastingService.IsRooted(___pawn);
        }
    }
}
