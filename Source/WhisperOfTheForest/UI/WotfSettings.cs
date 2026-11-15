using Verse;

namespace WhisperOfTheForest
{
    /// <summary>Profile-wide settings of the mod. Only what outlives a session belongs here.</summary>
    public class WotfSettings : ModSettings
    {
        /// <summary>Per-object log detail; see <see cref="WotfLog.DebugEnabled"/>.</summary>
        public bool logDebug;

        /// <summary>
        /// Opens every rune that normally needs an unlock; opt-in development override.
        /// </summary>
        public bool unlockAllGlyphs = false;

        /// <summary>
        /// Casts spend neither mana nor neural heat. Development switch so a spell can be tried
        /// repeatedly without waiting for regeneration.
        /// </summary>
        public bool freeCasting = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref logDebug, "logDebug", false);
            Scribe_Values.Look(ref unlockAllGlyphs, "unlockAllGlyphs", false);
            Scribe_Values.Look(ref freeCasting, "freeCasting", false);
        }
    }
}
