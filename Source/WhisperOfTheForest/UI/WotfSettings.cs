using Verse;

namespace WhisperOfTheForest.Experimental
{
    /// <summary>Profile-wide settings of the mod. Only what outlives a session belongs here.</summary>
    public class WotfSettings : ModSettings
    {
        /// <summary>Per-object log detail; see <see cref="WotfLog.DebugEnabled"/>.</summary>
        public bool logDebug;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref logDebug, "logDebug", false);
        }
    }
}
