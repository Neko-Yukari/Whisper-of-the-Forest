using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Shields
{
    [HarmonyPatch(typeof(CompProjectileInterceptor), nameof(CompProjectileInterceptor.CheckIntercept))]
    internal static class Patch_CompProjectileInterceptor_CheckIntercept
    {
        [HarmonyPrefix]
        private static bool Prefix(
            CompProjectileInterceptor __instance,
            Projectile projectile,
            Vector3 lastExactPos,
            Vector3 newExactPos,
            ref bool __result)
        {
            if (!(__instance is CompWotfShield shield))
            {
                return true;
            }

            __result = shield.TryInterceptVanillaProjectile(projectile, lastExactPos, newExactPos);
            return false;
        }
    }

    [HarmonyPatch(typeof(CompProjectileInterceptor), nameof(CompProjectileInterceptor.CheckBombardmentIntercept))]
    internal static class Patch_CompProjectileInterceptor_CheckBombardmentIntercept
    {
        [HarmonyPrefix]
        private static bool Prefix(
            CompProjectileInterceptor __instance,
            Bombardment bombardment,
            Bombardment.BombardmentProjectile projectile,
            ref bool __result)
        {
            if (!(__instance is CompWotfShield shield))
            {
                return true;
            }

            __result = shield.TryInterceptBombardment(bombardment, projectile);
            return false;
        }
    }

    [HarmonyPatch(typeof(CompProjectileInterceptor), nameof(CompProjectileInterceptor.BombardmentCanStartFireAt))]
    internal static class Patch_CompProjectileInterceptor_BombardmentCanStartFireAt
    {
        [HarmonyPrefix]
        private static bool Prefix(
            CompProjectileInterceptor __instance,
            Bombardment bombardment,
            IntVec3 cell,
            ref bool __result)
        {
            if (!(__instance is CompWotfShield shield))
            {
                return true;
            }

            __result = shield.CanBombardmentStartFireAt(bombardment, cell);
            return false;
        }
    }
}
