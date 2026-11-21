using System.Collections.Generic;
using Verse;

using WhisperOfTheForest.Equipment;

namespace WhisperOfTheForest.Spells
{
    public sealed class RuneEffect : SpellEffect
    {
        private SpellPayload payload;
        private int lifetimeTicks;

        public RuneEffect()
        {
        }

        public RuneEffect(SpellPayload payload, int lifetimeTicks)
        {
            this.payload = payload;
            this.lifetimeTicks = lifetimeTicks;
        }
        public override string DiagnosticName => "rune";
        public override void AppendSummary(List<string> lines, int indent)
        {
            lines.Add(SummaryIndent(indent) + SpellText.TF("WotF_SpellPlan.EffectRune", FormatSeconds(lifetimeTicks)));
            AppendPayloadSummary(payload, lines, indent);
        }
        public override int Execute(Thing caster, Thing primary, IntVec3 cell, Map map)
        {
            return SpellEffectRuntime.SpawnField(payload, lifetimeTicks, 0f, 0, SpellFieldMode.Rune,
                caster, cell, map);
        }
        public override void ExposeData()
        {
            Scribe_Deep.Look(ref payload, "payload");
            Scribe_Values.Look(ref lifetimeTicks, "lifetimeTicks");
        }
    }
    public sealed class RuneGlyphDef : EffectGlyphDef
    {
        public override bool ConsumesRemainder => true;
        public int lifetimeTicks;
        public override SpellEffect Compile(IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff, SpellPayload payload = null)
        {
            return new RuneEffect(payload, lifetimeTicks);
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (lifetimeTicks <= 0) yield return "Rune needs a positive lifetimeTicks";
        }
    }
}

