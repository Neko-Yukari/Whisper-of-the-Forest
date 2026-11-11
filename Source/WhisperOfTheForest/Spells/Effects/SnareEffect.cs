using System.Collections.Generic;
using Verse;

using WhisperOfTheForest.Equipment;

namespace WhisperOfTheForest.Spells
{
    public sealed class SnareEffect : SpellEffect
    {
        private int rootDurationTicks;
        private bool useArea;
        private float targetRadius;

        public SnareEffect()
        {
        }

        public SnareEffect(int rootDurationTicks, bool useArea, float targetRadius)
        {
            this.rootDurationTicks = rootDurationTicks;
            this.useArea = useArea;
            this.targetRadius = targetRadius;
        }
        public override string DiagnosticName => "snare";
        public override int Execute(Pawn caster, Thing primary, IntVec3 cell, Map map)
        {
            List<Thing> targets = SpellExecutor.CollectTargets(useArea, targetRadius, primary, cell, map,
                SpellTargets.CanBeHealed);
            return SpellEffectRuntime.ApplyTimedState(rootDurationTicks, targets, SpellEffectDefs.HediffEnsnare);
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref rootDurationTicks, "rootDurationTicks");
            Scribe_Values.Look(ref useArea, "useArea");
            Scribe_Values.Look(ref targetRadius, "targetRadius");
        }
    }
    public sealed class SnareGlyphDef : EffectGlyphDef
    {
        public int rootDurationTicks;
        public override SpellEffect Compile(IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff, SpellPayload payload = null)
        {
            float area = radius;
            int duration = rootDurationTicks;
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
                    duration = Duration(duration, rootDurationTicks, augment);
            }
            return new SnareEffect(duration, useArea, area);
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (rootDurationTicks <= 0) yield return "Snare needs a positive duration";
            foreach (string error in AreaErrors(radius)) yield return error;
        }

    }
}

