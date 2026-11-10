using Verse;

namespace WhisperOfTheForest.Rituals
{
    public class CompProperties_ChalkMark : CompProperties
    {
        public ChalkColor color = ChalkColor.White;

        public CompProperties_ChalkMark()
        {
            compClass = typeof(CompChalkMark);
        }
    }

    /// <summary>Marker comp for a drawn chalk mark. The mark carries no state; the color lives in the Def.</summary>
    public class CompChalkMark : ThingComp
    {
        public ChalkColor Color => ((CompProperties_ChalkMark)props).color;
    }
}
