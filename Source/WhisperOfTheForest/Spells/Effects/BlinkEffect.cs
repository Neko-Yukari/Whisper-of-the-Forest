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
        public override int Execute(Pawn caster, Thing primary, IntVec3 cell, Map map)
        {
            if (caster == null || !caster.Spawned || caster.Map != map) return 0;
            IntVec3 destination = primary == caster
                ? SpellEffectRuntime.ForwardCell(caster, Mathf.RoundToInt(selfForwardDistance))
                : cell;
            return SpellEffectRuntime.TryTeleport(caster, destination, map) ? 1 : 0;
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

