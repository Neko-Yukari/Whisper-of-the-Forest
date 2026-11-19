using System.Collections.Generic;
using Verse;

using WhisperOfTheForest.Equipment;

namespace WhisperOfTheForest.Spells
{
    public sealed class FreezeEffect : SpellEffect
    {
        private int freezeDurationTicks;
        private bool useArea;
        private float targetRadius;

        public FreezeEffect()
        {
        }

        public FreezeEffect(int freezeDurationTicks, bool useArea, float targetRadius)
        {
            this.freezeDurationTicks = freezeDurationTicks;
            this.useArea = useArea;
            this.targetRadius = targetRadius;
        }
        public override string DiagnosticName => "freeze";
        public override int Execute(Thing caster, Thing primary, IntVec3 cell, Map map)
        {
            List<Thing> targets = SpellExecutor.CollectTargets(useArea, targetRadius, primary, cell, map,
                SpellTargets.CanBeHealed);
            return SpellEffectRuntime.ApplyTimedState(freezeDurationTicks, targets, SpellEffectDefs.HediffFreeze);
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref freezeDurationTicks, "freezeDurationTicks");
            Scribe_Values.Look(ref useArea, "useArea");
            Scribe_Values.Look(ref targetRadius, "targetRadius");
        }
    }
    public sealed class FreezeGlyphDef : EffectGlyphDef
    {
        public int freezeDurationTicks;
        public override SpellEffect Compile(IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff, SpellPayload payload = null)
        {
            float area = radius;
            int duration = freezeDurationTicks;
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
                    duration = Duration(duration, freezeDurationTicks, augment);
            }
            return new FreezeEffect(duration, useArea, area);
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (freezeDurationTicks <= 0) yield return "Freeze needs a positive duration";
            foreach (string error in AreaErrors(radius)) yield return error;
        }

    }
}

