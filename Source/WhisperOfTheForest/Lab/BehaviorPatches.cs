using HarmonyLib;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Experimental
{
    /// <summary>
    /// Counts an actual projectile launch for a prepared lab pawn. Queueing AttackStatic is not
    /// enough: only this postfix means a real projectile left the weapon.
    /// </summary>
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Launch), new[]
    {
        typeof(Thing),
        typeof(Vector3),
        typeof(LocalTargetInfo),
        typeof(LocalTargetInfo),
        typeof(ProjectileHitFlags),
        typeof(bool),
        typeof(Thing),
        typeof(ThingDef)
    })]
    internal static class Patch_Projectile_Launch
    {
        private static void Postfix(Thing launcher, LocalTargetInfo intendedTarget)
        {
            if (launcher is Pawn pawn)
            {
                LabBehaviorTracker.RecordShot(pawn, intendedTarget);
            }
        }
    }
}
