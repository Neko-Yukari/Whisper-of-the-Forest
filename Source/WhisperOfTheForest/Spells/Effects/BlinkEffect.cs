using System.Collections.Generic;
using Verse;

using UnityEngine;
using WhisperOfTheForest.Equipment;

namespace WhisperOfTheForest.Spells
{
    public sealed class BlinkEffect : SpellEffect
    {
        private float selfForwardDistance;

        public BlinkEffect()
        {
        }

        public BlinkEffect(float selfForwardDistance)
        {
            this.selfForwardDistance = selfForwardDistance;
        }
        public override string DiagnosticName => "blink";
        public override SoundDef ResolveSound => SpellDefs.Glyph(SpellDefs.EffectBlink)?.resolveSound;
        public override void AppendSummary(List<string> lines, int indent)
        {
            lines.Add(SummaryIndent(indent) + SpellText.TF("WotF_SpellPlan.EffectBlink",
                selfForwardDistance.ToString("0.#")));
        }
        public override int Execute(Thing caster, Thing primary, IntVec3 cell, Map map)
        {
            if (!(caster is Pawn casterPawn) || !casterPawn.Spawned || casterPawn.Map != map) return 0;
            IntVec3 destination = primary == casterPawn
                ? SpellEffectRuntime.ForwardCell(casterPawn, Mathf.RoundToInt(selfForwardDistance))
                : cell;
            return SpellEffectRuntime.TryTeleport(casterPawn, destination, map) ? 1 : 0;
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref selfForwardDistance, "selfForwardDistance");
        }
    }

    public sealed class BlinkGlyphDef : EffectGlyphDef
    {
        public float selfForwardDistance;
        public override SpellEffect Compile(IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff, SpellPayload payload = null)
        {
            float distance = selfForwardDistance;
            for (int i = 0; i < augments.Count; i++)
            {
                SpellGlyphDef augment = augments[i];
                if (augment.defName == SpellDefs.AugmentAmplify || augment.defName == SpellDefs.AugmentDampen)
                    distance *= augment.factor;
            }
            return new BlinkEffect(distance);
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (selfForwardDistance <= 0f) yield return "Blink needs a positive selfForwardDistance";
        }
    }
}

