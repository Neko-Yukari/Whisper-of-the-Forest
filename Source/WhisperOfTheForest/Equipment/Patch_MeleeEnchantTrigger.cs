using HarmonyLib;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Equipment
{
    /// <summary>
    /// A melee hit applies its damage inside TryCastShot and returns true only on a real hit
    /// (RimWorld/Verb_MeleeAttack.cs:56-80), so the postfix is the first and only place that runs
    /// after the damage has resolved. Vanilla melee verbs all inherit this one method: the damage
    /// subclass only overrides ApplyMeleeDamageToTarget.
    /// </summary>
    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    internal static class Patch_MeleeEnchantTrigger
    {
        private static void Postfix(Verb_MeleeAttack __instance, bool __result)
        {
            if (!__result)
            {
                return;
            }

            CompEnchantedEquipment enchant = __instance.EquipmentSource?.GetComp<CompEnchantedEquipment>();
            if (enchant == null || !enchant.HasSpell)
            {
                return;
            }

            Thing target = __instance.CurrentTarget.Thing;
            if (target == null || !target.Spawned || target.Destroyed)
            {
                return;
            }

            if (target is Pawn pawn && pawn.Dead)
            {
                return;
            }

            enchant.TriggerOnHit(__instance.CasterPawn, target);
        }
    }
}
