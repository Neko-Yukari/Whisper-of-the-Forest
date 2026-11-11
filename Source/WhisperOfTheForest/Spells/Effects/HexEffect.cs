using System.Collections.Generic;
using Verse;

using WhisperOfTheForest.Equipment;

namespace WhisperOfTheForest.Spells
{
    public sealed class HexEffect : SpellEffect
    {
        private int hexDurationTicks;
        private bool useArea;
        private float targetRadius;

        public HexEffect()
        {
        }

        public HexEffect(int hexDurationTicks, bool useArea, float targetRadius)
        {
            this.hexDurationTicks = hexDurationTicks;
            this.useArea = useArea;
            this.targetRadius = targetRadius;
        }
        public override string DiagnosticName => "hex";
        public override int Execute(Pawn caster, Thing primary, IntVec3 cell, Map map)
        {
            List<Thing> targets = SpellExecutor.CollectTargets(useArea, targetRadius, primary, cell, map,
                SpellTargets.CanBeHealed);
            return SpellEffectRuntime.ApplyTimedState(hexDurationTicks, targets, SpellEffectDefs.HediffHex);
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref hexDurationTicks, "hexDurationTicks");
            Scribe_Values.Look(ref useArea, "useArea");
            Scribe_Values.Look(ref targetRadius, "targetRadius");
        }
    }
    public sealed class HexGlyphDef : EffectGlyphDef
    {
        public int hexDurationTicks;
        public override SpellEffect Compile(IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff, SpellPayload payload = null)
        {
            float area = radius;
            int duration = hexDurationTicks;
            bool useArea = false;
            for (int i = 0; i < augments.Count; i++)
            {
                SpellGlyphDef augment = augments[i];
                if (augment.defName == SpellDefs.AugmentAoe)
                {
                    useArea = true;
                    area += augment.radius;
                }
                else if (augment.defName == SpellDefs.AugmentExtend || augment.defName == SpellDefs.AugmentShorten)
                    duration = Duration(duration, hexDurationTicks, augment);
            }
            return new HexEffect(duration, useArea, area);
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (hexDurationTicks <= 0) yield return "Hex needs a positive duration";
            foreach (string error in AreaErrors(radius)) yield return error;
        }

    }
}

