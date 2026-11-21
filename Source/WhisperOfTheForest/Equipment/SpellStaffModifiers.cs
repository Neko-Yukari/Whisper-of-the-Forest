using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Equipment
{
    /// <summary>
    /// What a spell staff or wand (and its material) applies to one cast. Both long staves and
    /// one-handed wands are stuffable weapons driven by <see cref="CompProperties_SpellStaff"/>:
    ///
    ///   heat armour  -> effect duration (a material that carries heat holds the weave longer)
    ///   blunt armour -> mana cost       (a sturdier shaft needs less power to channel)
    ///   sharp/blunt damage multipliers -> damage (the average drives the effect deeper)
    ///   rangeFactor / windupFactor     -> spell aim range and windup pacing
    ///
    /// All coefficients and caps live on <see cref="CompProperties_SpellStaff"/> in XML; this class
    /// only applies the formula and owns no tunable numbers.
    /// </summary>
    public sealed class SpellStaffModifiers
    {
        public float manaFactor = 1f;
        public float durationFactor = 1f;
        public float damageFactor = 1f;
        public float rangeFactor = 1f;
        public float windupFactor = 1f;

        /// <summary>
        /// Modifiers for one staff/wand Def and material. Null <paramref name="props"/> yields
        /// neutral unit multipliers; null <paramref name="stuff"/> applies the staff's own baseline
        /// power, range and windup without material scaling.
        /// </summary>
        public static SpellStaffModifiers For(CompProperties_SpellStaff props, ThingDef stuff)
        {
            if (props == null)
            {
                return new SpellStaffModifiers();
            }

            if (stuff == null)
            {
                return new SpellStaffModifiers
                {
                    damageFactor = Mathf.Min(props.spellPowerFactor, props.maxDamageFactor),
                    rangeFactor = props.rangeFactor,
                    windupFactor = props.windupFactor
                };
            }

            float heat = stuff.GetStatValueAbstract(StatDefOf.StuffPower_Armor_Heat);
            float blunt = stuff.GetStatValueAbstract(StatDefOf.StuffPower_Armor_Blunt);
            float averageDamage = (stuff.GetStatValueAbstract(StatDefOf.SharpDamageMultiplier) +
                                   stuff.GetStatValueAbstract(StatDefOf.BluntDamageMultiplier)) / 2f;

            return new SpellStaffModifiers
            {
                manaFactor = Mathf.Clamp(1f - blunt * props.bluntArmorCostReduction,
                    props.minManaFactor, 1f),
                durationFactor = Mathf.Clamp(1f + heat * props.heatArmorDurationBonus,
                    1f, props.maxDurationFactor),
                damageFactor = Mathf.Min(props.spellPowerFactor * averageDamage,
                    props.maxDamageFactor),
                rangeFactor = props.rangeFactor,
                windupFactor = props.windupFactor
            };
        }

        /// <summary>
        /// Applies the material's mana discount to the recipe before the cast checks affordability.
        /// The plan the cast freezes carries this same cost, so the save and the actual spend agree.
        /// </summary>
        public void ApplyCost(SpellCost cost)
        {
            if (cost != null && cost.mana > 0f)
            {
                cost.mana *= manaFactor;
            }
        }
    }
}
