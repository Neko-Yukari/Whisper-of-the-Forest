using RimWorld;

namespace WhisperOfTheForest.Experimental.Abilities
{
    /// <summary>
    /// Behaviour-free XML base for every Whisper of the Forest ability effect. It exists so the lab
    /// and any other tooling can discover the experiment psycasts with one type check
    /// (<c>def.comps.OfType&lt;CompProperties_WotFAbilityEffect&gt;()</c>) instead of keeping a
    /// hand-written list of concrete comp properties.
    /// </summary>
    public class CompProperties_WotFAbilityEffect : CompProperties_AbilityEffect
    {
    }
}
