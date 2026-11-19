using System.Collections.Generic;
using Verse;

using WhisperOfTheForest.Equipment;

namespace WhisperOfTheForest.Spells
{
    public sealed class LingerEffect : SpellEffect
    {
        private SpellPayload payload;
        private int lifetimeTicks;
        private float fieldRadius;
        private int pulseIntervalTicks;

        public LingerEffect()
        {
        }

        public LingerEffect(SpellPayload payload, int lifetimeTicks, float fieldRadius, int pulseIntervalTicks)
        {
            this.payload = payload;
            this.lifetimeTicks = lifetimeTicks;
            this.fieldRadius = fieldRadius;
            this.pulseIntervalTicks = pulseIntervalTicks;
        }
        public override string DiagnosticName => "linger";
        public override int Execute(Thing caster, Thing primary, IntVec3 cell, Map map)
        {
            return SpellEffectRuntime.SpawnField(payload, lifetimeTicks, fieldRadius, pulseIntervalTicks,
                SpellFieldMode.Linger, caster, cell, map);
        }
        public override void ExposeData()
        {
            Scribe_Deep.Look(ref payload, "payload");
            Scribe_Values.Look(ref lifetimeTicks, "lifetimeTicks");
            Scribe_Values.Look(ref fieldRadius, "fieldRadius");
            Scribe_Values.Look(ref pulseIntervalTicks, "pulseIntervalTicks");
        }
    }

    public sealed class LingerGlyphDef : EffectGlyphDef
    {
        public override bool ConsumesRemainder => true;
        public int pulseIntervalTicks;
        public int lifetimeTicks;
        public override SpellEffect Compile(IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff, SpellPayload payload = null)
        {
            float area = radius;
            int duration = lifetimeTicks;
            int interval = pulseIntervalTicks;
            for (int i = 0; i < augments.Count; i++)
            {
                SpellGlyphDef augment = augments[i];
                if (augment.defName == SpellDefs.AugmentAoe) area += augment.radius;
                else if (augment.defName == SpellDefs.AugmentExtend || augment.defName == SpellDefs.AugmentShorten)
                    duration = Duration(duration, lifetimeTicks, augment);
                else if (augment.defName == SpellDefs.AugmentAccelerate
                         || augment.defName == SpellDefs.AugmentDecelerate)
                {
                    float factor = augment.factor > 0f ? augment.factor : 1f;
                    interval = ScaleTicks(interval, 1f / factor);
                }
            }
            return new LingerEffect(payload, duration, area, interval);
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (radius <= 0f) yield return "Linger needs a positive radius";
            if (pulseIntervalTicks <= 0) yield return "Linger needs a positive pulseIntervalTicks";
            if (lifetimeTicks <= 0) yield return "Linger needs a positive lifetimeTicks";
        }
    }
}

