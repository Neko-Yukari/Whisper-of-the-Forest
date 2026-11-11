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

        public HarmEffect()
        {
        }

        public HarmEffect(float damage, float armorPenetration, bool useArea, float targetRadius)
        {
            this.damage = damage;
            this.armorPenetration = armorPenetration;
            this.useArea = useArea;
            this.targetRadius = targetRadius;
        }
        public override string DiagnosticName => "harm";
        public override int Execute(Pawn caster, Thing primary, IntVec3 cell, Map map)
        {
            List<Thing> targets = SpellExecutor.CollectTargets(useArea, targetRadius, primary, cell, map,
                SpellTargets.CanTakeDamage);
            for (int i = 0; i < targets.Count; i++)
                SpellDamage.Deal(targets[i], damage, armorPenetration, caster);
            return targets.Count;
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref damage, "damage");
            Scribe_Values.Look(ref armorPenetration, "armorPenetration");
            Scribe_Values.Look(ref useArea, "useArea");
            Scribe_Values.Look(ref targetRadius, "targetRadius");
        }
    }

    public sealed class HarmGlyphDef : EffectGlyphDef
    {
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
            return new HarmEffect(value, penetration, useArea, area);
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
