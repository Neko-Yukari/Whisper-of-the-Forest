using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Spells
{
    public abstract class SpellEffect : IExposable
    {
        public abstract string DiagnosticName { get; }
        public abstract int Execute(Pawn caster, Thing primary, IntVec3 cell, Map map);
        public abstract void ExposeData();

        internal static void CheckLoaded(List<SpellEffect> effects, string owner)
        {
            if (Scribe.mode != LoadSaveMode.LoadingVars || effects == null) return;
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                if (effects[i] != null) continue;
                SpellDiagnostics.Error(owner + " contains a null spell effect at index " + i);
                effects.RemoveAt(i);
            }
        }
    }
}
