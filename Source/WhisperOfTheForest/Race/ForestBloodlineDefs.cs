using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Experimental.Race
{
    /// <summary>
    /// One rung of the forest bloodline ladder. The experience needed to leave this level and the
    /// level's mana and neural-heat contribution are data here, so no C# copies a tunable value.
    /// </summary>
    public class WotF_CasterLevelDef : Def
    {
        public int level;
        public float xpToNextLevel;
        public float manaMax;
        public float entropyMaxBonus;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (level < 1)
            {
                yield return "level must be at least 1";
            }

            if (manaMax <= 0f)
            {
                yield return "manaMax must be positive";
            }

            if (entropyMaxBonus < 0f)
            {
                yield return "entropyMaxBonus must not be negative";
            }
        }
    }

    /// <summary>
    /// The bloodline gene's Def. It carries the level table plus the stat and colours the gene
    /// reads, so the gene code never hardcodes a stat name or a bar colour.
    /// </summary>
    public class WotF_ForestBloodlineGeneDef : GeneDef
    {
        public StatDef manaMaxStat;
        public Color manaBarColor;
        public Color manaBarHighlightColor;
        public List<WotF_CasterLevelDef> levelTable;

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

            if (manaMaxStat == null)
            {
                yield return "manaMaxStat is null";
            }

            if (manaBarColor.a <= 0f)
            {
                yield return "manaBarColor is unset (alpha 0)";
            }

            if (manaBarHighlightColor.a <= 0f)
            {
                yield return "manaBarHighlightColor is unset (alpha 0)";
            }

            if (levelTable.NullOrEmpty())
            {
                yield return "levelTable is empty";
                yield break;
            }

            for (int i = 0; i < levelTable.Count; i++)
            {
                WotF_CasterLevelDef levelDef = levelTable[i];
                if (levelDef == null)
                {
                    yield return "levelTable[" + i + "] is null";
                    yield break;
                }

                if (levelDef.level != i + 1)
                {
                    yield return "levelTable[" + i + "] has level " + levelDef.level + ", expected " + (i + 1);
                }

                if (i < levelTable.Count - 1 && levelDef.xpToNextLevel <= 0f)
                {
                    yield return "level " + levelDef.level + " needs a positive xpToNextLevel";
                }
            }
        }
    }
}
