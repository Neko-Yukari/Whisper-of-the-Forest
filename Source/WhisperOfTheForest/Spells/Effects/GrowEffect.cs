using System.Collections.Generic;
using UnityEngine;
using Verse;

using WhisperOfTheForest.Equipment;

namespace WhisperOfTheForest.Spells
{
    public sealed class GrowEffect : SpellEffect
    {
        private float growthFactor;
        private int fertilityDurationTicks;
        private bool useArea;
        private float soilRadius;

        public GrowEffect()
        {
        }

        public GrowEffect(float growthFactor, int fertilityDurationTicks, bool useArea, float soilRadius)
        {
            this.growthFactor = growthFactor;
            this.fertilityDurationTicks = fertilityDurationTicks;
            this.useArea = useArea;
            this.soilRadius = soilRadius;
        }
        public override string DiagnosticName => "grow";
        public override SoundDef ResolveSound => SpellDefs.Glyph(SpellDefs.EffectGrow)?.resolveSound;
        public override void AppendSummary(List<string> lines, int indent)
        {
            lines.Add(SummaryIndent(indent) + SpellText.TF("WotF_SpellPlan.EffectGrow",
                growthFactor.ToString("0.##"), FormatSeconds(fertilityDurationTicks), FormatAreaSuffix(useArea, soilRadius)));
        }
        public override int Execute(Thing caster, Thing primary, IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map) || fertilityDurationTicks <= 0 || growthFactor <= 1f) return 0;
            if (!useArea)
            {
                MapComponent_ForestFertility.Register(map, cell, fertilityDurationTicks, growthFactor);
                return 1;
            }
            int affected = 0;
            foreach (IntVec3 target in GenRadial.RadialCellsAround(cell, soilRadius, true))
            {
                if (!target.InBounds(map)) continue;
                MapComponent_ForestFertility.Register(map, target, fertilityDurationTicks, growthFactor);
                affected++;
            }
            return affected;
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref growthFactor, "growthFactor");
            Scribe_Values.Look(ref fertilityDurationTicks, "fertilityDurationTicks");
            Scribe_Values.Look(ref useArea, "useArea");
            Scribe_Values.Look(ref soilRadius, "soilRadius");
        }
    }

    public sealed class GrowGlyphDef : EffectGlyphDef
    {
        public float growthRate;
        public int fertilityDurationTicks;
        public override SpellEffect Compile(IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff, SpellPayload payload = null)
        {
            float magnitude = 1f;
            float area = radius;
            int duration = fertilityDurationTicks;
            bool useArea = false;
            for (int i = 0; i < augments.Count; i++)
            {
                SpellGlyphDef augment = augments[i];
                if (augment.defName == SpellDefs.AugmentAmplify || augment.defName == SpellDefs.AugmentDampen)
                    magnitude *= augment.factor;
                else if (augment.defName == SpellDefs.AugmentExtend || augment.defName == SpellDefs.AugmentShorten)
                    duration = Duration(duration, fertilityDurationTicks, augment);
                else if (augment.defName == SpellDefs.AugmentAoe)
                {
                    useArea = true;
                    area += augment.radius;
                }
            }
            if (duration > 0 && staff != null)
                duration = Mathf.Max(1, Mathf.RoundToInt(duration * staff.durationFactor));
            float factor = growthRate > 0f ? growthRate * Mathf.Max(1f, magnitude) : 0f;
            return new GrowEffect(factor, duration, useArea, area);
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (growthRate <= 1f) yield return "Grow needs a growthRate above 1";
            if (fertilityDurationTicks <= 0) yield return "fertilityDurationTicks must be positive";
            foreach (string error in AreaErrors(radius)) yield return error;
        }
    }
}

