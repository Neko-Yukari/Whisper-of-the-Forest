using RimWorld;
using Verse;

namespace WhisperOfTheForest.AncientRuins
{
    /// <summary>
    /// The loot table one relic casket is filled from. Kept on the building Def so the guaranteed
    /// contents (resonance core, super rune rubbing, material blanks) are data, not code.
    /// </summary>
    public class RelicCasketExtension : DefModExtension
    {
        public ThingSetMakerDef contents;
    }
}
