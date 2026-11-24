using System.Collections.Generic;
using RimWorld;
using Verse;

using UnityEngine;
using WhisperOfTheForest.Equipment;

namespace WhisperOfTheForest.Spells
{
    public sealed class ColdSnapEffect : SpellEffect
    {
        private float damage;
        private float armorPenetration;
        private float frozenTargetMultiplier;
        private bool useArea;
        private float targetRadius;
        private SpellDamageSource logSource;

        public ColdSnapEffect()
        {
        }

        public ColdSnapEffect(float damage, float armorPenetration, float frozenTargetMultiplier,
            bool useArea, float targetRadius, SpellDamageSource logSource)
        {
            this.damage = damage;
            this.armorPenetration = armorPenetration;
            this.frozenTargetMultiplier = frozenTargetMultiplier;
            this.useArea = useArea;
            this.targetRadius = targetRadius;
            this.logSource = logSource;
        }
        public override string DiagnosticName => "cold snap";
        public override SoundDef ResolveSound => SpellDefs.Glyph(SpellDefs.EffectColdSnap)?.resolveSound;
        public override void AppendSummary(List<string> lines, int indent)
        {
            lines.Add(SummaryIndent(indent) + SpellText.TF("WotF_SpellPlan.EffectColdSnap",
                damage.ToString("0.#"), frozenTargetMultiplier.ToString("0.#"),
                FormatApSuffix(armorPenetration), FormatAreaSuffix(useArea, targetRadius)));
        }
        public override int Execute(Thing caster, Thing primary, IntVec3 cell, Map map)
        {
            List<Thing> targets = SpellExecutor.CollectTargets(useArea, targetRadius, primary, cell, map,
                SpellTargets.CanTakeDamage);
            for (int i = 0; i < targets.Count; i++)
            {
                Thing target = targets[i];
                bool slowed = target is Pawn pawn
                              && SpellEffectRuntime.FindHediff(pawn, SpellEffectDefs.HediffFreeze) != null;
                float amount = slowed ? damage * Mathf.Max(1f, frozenTargetMultiplier) : damage;
                SpellDamage.Deal(target, amount, armorPenetration, caster, logSource);
            }
            return targets.Count;
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref damage, "damage");
            Scribe_Values.Look(ref armorPenetration, "armorPenetration");
            Scribe_Values.Look(ref frozenTargetMultiplier, "frozenTargetMultiplier");
            Scribe_Values.Look(ref useArea, "useArea");
            Scribe_Values.Look(ref targetRadius, "targetRadius");
            Scribe_Deep.Look(ref logSource, "logSource");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && logSource == null)
                logSource = SpellDamageSource.FromGlyph(SpellDefs.Glyph(SpellDefs.EffectColdSnap));
        }
    }

    public sealed class ColdSnapGlyphDef : EffectGlyphDef
    {
        protected override bool RequiresCombatLogRules => true;
        public float damage;
        public float armorPenetration;
        public float vulnerableFactor;
        public override SpellEffect Compile(IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff, SpellPayload payload = null)
        {
            float value = damage;
            float area = radius;
            bool useArea = false;
            for (int i = 0; i < augments.Count; i++)
            {
                SpellGlyphDef augment = augments[i];
                if (augment.defName == SpellDefs.AugmentAmplify || augment.defName == SpellDefs.AugmentDampen)
                {
                    if (value > 0f) value *= augment.factor;
                }
                else if (augment.defName == SpellDefs.AugmentAoe)
                {
                    useArea = true;
                    area += augment.radius;
                }
            }
            if (value > 0f && staff != null) value *= staff.damageFactor;
            return new ColdSnapEffect(value, armorPenetration, vulnerableFactor, useArea, area,
                SpellDamageSource.FromGlyph(this));
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (damage <= 0f) yield return "Cold Snap needs a positive damage";
            if (vulnerableFactor <= 0f) yield return "vulnerableFactor must be positive";
            if (armorPenetration < 0f) yield return "armorPenetration must not be negative";
            foreach (string error in AreaErrors(radius)) yield return error;
        }
    }
}

