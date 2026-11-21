using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Vfx;

using UnityEngine;
using WhisperOfTheForest.Equipment;

namespace WhisperOfTheForest.Spells
{
    public sealed class LightningEffect : SpellEffect
    {
        private float burnDamage;
        private float burnArmorPenetration;
        private float empDamage;
        private float bonusPerChargeStack;
        private int chargeDurationTicks;
        private bool useArea;
        private float targetRadius;
        private SpellDamageSource logSource;

        public LightningEffect()
        {
        }

        public LightningEffect(float burnDamage, float burnArmorPenetration, float empDamage,
            float bonusPerChargeStack, int chargeDurationTicks, bool useArea, float targetRadius,
            SpellDamageSource logSource)
        {
            this.burnDamage = burnDamage;
            this.burnArmorPenetration = burnArmorPenetration;
            this.empDamage = empDamage;
            this.bonusPerChargeStack = bonusPerChargeStack;
            this.chargeDurationTicks = chargeDurationTicks;
            this.useArea = useArea;
            this.targetRadius = targetRadius;
            this.logSource = logSource;
        }
        public override string DiagnosticName => "lightning";
        public override void AppendSummary(List<string> lines, int indent)
        {
            string chargeDur = chargeDurationTicks > 0
                ? SpellText.TF("WotF_SpellPlan.ChargeDurSuffix", FormatSeconds(chargeDurationTicks))
                : string.Empty;
            lines.Add(SummaryIndent(indent) + SpellText.TF("WotF_SpellPlan.EffectLightning",
                burnDamage.ToString("0.#"), FormatApSuffix(burnArmorPenetration), empDamage.ToString("0.#"),
                bonusPerChargeStack.ToStringPercent("0"), chargeDur, FormatAreaSuffix(useArea, targetRadius)));
        }
        public override int Execute(Thing caster, Thing primary, IntVec3 cell, Map map)
        {
            VfxService.Play(new VfxRequest
            {
                map = map, origin = cell.ToVector3Shifted(),
                vfxDefName = "WotF_Vfx_Lightning"
            });
            List<Thing> targets = SpellExecutor.CollectTargets(useArea, targetRadius, primary, cell, map,
                SpellTargets.CanTakeDamage);
            for (int i = 0; i < targets.Count; i++)
            {
                Thing target = targets[i];
                float stacks = 0f;
                if (target is Pawn pawn)
                {
                    Hediff charge = SpellEffectRuntime.FindHediff(pawn, SpellEffectDefs.HediffLightningCharge);
                    stacks = charge?.Severity ?? 0f;
                }
                if (empDamage > 0f)
                    SpellDamage.Deal(target, DamageDefOf.EMP, empDamage, 0f, caster, logSource);
                float damage = burnDamage * (1f + stacks * bonusPerChargeStack);
                SpellDamage.Deal(target, DamageDefOf.Burn, damage, burnArmorPenetration, caster, logSource);
                if (target is Pawn charged)
                    SpellEffectRuntime.AddTimedHediff(charged, SpellEffectDefs.HediffLightningCharge,
                        chargeDurationTicks);
            }
            return targets.Count;
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref burnDamage, "burnDamage");
            Scribe_Values.Look(ref burnArmorPenetration, "burnArmorPenetration");
            Scribe_Values.Look(ref empDamage, "empDamage");
            Scribe_Values.Look(ref bonusPerChargeStack, "bonusPerChargeStack");
            Scribe_Values.Look(ref chargeDurationTicks, "chargeDurationTicks");
            Scribe_Values.Look(ref useArea, "useArea");
            Scribe_Values.Look(ref targetRadius, "targetRadius");
            Scribe_Deep.Look(ref logSource, "logSource");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && logSource == null)
                logSource = SpellDamageSource.FromGlyph(SpellDefs.Glyph(SpellDefs.EffectLightning));
        }
    }

    public sealed class LightningGlyphDef : EffectGlyphDef
    {
        protected override bool RequiresCombatLogRules => true;
        public float burnDamage;
        public float armorPenetration;
        public float empAmount;
        public float perStackFactor;
        public int extendBaseChargeDurationTicks;
        public override SpellEffect Compile(IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff, SpellPayload payload = null)
        {
            float value = burnDamage;
            float penetration = armorPenetration;
            float area = radius;
            int duration = 0;
            bool useArea = false;
            for (int i = 0; i < augments.Count; i++)
            {
                SpellGlyphDef augment = augments[i];
                if (augment.defName == SpellDefs.AugmentAmplify || augment.defName == SpellDefs.AugmentDampen)
                {
                    if (value > 0f) value *= augment.factor;
                }
                else if (augment.defName == SpellDefs.AugmentBreakArmor) penetration += augment.factor;
                else if (augment.defName == SpellDefs.AugmentAoe)
                {
                    useArea = true;
                    area += augment.radius;
                }
                else if (augment.defName == SpellDefs.AugmentExtend || augment.defName == SpellDefs.AugmentShorten)
                    duration = Duration(duration, extendBaseChargeDurationTicks, augment);
            }
            if (value > 0f && staff != null) value *= staff.damageFactor;
            return new LightningEffect(value, penetration, empAmount, perStackFactor, duration, useArea, area,
                SpellDamageSource.FromGlyph(this));
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (burnDamage <= 0f) yield return "Lightning needs a positive burnDamage";
            if (empAmount < 0f) yield return "empAmount must not be negative";
            if (perStackFactor < 0f) yield return "perStackFactor must not be negative";
            if (armorPenetration < 0f) yield return "armorPenetration must not be negative";
            if (extendBaseChargeDurationTicks <= 0) yield return "extendBaseChargeDurationTicks must be positive";
            foreach (string error in AreaErrors(radius)) yield return error;
        }
    }
}

