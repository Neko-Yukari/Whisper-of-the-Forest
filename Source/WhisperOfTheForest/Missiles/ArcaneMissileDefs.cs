using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Experimental.Vfx;

namespace WhisperOfTheForest.Experimental.Missiles
{
    /// <summary>Missile-layer logging: the shared leveled entry with the missile module tag.</summary>
    public static class ArcaneMissileDiagnostics
    {
        public static void Error(string message)
        {
            WotfLog.Error(WotfLog.ModuleMissile, message);
        }

        public static void Warning(string message)
        {
            WotfLog.Warning(WotfLog.ModuleMissile, message);
        }

        public static void Info(string message)
        {
            WotfLog.Info(WotfLog.ModuleMissile, message);
        }

        public static void Debug(string message)
        {
            WotfLog.Debug(WotfLog.ModuleMissile, message);
        }
    }

    /// <summary>
    /// Selects how one missile is drawn. The Def only names a lifecycle class, so a new backend is a
    /// new <see cref="VfxWorker"/> plus a Def - the missile core never learns about it. It is a
    /// <see cref="VfxDef"/> like the railgun's: the missile layer owns no visual framework of its own.
    /// </summary>
    public class ArcaneMissileVisualDef : VfxDef
    {
        /// <summary>Effect played where a missile of this backend detonates.</summary>
        public string impactVisualDefName;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (impactVisualDefName.NullOrEmpty())
            {
                yield return "impactVisualDefName is empty; a detonating missile would have no effect";
            }
        }
    }

    /// <summary>
    /// Selects the hit strategy. A new event is a new <see cref="ArcaneMissileImpactWorker"/> subclass
    /// plus a Def with that type in workerClass; no switch statement has to change.
    /// </summary>
    public class ArcaneMissileImpactDef : Def
    {
        public Type workerClass;

        [Unsaved] private ArcaneMissileImpactWorker worker;
        [Unsaved] private bool failureLogged;

        /// <summary>Workers are stateless and shared by every missile using this Def.</summary>
        public ArcaneMissileImpactWorker GetWorker()
        {
            if (worker != null)
            {
                return worker;
            }

            if (workerClass == null || !typeof(ArcaneMissileImpactWorker).IsAssignableFrom(workerClass))
            {
                if (!failureLogged)
                {
                    failureLogged = true;
                    ArcaneMissileDiagnostics.Error(
                        "impact def " + defName + " has an invalid workerClass (" + (workerClass?.FullName ?? "null")
                        + "); it must derive from " + typeof(ArcaneMissileImpactWorker).FullName
                        + ". Impacts will do nothing instead of falling back to the enum.");
                }

                return null;
            }

            try
            {
                worker = (ArcaneMissileImpactWorker)Activator.CreateInstance(workerClass);
                return worker;
            }
            catch (Exception exception)
            {
                if (!failureLogged)
                {
                    failureLogged = true;
                    ArcaneMissileDiagnostics.Error(
                        "impact def " + defName + " could not create " + workerClass.FullName + ": " + exception);
                }

                return null;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (workerClass == null)
            {
                yield return "workerClass is null";
            }
            else if (!typeof(ArcaneMissileImpactWorker).IsAssignableFrom(workerClass))
            {
                yield return "workerClass must derive from " + typeof(ArcaneMissileImpactWorker).FullName;
            }
        }
    }

    /// <summary>Def names and lookups used by the missile feature, kept in one place.</summary>
    public static class ArcaneMissileDefs
    {
        public const string ProjectileThingDefName = "WotF_ArcaneMissileProjectile";
        public const string RimUnityVisualDefName = "WotF_Visual_RimUnity";
        public const string NativeVisualDefName = "WotF_Visual_Native";
        public const string DamageImpactDefName = "WotF_Impact_Damage";
        public const string HealImpactDefName = "WotF_Impact_Heal";
        public const string ExplodeImpactDefName = "WotF_Impact_Explode";
        public const string ArcaneForceDamageDefName = "WotF_ArcaneMissileDamage";

        public static DamageDef ArcaneForce =>
            DefDatabase<DamageDef>.GetNamedSilentFail(ArcaneForceDamageDefName) ?? DamageDefOf.Blunt;

        /// <summary>
        /// The ThingDef every missile is spawned as. A missing def is reported by the launcher and
        /// no missile is fired; nothing falls back to the old map-owned simulation.
        /// </summary>
        public static ThingDef ProjectileThingDef =>
            DefDatabase<ThingDef>.GetNamedSilentFail(ProjectileThingDefName);

        /// <summary>The documented default backend; used only when settings.visualDef is null.</summary>
        public static ArcaneMissileVisualDef DefaultVisual =>
            DefDatabase<ArcaneMissileVisualDef>.GetNamedSilentFail(RimUnityVisualDefName);

        public static ArcaneMissileVisualDef VisualByName(string defName)
        {
            return string.IsNullOrEmpty(defName) ? null : DefDatabase<ArcaneMissileVisualDef>.GetNamedSilentFail(defName);
        }

        public static string ImpactDefNameFor(ArcaneMissileImpactMode mode)
        {
            switch (mode)
            {
                case ArcaneMissileImpactMode.Heal:
                    return HealImpactDefName;
                case ArcaneMissileImpactMode.Explode:
                    return ExplodeImpactDefName;
                default:
                    return DamageImpactDefName;
            }
        }

        public static ArcaneMissileImpactDef ImpactForMode(ArcaneMissileImpactMode mode)
        {
            return DefDatabase<ArcaneMissileImpactDef>.GetNamedSilentFail(ImpactDefNameFor(mode));
        }
    }
}
