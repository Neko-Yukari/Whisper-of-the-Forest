using System.Collections.Generic;
using Verse;

using WhisperOfTheForest.Equipment;

namespace WhisperOfTheForest.Spells
{
    public sealed class HackEffect : SpellEffect
    {
        private float hackStrength;
        private bool useArea;
        private float targetRadius;
        private float baseTicks;
        private int minTicks;
        private int maxTicks;
        private int controlTicks;

        public HackEffect()
        {
        }
        public HackEffect(float hackStrength, bool useArea, float targetRadius, float baseTicks,
            int minTicks, int maxTicks, int controlTicks)
        {
            this.hackStrength = hackStrength;
            this.useArea = useArea;
            this.targetRadius = targetRadius;
            this.baseTicks = baseTicks;
            this.minTicks = minTicks;
            this.maxTicks = maxTicks;
            this.controlTicks = controlTicks;
        }
        public float HackStrength => hackStrength;
        public bool UseArea => useArea;
        public float TargetRadius => targetRadius;
        public float BaseTicks => baseTicks;
        public int MinTicks => minTicks;
        public int MaxTicks => maxTicks;
        public int ControlTicks => controlTicks;
        public override string DiagnosticName => "hack";
        public override void AppendSummary(List<string> lines, int indent)
        {
            lines.Add(SummaryIndent(indent) + SpellText.TF("WotF_SpellPlan.EffectHack",
                hackStrength.ToString("0.#"), FormatSeconds(controlTicks), FormatAreaSuffix(useArea, targetRadius)));
        }
        public override int Execute(Thing caster, Thing primary, IntVec3 cell, Map map)
        {
            return MechHackService.Apply(this, caster as Pawn, primary, cell, map);
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref hackStrength, "hackStrength");
            Scribe_Values.Look(ref useArea, "useArea");
            Scribe_Values.Look(ref targetRadius, "targetRadius");
            Scribe_Values.Look(ref baseTicks, "baseTicks");
            Scribe_Values.Look(ref minTicks, "minTicks");
            Scribe_Values.Look(ref maxTicks, "maxTicks");
            Scribe_Values.Look(ref controlTicks, "controlTicks");
        }
    }
    public sealed class HackGlyphDef : EffectGlyphDef
    {
        public float hackStrength;
        public float baseTicks;
        public int minTicks;
        public int maxTicks;
        public int controlTicks;
        public override SpellEffect Compile(IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff, SpellPayload payload = null)
        {
            float strength = hackStrength;
            float area = radius;
            bool useArea = false;
            for (int i = 0; i < augments.Count; i++)
            {
                SpellGlyphDef augment = augments[i];
                if (augment.defName == SpellDefs.AugmentAmplify || augment.defName == SpellDefs.AugmentDampen)
                    strength *= augment.factor;
                else if (augment.defName == SpellDefs.AugmentAoe)
                {
                    useArea = true;
                    area += augment.radius;
                }
            }
            return new HackEffect(strength, useArea, area, baseTicks, minTicks, maxTicks, controlTicks);
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (hackStrength <= 0f) yield return "Hack needs a positive hackStrength";
            if (baseTicks <= 0f) yield return "Hack needs a positive baseTicks";
            if (minTicks <= 0 || maxTicks <= 0) yield return "Hack needs positive minTicks and maxTicks";
            if (minTicks > maxTicks) yield return "Hack minTicks must not exceed maxTicks";
            if (controlTicks <= 0) yield return "Hack needs a positive controlTicks";
            foreach (string error in AreaErrors(radius)) yield return error;
        }
    }
}

