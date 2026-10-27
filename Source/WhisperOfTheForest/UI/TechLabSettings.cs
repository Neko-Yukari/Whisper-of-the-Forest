using Verse;

namespace WhisperOfTheForest.Experimental
{
    /// <summary>
    /// Global (profile-wide) UI preferences for the experimental tech lab.
    /// Window open/closed state is deliberately not stored here: the lab window
    /// must always start closed after loading a game.
    /// </summary>
    public class TechLabSettings : ModSettings
    {
        public bool showFloatingEntry = true;
        public bool hasCustomEntryPosition;
        public float entryX = -1f;
        public float entryY = -1f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref showFloatingEntry, "showFloatingEntry", true);
            Scribe_Values.Look(ref hasCustomEntryPosition, "hasCustomEntryPosition", false);
            Scribe_Values.Look(ref entryX, "entryX", -1f);
            Scribe_Values.Look(ref entryY, "entryY", -1f);
        }
    }
}
