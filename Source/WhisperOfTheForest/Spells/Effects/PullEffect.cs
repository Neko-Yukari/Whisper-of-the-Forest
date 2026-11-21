using System.Collections.Generic;
using Verse;

using UnityEngine;
using WhisperOfTheForest.Equipment;

namespace WhisperOfTheForest.Spells
{
    public sealed class PullEffect : SpellEffect
    {
        private float distance;
        private bool useArea;
        private float targetRadius;

        public PullEffect()
        {
        }

        public PullEffect(float distance, bool useArea, float targetRadius)
        {
            this.distance = distance;
            this.useArea = useArea;
            this.targetRadius = targetRadius;
        }
        public override string DiagnosticName => "pull";
        public override void AppendSummary(List<string> lines, int indent)
        {
            lines.Add(SummaryIndent(indent) + SpellText.TF("WotF_SpellPlan.EffectPull",
                distance.ToString("0.#"), FormatAreaSuffix(useArea, targetRadius)));
        }
        public override int Execute(Thing caster, Thing primary, IntVec3 cell, Map map)
        {
            List<Thing> targets = SpellExecutor.CollectTargets(useArea, targetRadius, primary, cell, map,
                SpellTargets.CanBeHealed);
            return SpellEffectRuntime.ApplyMove(distance, targets, cell, map, true);
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref distance, "distance");
            Scribe_Values.Look(ref useArea, "useArea");
            Scribe_Values.Look(ref targetRadius, "targetRadius");
        }
    }

    public sealed class PullGlyphDef : EffectGlyphDef
    {
        public float pullDistance;
        public override SpellEffect Compile(IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff, SpellPayload payload = null)
        {
            float distance = pullDistance;
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
            return new PullEffect(distance, useArea, area);
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (pullDistance <= 0f) yield return "pull needs a positive pullDistance";
            foreach (string error in AreaErrors(radius)) yield return error;
        }
    }
}

