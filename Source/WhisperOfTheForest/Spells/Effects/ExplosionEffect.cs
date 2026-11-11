using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Equipment;

namespace WhisperOfTheForest.Spells
{
    public sealed class ExplosionEffect : SpellEffect
    {
        private float blastDamage;
        private float blastRadius;
        private float armorPenetration;
        private bool ignoreBuildings;

        public ExplosionEffect()
        {
        }

        public ExplosionEffect(float blastDamage, float blastRadius, float armorPenetration, bool ignoreBuildings)
        {
            this.blastDamage = blastDamage;
            this.blastRadius = blastRadius;
            this.armorPenetration = armorPenetration;
            this.ignoreBuildings = ignoreBuildings;
        }
        public override string DiagnosticName => "explosion";
        public override int Execute(Pawn caster, Thing primary, IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map)) return 0;
            List<Thing> ignored = ignoreBuildings ? BuildingsAround(cell, map, blastRadius) : null;
            GenExplosion.DoExplosion(cell, map, blastRadius, SpellDamage.ArcaneForce, caster,
                damAmount: Mathf.Max(1, Mathf.RoundToInt(blastDamage)), armorPenetration: armorPenetration,
                doVisualEffects: false, damageFalloff: true, ignoredThings: ignored);
            return 1;
        }
        private static List<Thing> BuildingsAround(IntVec3 cell, Map map, float radius)
        {
            var buildings = new List<Thing>();
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(cell, map, radius, true))
                if (thing is Building) buildings.Add(thing);
            return buildings;
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref blastDamage, "blastDamage");
            Scribe_Values.Look(ref blastRadius, "blastRadius");
            Scribe_Values.Look(ref armorPenetration, "armorPenetration");
            Scribe_Values.Look(ref ignoreBuildings, "ignoreBuildings");
        }
    }

    public sealed class ExplosionGlyphDef : EffectGlyphDef
    {
        public float blastDamage;
        public float armorPenetration;
        public override SpellEffect Compile(IReadOnlyList<SpellGlyphDef> augments,
            SpellStaffModifiers staff, SpellPayload payload = null)
        {
            float value = blastDamage;
            float penetration = armorPenetration;
            float area = radius;
            bool ignoreBuildings = false;
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
                else if (augment.defName == SpellDefs.AugmentAoe) area += augment.radius;
                else if (augment.defName == SpellDefs.AugmentSensitive) ignoreBuildings = true;
                else if (augment.defName == SpellDefs.AugmentBreakArmor) penetration += augment.factor;
            }
            if (value > 0f && staff != null) value *= staff.damageFactor;
            return new ExplosionEffect(value, area, penetration, ignoreBuildings);
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (blastDamage <= 0f) yield return "Explosion needs a positive blastDamage";
            if (armorPenetration < 0f) yield return "armorPenetration must not be negative";
            if (radius <= 0f) yield return "Explosion needs a positive radius";
        }

    }
}
