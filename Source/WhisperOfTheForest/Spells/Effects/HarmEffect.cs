using System.Collections.Generic;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Equipment;

namespace WhisperOfTheForest.Spells
{
    public sealed class HarmEffect : SpellEffect
    {
        private float damage;
        private float armorPenetration;
        private bool useArea;
        private float targetRadius;
        private SpellDamageSource logSource;

        public HarmEffect()
        {
        }

        public HarmEffect(float damage, float armorPenetration, bool useArea, float targetRadius,
            SpellDamageSource logSource)
        {
            this.damage = damage;
            this.armorPenetration = armorPenetration;
            this.useArea = useArea;
            this.targetRadius = targetRadius;
            this.logSource = logSource;
        }
        public override string DiagnosticName => "harm";
        public override SoundDef ResolveSound => SpellDefs.Glyph(SpellDefs.EffectHarm)?.resolveSound;
        public override void AppendSummary(List<string> lines, int indent)
        {
            lines.Add(SummaryIndent(indent) + SpellText.TF("WotF_SpellPlan.EffectHarm",
                damage.ToString("0.#"), FormatApSuffix(armorPenetration), FormatAreaSuffix(useArea, targetRadius)));
        }
        public override int Execute(Thing caster, Thing primary, IntVec3 cell, Map map)
        {
            List<Thing> targets = SpellExecutor.CollectTargets(useArea, targetRadius, primary, cell, map,
                SpellTargets.CanTakeDamage);
            for (int i = 0; i < targets.Count; i++)
                SpellDamage.Deal(targets[i], damage, armorPenetration, caster, logSource);
            return targets.Count;
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref damage, "damage");
            Scribe_Values.Look(ref armorPenetration, "armorPenetration");
            Scribe_Values.Look(ref useArea, "useArea");
            Scribe_Values.Look(ref targetRadius, "targetRadius");
            Scribe_Deep.Look(ref logSource, "logSource");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && logSource == null)
                logSource = SpellDamageSource.FromGlyph(SpellDefs.Glyph(SpellDefs.EffectHarm));
        }
    }

    public sealed class HarmGlyphDef : EffectGlyphDef
    {
        protected override bool RequiresCombatLogRules => true;
        public float damage;
        public float armorPenetration;
        public override SpellEffect Compile(IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff, SpellPayload payload = null)
        {
            float value = damage;
            float penetration = armorPenetration;
            float area = radius;
            bool useArea = false;
            for (int i = 0; i < augments.Count; i++)
            {
                SpellGlyphDef augment = augments[i];
                if (augment.defName == SpellDefs.AugmentAmplify)
                {
                    if (value > 0f) value *= augment.factor;
                    area += augment.radius;
                }
                else if (augment.defName == SpellDefs.AugmentDampen)
                {
                    if (value > 0f) value *= augment.factor;
                }
                else if (augment.defName == SpellDefs.AugmentBreakArmor)
                {
                    penetration += augment.factor;
                }
                else if (augment.defName == SpellDefs.AugmentAoe)
                {
                    useArea = true;
                    area += augment.radius;
                }
            }
            if (value > 0f && staff != null) value *= staff.damageFactor;
            return new HarmEffect(value, penetration, useArea, area, SpellDamageSource.FromGlyph(this));
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (damage <= 0f) yield return "Harm needs a positive damage";
            if (armorPenetration < 0f) yield return "armorPenetration must not be negative";
            foreach (string error in AreaErrors(radius)) yield return error;
        }

    }
}
