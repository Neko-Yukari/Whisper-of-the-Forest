using System.Collections.Generic;
using Verse;
using WhisperOfTheForest.Vfx;

namespace WhisperOfTheForest.Missiles
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

    /// <summary>Def names and lookups used by the missile feature, kept in one place.</summary>
    public static class ArcaneMissileDefs
    {
        public const string ProjectileThingDefName = "WotF_ArcaneMissileProjectile";
        public const string RimUnityVisualDefName = "WotF_Visual_RimUnity";
        public const string NativeVisualDefName = "WotF_Visual_Native";

        /// <summary>
        /// The ThingDef every missile is spawned as. A missing def is reported by the launcher and
        /// no missile is fired; nothing falls back to the old map-owned simulation.
        /// </summary>
        public static ThingDef ProjectileThingDef =>
            DefDatabase<ThingDef>.GetNamedSilentFail(ProjectileThingDefName);
    }
}