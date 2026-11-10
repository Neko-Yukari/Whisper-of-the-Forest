using HarmonyLib;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Spells
{
    [HarmonyPatch(typeof(CompOverseerSubject), nameof(CompOverseerSubject.State), MethodType.Getter)]
    internal static class Patch_MechHackOverseerState
    {
        private static void Postfix(CompOverseerSubject __instance, ref OverseerSubjectState __result)
        {
            if (MechHackService.IsTemporarilyControlled(__instance.Parent))
                __result = OverseerSubjectState.Overseen;
        }
    }

    [HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.ShowDraftGizmo), MethodType.Getter)]
    internal static class Patch_MechHackDraftGizmo
    {
        private static void Postfix(Pawn_DraftController __instance, ref bool __result)
        {
            if (MechHackService.IsTemporarilyControlled(__instance.pawn)) __result = true;
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.CanDraftMech))]
    internal static class Patch_MechHackCanDraft
    {
        private static void Postfix(Pawn mech, ref AcceptanceReport __result)
        {
            if (MechHackService.IsTemporarilyControlled(mech)
                && mech.needs.energy?.IsLowEnergySelfShutdown != true) __result = true;
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.InMechanitorCommandRange))]
    internal static class Patch_MechHackCommandRange
    {
        private static void Postfix(Pawn mech, LocalTargetInfo target, ref bool __result)
        {
            if (MechHackService.IsTemporarilyControlled(mech))
                __result = mech.Spawned && target.IsValid && target.Cell.InBounds(mech.Map)
                    && (!target.HasThing || target.Thing.Map == mech.Map);
        }
    }
}
