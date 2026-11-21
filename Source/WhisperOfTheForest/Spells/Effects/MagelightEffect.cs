using System.Collections.Generic;
using Verse;

using WhisperOfTheForest.Equipment;

namespace WhisperOfTheForest.Spells
{
    public sealed class MagelightEffect : SpellEffect
    {
        private float lightRadius;
        private string lightColor;
        private int lifetimeTicks;

        public MagelightEffect()
        {
        }

        public MagelightEffect(float lightRadius, string lightColor, int lifetimeTicks)
        {
            this.lightRadius = lightRadius;
            this.lightColor = lightColor;
            this.lifetimeTicks = lifetimeTicks;
        }

        public float LightRadius => lightRadius;
        public string LightColor => lightColor;
        public int LifetimeTicks => lifetimeTicks;
        public override string DiagnosticName => "magelight";
        public override void AppendSummary(List<string> lines, int indent)
        {
            lines.Add(SummaryIndent(indent) + SpellText.TF("WotF_SpellPlan.EffectMagelight",
                lightRadius.ToString("0.#"), FormatSeconds(lifetimeTicks)));
        }
        public override int Execute(Thing caster, Thing primary, IntVec3 cell, Map map)
        {
            return SpellEffectRuntime.SpawnTemporaryLight(map, cell, this) ? 1 : 0;
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref lightRadius, "lightRadius");
            Scribe_Values.Look(ref lightColor, "lightColor");
            Scribe_Values.Look(ref lifetimeTicks, "lifetimeTicks");
        }
    }

    public sealed class MagelightGlyphDef : EffectGlyphDef
    {
        public float glowRadius;
        public string glowColor;
        public int lifetimeTicks;
        public override SpellEffect Compile(IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff, SpellPayload payload = null)
        {
            float magnitude = 1f;
            int duration = lifetimeTicks;
            for (int i = 0; i < augments.Count; i++)
            {
                SpellGlyphDef augment = augments[i];
                if (augment.defName == SpellDefs.AugmentAmplify || augment.defName == SpellDefs.AugmentDampen)
                    magnitude *= augment.factor;
                else if (augment.defName == SpellDefs.AugmentExtend || augment.defName == SpellDefs.AugmentShorten)
                    duration = Duration(duration, lifetimeTicks, augment);
            }
            return new MagelightEffect(glowRadius * magnitude, glowColor, duration);
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (glowRadius <= 0f) yield return "Magelight needs a positive glowRadius";
            if (glowColor.NullOrEmpty() || !glowColor.StartsWith("#"))
                yield return "Magelight glowColor must be an HTML colour such as #FFE9A0";
            if (lifetimeTicks <= 0) yield return "Magelight needs a positive lifetimeTicks";
        }
    }
}

