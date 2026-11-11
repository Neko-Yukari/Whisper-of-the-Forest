using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Equipment
{
    /// <summary>
    /// What the staff's material adds to one cast. The staff is a stuffable weapon, so the material
    /// is a real ThingDef the player can choose at the workbench; these three multipliers are the
    /// whole bridge between that material and the spell, and they are computed from the material's
    /// vanilla armour stats instead of a per-stuff table in C#:
    ///
    ///   heat armour  -> effect duration (a material that carries heat holds the weave longer)
    ///   blunt armour -> mana cost       (a sturdier shaft needs less power to channel)
    ///   sharp/blunt damage multipliers -> damage (the average drives the effect deeper)
    ///
    /// The coefficients and caps are Def values on <see cref="SpellTuningDef"/>, so this class only
    /// applies the formula and never owns a tunable number.
    /// </summary>
    public sealed class SpellStaffModifiers
    {
        public float manaFactor = 1f;
        public float durationFactor = 1f;
        public float damageFactor = 1f;

        /// <summary>Modifiers for one material; a null material means the neutral staff.</summary>
        public static SpellStaffModifiers For(ThingDef stuff)
        {
            SpellTuningDef tuning = SpellDefs.Tuning;
            if (stuff == null)
            {
                return new SpellStaffModifiers { damageFactor = tuning.staffSpellPower };
            }

            float heat = stuff.GetStatValueAbstract(StatDefOf.StuffPower_Armor_Heat);
            float blunt = stuff.GetStatValueAbstract(StatDefOf.StuffPower_Armor_Blunt);
            float averageDamage = (stuff.GetStatValueAbstract(StatDefOf.SharpDamageMultiplier) +
                                   stuff.GetStatValueAbstract(StatDefOf.BluntDamageMultiplier)) / 2f;

            return new SpellStaffModifiers
            {
                manaFactor = Mathf.Clamp(1f - blunt * tuning.staffBluntArmorCostReduction,
                    tuning.staffMinManaFactor, 1f),
                durationFactor = Mathf.Clamp(1f + heat * tuning.staffHeatArmorDurationBonus,
                    1f, tuning.staffMaxDurationFactor),
                damageFactor = Mathf.Min(tuning.staffSpellPower * averageDamage,
                    tuning.staffMaxDamageFactor)
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
