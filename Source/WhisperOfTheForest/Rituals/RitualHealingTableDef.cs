using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    public enum HealingTier
    {
        Minor = 1,
        Medium = 2,
        Grand = 3
    }

    public sealed class HealingInjuryRule
    {
        public float basePriority;
        public float priorityPerSeverity;
        public float priorityPerBleedRate;
        public float baseCost;
        public float costPerSeverity;

        public IEnumerable<string> ConfigErrors(string owner)
        {
            if (basePriority < 0f || priorityPerSeverity < 0f || priorityPerBleedRate < 0f)
            {
                yield return owner + " has negative priority weights.";
            }

            if (baseCost < 0f || costPerSeverity <= 0f)
            {
                yield return owner + " requires baseCost >= 0 and costPerSeverity > 0.";
            }
        }
    }

    public sealed class HealingBodyPartEntry
    {
        public BodyPartDef part;
        public float costFactor;
        public float priorityWeight;
        public float regrowCost;
        public float regrowPriority;

        public IEnumerable<string> ConfigErrors(string owner, bool requirePart)
        {
            if (requirePart && part == null)
            {
                yield return owner + " has a bodyParts entry with null part.";
            }

            string label = part != null ? part.defName : "unlistedLimb";
            if (costFactor <= 0f)
            {
                yield return owner + " " + label + " costFactor must be positive.";
            }

            if (priorityWeight <= 0f)
            {
                yield return owner + " " + label + " priorityWeight must be positive.";
            }

            if (regrowCost <= 0f)
            {
                yield return owner + " " + label + " regrowCost must be positive.";
            }

            if (regrowPriority <= 0f)
            {
                yield return owner + " " + label + " regrowPriority must be positive.";
            }
        }
    }

    public sealed class HealingConditionEntry
    {
        public HediffDef hediff;
        public float priority;
        public float priorityPerSeverity;
        public float baseCost;
        public float costPerSeverity;
        public bool applyPartFactors;
        public bool allowPartialHeal;
        public bool allowUncurableByItem;

        public IEnumerable<string> ConfigErrors(string owner, bool requireHediff)
        {
            if (requireHediff && hediff == null)
            {
                yield return owner + " has a conditions entry with null hediff.";
            }

            string label = hediff != null ? hediff.defName : "defaultCondition";
            if (priority < 0f || priorityPerSeverity < 0f)
            {
                yield return owner + " " + label + " has negative priority.";
            }

            if (baseCost < 0f || costPerSeverity < 0f || (baseCost <= 0f && costPerSeverity <= 0f))
            {
                yield return owner + " " + label + " requires non-negative costs with a positive total.";
            }

            if (allowPartialHeal && costPerSeverity <= 0f)
            {
                yield return owner + " " + label + " sets allowPartialHeal without positive costPerSeverity.";
            }
        }
    }

    /// <summary>
    /// Priority weights, life-pool costs and the base strength-to-pool curve for life restoration rites.
    /// Unlisted modded body parts resolve directly to <see cref="unlistedLimb"/> (single-limb tier) with
    /// no HP-derived fallback formula in code.
    /// </summary>
    public sealed class RitualHealingTableDef : Def
    {
        public SimpleCurve basePoolCurve;
        public float lifeThreateningPriorityBonus;
        public int bleedDeathWindowTicks;
        public float bleedRateEmergencyThreshold;
        public float immunePriorityFactor;

        public HealingInjuryRule freshInjury;
        public HealingInjuryRule permanentInjury;
        public HealingBodyPartEntry unlistedLimb;

        public HealingConditionEntry defaultAddiction;
        public HealingConditionEntry defaultInfection;
        public HealingConditionEntry defaultChronic;
        public HealingConditionEntry defaultCondition;

        public List<HealingBodyPartEntry> bodyParts = new List<HealingBodyPartEntry>();
        public List<HealingConditionEntry> conditions = new List<HealingConditionEntry>();

        private Dictionary<BodyPartDef, HealingBodyPartEntry> partsByDef;
        private Dictionary<HediffDef, HealingConditionEntry> conditionsByDef;

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            BuildIndex();
        }

        private void EnsureIndex()
        {
            if (partsByDef == null || conditionsByDef == null)
            {
                BuildIndex();
            }
        }

        private void BuildIndex()
        {
            partsByDef = new Dictionary<BodyPartDef, HealingBodyPartEntry>();
            if (bodyParts != null)
            {
                for (int i = 0; i < bodyParts.Count; i++)
                {
                    HealingBodyPartEntry entry = bodyParts[i];
                    if (entry?.part != null)
                    {
                        partsByDef[entry.part] = entry;
                    }
                }
            }

            conditionsByDef = new Dictionary<HediffDef, HealingConditionEntry>();
            if (conditions != null)
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    HealingConditionEntry entry = conditions[i];
                    if (entry?.hediff != null)
                    {
                        conditionsByDef[entry.hediff] = entry;
                    }
                }
            }
        }

        public float BasePoolFor(float ritualStrength)
        {
            return basePoolCurve.Evaluate(ritualStrength);
        }

        public HealingBodyPartEntry PartFor(BodyPartDef partDef)
        {
            EnsureIndex();
            if (partDef != null && partsByDef.TryGetValue(partDef, out HealingBodyPartEntry entry))
            {
                return entry;
            }

            return unlistedLimb;
        }

        public HealingConditionEntry ConditionFor(Hediff hediff)
        {
            EnsureIndex();
            if (conditionsByDef.TryGetValue(hediff.def, out HealingConditionEntry entry))
            {
                return entry;
            }

            if (hediff is Hediff_Addiction || hediff.def.IsAddiction)
            {
                return defaultAddiction;
            }

            if (hediff.def.isInfection || hediff.def.PossibleToDevelopImmunityNaturally())
            {
                return defaultInfection;
            }

            if (hediff.def.chronic)
            {
                return defaultChronic;
            }

            return defaultCondition;
        }

        public bool AllowsUncurableByItem(HediffDef def)
        {
            EnsureIndex();
            return def != null
                && conditionsByDef.TryGetValue(def, out HealingConditionEntry entry)
                && entry.allowUncurableByItem;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (basePoolCurve == null || basePoolCurve.PointsCount == 0 || basePoolCurve.MinY <= 0f)
            {
                yield return defName + " must declare a positive basePoolCurve.";
            }

            if (lifeThreateningPriorityBonus < 0f)
            {
                yield return defName + " lifeThreateningPriorityBonus must not be negative.";
            }

            if (bleedDeathWindowTicks < 0)
            {
                yield return defName + " bleedDeathWindowTicks must not be negative.";
            }

            if (bleedRateEmergencyThreshold < 0f)
            {
                yield return defName + " bleedRateEmergencyThreshold must not be negative.";
            }

            if (immunePriorityFactor <= 0f || immunePriorityFactor > 1f)
            {
                yield return defName + " immunePriorityFactor must be in (0, 1].";
            }

            if (freshInjury == null)
            {
                yield return defName + " must declare freshInjury.";
            }
            else
            {
                foreach (string error in freshInjury.ConfigErrors(defName + ".freshInjury"))
                {
                    yield return error;
                }
            }

            if (permanentInjury == null)
            {
                yield return defName + " must declare permanentInjury.";
            }
            else
            {
                foreach (string error in permanentInjury.ConfigErrors(defName + ".permanentInjury"))
                {
                    yield return error;
                }
            }

            if (unlistedLimb == null)
            {
                yield return defName + " must declare unlistedLimb.";
            }
            else
            {
                foreach (string error in unlistedLimb.ConfigErrors(defName, requirePart: false))
                {
                    yield return error;
                }
            }

            foreach (string error in CheckDefaultCondition("defaultAddiction", defaultAddiction)) yield return error;
            foreach (string error in CheckDefaultCondition("defaultInfection", defaultInfection)) yield return error;
            foreach (string error in CheckDefaultCondition("defaultChronic", defaultChronic)) yield return error;
            foreach (string error in CheckDefaultCondition("defaultCondition", defaultCondition)) yield return error;

            var seenParts = new HashSet<BodyPartDef>();
            if (bodyParts != null)
            {
                for (int i = 0; i < bodyParts.Count; i++)
                {
                    HealingBodyPartEntry entry = bodyParts[i];
                    if (entry == null)
                    {
                        yield return defName + " has a null bodyParts entry.";
                        continue;
                    }

                    foreach (string error in entry.ConfigErrors(defName, requirePart: true))
                    {
                        yield return error;
                    }

                    if (entry.part != null && !seenParts.Add(entry.part))
                    {
                        yield return defName + " lists body part " + entry.part.defName + " more than once.";
                    }
                }
            }

            var seenConditions = new HashSet<HediffDef>();
            if (conditions != null)
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    HealingConditionEntry entry = conditions[i];
                    if (entry == null)
                    {
                        yield return defName + " has a null conditions entry.";
                        continue;
                    }

                    foreach (string error in entry.ConfigErrors(defName, requireHediff: true))
                    {
                        yield return error;
                    }

                    if (entry.hediff != null && !seenConditions.Add(entry.hediff))
                    {
                        yield return defName + " lists condition " + entry.hediff.defName + " more than once.";
                    }
                }
            }
        }

        private IEnumerable<string> CheckDefaultCondition(string fieldName, HealingConditionEntry entry)
        {
            if (entry == null)
            {
                yield return defName + " must declare " + fieldName + ".";
                yield break;
            }

            foreach (string error in entry.ConfigErrors(defName + "." + fieldName, requireHediff: false))
            {
                yield return error;
            }
        }
    }
}
