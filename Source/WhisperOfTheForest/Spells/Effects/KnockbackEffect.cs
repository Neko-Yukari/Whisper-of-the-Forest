using System.Collections.Generic;
using Verse;

using UnityEngine;
using WhisperOfTheForest.Equipment;

namespace WhisperOfTheForest.Spells
{
    public sealed class KnockbackEffect : SpellEffect
    {
        private float distance;
        private bool useArea;
        private float targetRadius;

        public KnockbackEffect()
        {
        }

        public KnockbackEffect(float distance, bool useArea, float targetRadius)
        {
            this.distance = distance;
            this.useArea = useArea;
            this.targetRadius = targetRadius;
        }
        public override string DiagnosticName => "knockback";
        public override void AppendSummary(List<string> lines, int indent)
        {
            lines.Add(SummaryIndent(indent) + SpellText.TF("WotF_SpellPlan.EffectKnockback",
                distance.ToString("0.#"), FormatAreaSuffix(useArea, targetRadius)));
        }
        public override int Execute(Thing caster, Thing primary, IntVec3 cell, Map map)
        {
            List<Thing> targets = SpellExecutor.CollectTargets(useArea, targetRadius, primary, cell, map,
                SpellTargets.CanBeHealed);
            return SpellEffectRuntime.ApplyMove(distance, targets, cell, map, false);
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref distance, "distance");
            Scribe_Values.Look(ref useArea, "useArea");
            Scribe_Values.Look(ref targetRadius, "targetRadius");
        }
    }

    public sealed class KnockbackGlyphDef : EffectGlyphDef
    {
        public float pushDistance;
        public override SpellEffect Compile(IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff, SpellPayload payload = null)
        {
            float distance = pushDistance;
            float area = radius;
            bool useArea = false;
            for (int i = 0; i < augments.Count; i++)
            {
                SpellGlyphDef augment = augments[i];
                if (augment.defName == SpellDefs.AugmentAmplify || augment.defName == SpellDefs.AugmentDampen)
                    distance *= augment.factor;
                else if (augment.defName == SpellDefs.AugmentAoe)
                {
                    useArea = true;
                    area += augment.radius;
                }
            }
            return new KnockbackEffect(distance, useArea, area);
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (pushDistance <= 0f) yield return "knockback needs a positive pushDistance";
            foreach (string error in AreaErrors(radius)) yield return error;
        }
    }
}

