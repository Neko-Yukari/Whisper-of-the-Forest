using System.Collections.Generic;
using WhisperOfTheForest.Equipment;

namespace WhisperOfTheForest.Spells
{
    public abstract class EffectGlyphDef : SpellGlyphDef
    {
        public virtual bool ConsumesRemainder => false;
        public abstract SpellEffect Compile(IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff, SpellPayload payload = null);

        protected static int ScaleTicks(int ticks, float factor)
        {
            return ticks <= 0 ? 0 : System.Math.Max(1, (int)System.Math.Round(ticks * factor));
        }
        protected static int Duration(int ticks, int basis, SpellGlyphDef augment)
        {
            if (augment.defName == SpellDefs.AugmentExtend)
                return ScaleTicks(ticks > 0 ? ticks : basis, augment.factor);
            if (augment.defName == SpellDefs.AugmentShorten && ticks > 0)
                return ScaleTicks(ticks, augment.factor);
            return ticks;
        }
        protected IEnumerable<string> AreaErrors(float radius)
        {
            if (radius < 0f) yield return "radius must not be negative";
            if (AcceptsAugment(SpellDefs.AugmentAoe) && radius <= 0f)
                yield return "an Effect that accepts AOE needs a positive radius";
        }
    }
}
