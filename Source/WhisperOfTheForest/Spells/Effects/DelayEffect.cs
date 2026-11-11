using System.Collections.Generic;
using Verse;

using WhisperOfTheForest.Equipment;

namespace WhisperOfTheForest.Spells
{
    public sealed class DelayEffect : SpellEffect
    {
        private SpellPayload payload;
        private int delayTicks;

        public DelayEffect()
        {
        }

        public DelayEffect(SpellPayload payload, int delayTicks)
        {
            this.payload = payload;
            this.delayTicks = delayTicks;
        }
        internal SpellPayload Payload => payload;

        public override string DiagnosticName => "delay";
        public override int Execute(Pawn caster, Thing primary, IntVec3 cell, Map map)
        {
            if (payload == null || payload.steps.Count == 0 || delayTicks <= 0) return 0;
            MapComponent_SpellDelays component = map.GetComponent<MapComponent_SpellDelays>();
            if (component == null)
            {
                SpellDiagnostics.Error("map has no spell delay component; the delayed payload was dropped.");
                return 0;
            }
            component.Schedule(caster, cell, Find.TickManager.TicksGame + delayTicks, payload.steps);
            return 1;
        }
        public override void ExposeData()
        {
            Scribe_Deep.Look(ref payload, "payload");
            Scribe_Values.Look(ref delayTicks, "delayTicks");
        }
    }
    public sealed class DelayGlyphDef : EffectGlyphDef
    {
        public override bool ConsumesRemainder => true;
        public int delayTicks;
        public override SpellEffect Compile(IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff, SpellPayload payload = null)
        {
            int duration = delayTicks;
            for (int i = 0; i < augments.Count; i++)
            {
                SpellGlyphDef augment = augments[i];
                if (augment.defName == SpellDefs.AugmentExtend || augment.defName == SpellDefs.AugmentShorten)
                    duration = Duration(duration, delayTicks, augment);
            }
            return new DelayEffect(payload, duration);
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (delayTicks <= 0) yield return "Delay needs a positive delayTicks";
        }
    }
}

