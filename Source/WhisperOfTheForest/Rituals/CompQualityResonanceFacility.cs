using RimWorld;

namespace WhisperOfTheForest.Rituals
{
    public sealed class CompProperties_QualityResonanceFacility : CompProperties_FacilityQualityBased
    {
        public CompProperties_QualityResonanceFacility()
        {
            compClass = typeof(CompQualityResonanceFacility);
        }
    }

    /// <summary>
    /// Vanilla CompFacilityQualityBased only populates its statOffsets list in PostQualitySet and
    /// PostLoadInit (RimWorld/CompFacilityQualityBased.cs:14-44). When a statue is placed via dev/god
    /// mode, CompQuality.SetQuality is never called, leaving statOffsets empty (zero resonance bonus
    /// and a leading blank line in CompFacility.CompInspectStringExtra:214). Populate on spawn first.
    /// </summary>
    public sealed class CompQualityResonanceFacility : CompFacilityQualityBased
    {
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (StatOffsets.Count == 0)
            {
                PostQualitySet();
            }

            base.PostSpawnSetup(respawningAfterLoad);
        }
    }
}
