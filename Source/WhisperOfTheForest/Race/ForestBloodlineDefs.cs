using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Race
{
    /// <summary>
    /// The bloodline gene's Def. It carries the level curves, the vanilla psylink mirror ladder and
    /// the stat and colours the gene reads, so the gene code never hardcodes a tunable value.
    /// </summary>
    public class WotF_ForestBloodlineGeneDef : GeneDef
    {
        public StatDef manaMaxStat;

        /// <summary>Mana restored per second; the gene adds it every tick it is ticked.</summary>
        public StatDef manaRegenStat;
        public Color manaBarColor;
        public Color manaBarHighlightColor;

        public int maxLevel;

        /// <summary>Work speed multiplier on a workbench that has a CompPowerTrader.</summary>
        public float poweredWorkTableSpeedFactor;

        /// <summary>Quality tiers added to a product made at a workbench without a CompPowerTrader.</summary>
        public int handcraftQualityBonus;

        /// <summary>The effecter a bloodline pawn uses while fixing a broken-down building.</summary>
        public EffecterDef elfRepairEffect;

        /// <summary>Experience needed to leave level x.</summary>
        public SimpleCurve xpToNextLevelCurve;

        /// <summary>Mana cap at level x, added to <see cref="manaMaxStat"/>.</summary>
        public SimpleCurve manaMaxCurve;

        /// <summary>Flat addition to the vanilla neural-heat limit at level x.</summary>
        public SimpleCurve entropyMaxCurve;

        /// <summary>Factor on the vanilla neural-heat recovery rate at level x.</summary>
        public SimpleCurve entropyRecoveryFactorCurve;

        /// <summary>Entry n is the bloodline level that unlocks mirror psylink level n + 1.</summary>
        public List<int> psylinkUnlockLevels;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (!typeof(Gene_ForestBloodline).IsAssignableFrom(geneClass))
            {
                yield return "geneClass must be Gene_ForestBloodline";
            }

            if (!typeof(GeneGizmo_ForestMana).IsAssignableFrom(resourceGizmoType))
            {
                yield return "resourceGizmoType must be GeneGizmo_ForestMana";
            }

            // The vanilla resource gizmo iterates this list every frame without a null check.
            if (resourceGizmoThresholds == null)
            {
                yield return "resourceGizmoThresholds is null; write an empty list if the bar has no marks";
            }

            if (manaMaxStat == null)
            {
                yield return "manaMaxStat is null";
            }

            if (manaRegenStat == null)
            {
                yield return "manaRegenStat is null";
            }

            if (manaBarColor.a <= 0f)
            {
                yield return "manaBarColor is unset (alpha 0)";
            }

            if (manaBarHighlightColor.a <= 0f)
            {
                yield return "manaBarHighlightColor is unset (alpha 0)";
            }

            if (maxLevel < 1)
            {
                yield return "maxLevel must be at least 1";
            }

            if (poweredWorkTableSpeedFactor <= 0f || poweredWorkTableSpeedFactor > 1f)
            {
                yield return "poweredWorkTableSpeedFactor must be in (0, 1]";
            }

            if (handcraftQualityBonus < 0)
            {
                yield return "handcraftQualityBonus must not be negative";
            }

            if (elfRepairEffect == null)
            {
                yield return "elfRepairEffect is null";
            }

            if (xpToNextLevelCurve == null || xpToNextLevelCurve.PointsCount == 0)
            {
                yield return "xpToNextLevelCurve is empty";
            }
            else
            {
                for (int level = 1; level < maxLevel; level++)
                {
                    if (xpToNextLevelCurve.Evaluate(level) <= 0f)
                    {
                        yield return "xpToNextLevelCurve must be positive at level " + level;
                        break;
                    }
                }
            }

            if (manaMaxCurve == null || manaMaxCurve.PointsCount == 0)
            {
                yield return "manaMaxCurve is empty";
            }

            if (entropyMaxCurve == null || entropyMaxCurve.PointsCount == 0)
            {
                yield return "entropyMaxCurve is empty";
            }

            if (entropyRecoveryFactorCurve == null || entropyRecoveryFactorCurve.PointsCount == 0)
            {
                yield return "entropyRecoveryFactorCurve is empty";
            }

            if (psylinkUnlockLevels.NullOrEmpty())
            {
                yield return "psylinkUnlockLevels is empty";
                yield break;
            }

            for (int i = 0; i < psylinkUnlockLevels.Count; i++)
            {
                int unlock = psylinkUnlockLevels[i];
                if (unlock < 1 || unlock > maxLevel)
                {
                    yield return "psylinkUnlockLevels[" + i + "] = " + unlock + " is outside 1.." + maxLevel;
                }

                if (i > 0 && unlock <= psylinkUnlockLevels[i - 1])
                {
                    yield return "psylinkUnlockLevels must be strictly increasing (entry " + i + ")";
                }
            }
        }
    }
}
