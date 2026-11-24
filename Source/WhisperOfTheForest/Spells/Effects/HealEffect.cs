using System.Collections.Generic;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Equipment;

namespace WhisperOfTheForest.Spells
{
    public sealed class HealEffect : SpellEffect
    {
        private float tendQuality;
        private int clottingDurationTicks;
        private bool useArea;
        private float targetRadius;

        public HealEffect()
        {
        }
        public HealEffect(float tendQuality, int clottingDurationTicks, bool useArea, float targetRadius)
        {
            this.tendQuality = tendQuality;
            this.clottingDurationTicks = clottingDurationTicks;
            this.useArea = useArea;
            this.targetRadius = targetRadius;
        }
        public override string DiagnosticName => "heal";
        public override SoundDef ResolveSound => SpellDefs.Glyph(SpellDefs.EffectHeal)?.resolveSound;
        public override void AppendSummary(List<string> lines, int indent)
        {
            lines.Add(SummaryIndent(indent) + SpellText.TF("WotF_SpellPlan.EffectHeal",
                tendQuality.ToStringPercent("0"), FormatSeconds(clottingDurationTicks), FormatAreaSuffix(useArea, targetRadius)));
        }
        public override int Execute(Thing caster, Thing primary, IntVec3 cell, Map map)
        {
            List<Thing> targets = SpellExecutor.CollectTargets(useArea, targetRadius, primary, cell, map,
                SpellTargets.CanBeHealed);
            int affected = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                if (!(targets[i] is Pawn pawn)) continue;
                List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
                for (int j = 0; j < hediffs.Count; j++)
                    if (hediffs[j] is Hediff_Injury injury && !HediffUtility.IsPermanent(injury))
                        injury.Tended(tendQuality, 1f);
                Hediff clotting = SpellEffectRuntime.MakeTimedHediff(pawn, SpellEffectDefs.HediffClotting,
                    clottingDurationTicks);
                if (clotting != null) pawn.health.AddHediff(clotting);
                affected++;
            }
            return affected;
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref tendQuality, "tendQuality");
            Scribe_Values.Look(ref clottingDurationTicks, "clottingDurationTicks");
            Scribe_Values.Look(ref useArea, "useArea");
            Scribe_Values.Look(ref targetRadius, "targetRadius");
        }
    }

    public sealed class HealGlyphDef : EffectGlyphDef
    {
        public float baseTendQuality;
        public int clottingDurationTicks;
        public override SpellEffect Compile(IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff, SpellPayload payload = null)
        {
            float magnitude = 1f;
            float area = radius;
            int duration = clottingDurationTicks;
            bool useArea = false;
            for (int i = 0; i < augments.Count; i++)
            {
                SpellGlyphDef augment = augments[i];
                if (augment.defName == SpellDefs.AugmentAmplify || augment.defName == SpellDefs.AugmentDampen)
                    magnitude *= augment.factor;
                else if (augment.defName == SpellDefs.AugmentExtend || augment.defName == SpellDefs.AugmentShorten)
                    duration = Duration(duration, clottingDurationTicks, augment);
                else if (augment.defName == SpellDefs.AugmentAoe)
                {
                    useArea = true;
                    area += augment.radius;
                }
            }
            if (duration > 0 && staff != null)
                duration = Mathf.Max(1, Mathf.RoundToInt(duration * staff.durationFactor));
            return new HealEffect(Mathf.Clamp01(baseTendQuality * magnitude), duration, useArea, area);
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (baseTendQuality <= 0f || baseTendQuality > 1f)
                yield return "baseTendQuality must be in (0, 1]";
            if (clottingDurationTicks <= 0) yield return "clottingDurationTicks must be positive";
            foreach (string error in AreaErrors(radius)) yield return error;
        }

    }
}
