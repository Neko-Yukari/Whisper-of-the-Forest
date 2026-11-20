using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Rituals;

namespace WhisperOfTheForest.Tests
{
    /// <summary>
    /// Seam: ritual research gating requires every RitualDef to declare a researchPrerequisite and
    /// maps existing altar tiers on upgrade to the altar and ritual research projects up through that tier.
    /// </summary>
    internal static class RitualResearchTests
    {
        internal static IEnumerable<(string, Action)> All => new (string, Action)[]
        {
            ("ritual-research: ConfigErrors rejects a RitualDef without researchPrerequisite", RequiresResearchPrerequisite),
            ("ritual-research: save migration unlocks altar and ritual projects up to max altar tier", MigrationUnlocksUpToMaxTier),
            ("ritual-ui-r1: adaptive chip layout fits 10-char labels without ellipsis and truncates safely on overflow", AdaptiveChipLayoutPreservesFittingLabels),
            ("ritual-ui-r1: narrow chip width clamps countBox without overlapping icon box", NarrowChipLayoutClampsCountWithoutIconOverlap),
            ("ritual-ui-r3: diagram (row, col) maps North-up and matches RitualRing border cells", DiagramOffsetMatchesNorthUpRing),
        };

        private static void RequiresResearchPrerequisite()
        {
            var project = new ResearchProjectDef
            {
                defName = "T_Ritual_Research",
                baseCost = 100f,
                techLevel = TechLevel.Neolithic
            };
            var missing = BuildMinimalRitual("T_Ritual_MissingResearch", tier: 1, research: null);
            var valid = BuildMinimalRitual("T_Ritual_WithResearch", tier: 1, research: project);

            Check.True(
                missing.ConfigErrors().Any(e => e.Contains("researchPrerequisite")),
                "RitualDef without researchPrerequisite should report a ConfigError");
            Check.True(
                !valid.ConfigErrors().Any(e => e.Contains("researchPrerequisite")),
                "RitualDef with researchPrerequisite should pass researchPrerequisite check");
        }

        private static void MigrationUnlocksUpToMaxTier()
        {
            var pAltar1 = new ResearchProjectDef { defName = "T_Altar1" };
            var pAltar2 = new ResearchProjectDef { defName = "T_Altar2" };
            var pAltar3 = new ResearchProjectDef { defName = "T_Altar3" };
            var pRite1 = new ResearchProjectDef { defName = "T_Rite1" };
            var pRite2 = new ResearchProjectDef { defName = "T_Rite2" };
            var pRite3 = new ResearchProjectDef { defName = "T_Rite3" };

            var altars = new[]
            {
                BuildMinimalAltar("T_AltarDef1", 1, pAltar1),
                BuildMinimalAltar("T_AltarDef2", 2, pAltar2),
                BuildMinimalAltar("T_AltarDef3", 3, pAltar3),
            };
            var rituals = new[]
            {
                BuildMinimalRitual("T_RiteDef1", 1, pRite1),
                BuildMinimalRitual("T_RiteDef2", 2, pRite2),
                BuildMinimalRitual("T_RiteDef3", 3, pRite3),
            };

            Check.Equal(0, GameComponent_RitualResearch.ProjectsToUnlockForAltarTier(0, altars, rituals).Count, "tier 0 unlocks nothing");

            List<ResearchProjectDef> t1 = GameComponent_RitualResearch.ProjectsToUnlockForAltarTier(1, altars, rituals);
            Check.True(t1.Contains(pAltar1) && t1.Contains(pRite1), "tier 1 unlocks tier 1 altar and ritual");
            Check.True(!t1.Contains(pAltar2) && !t1.Contains(pRite2) && !t1.Contains(pAltar3) && !t1.Contains(pRite3), "tier 1 excludes higher tiers");

            List<ResearchProjectDef> t2 = GameComponent_RitualResearch.ProjectsToUnlockForAltarTier(2, altars, rituals);
            Check.True(t2.Contains(pAltar1) && t2.Contains(pAltar2) && t2.Contains(pRite1) && t2.Contains(pRite2), "tier 2 unlocks tier 1..2");
            Check.True(!t2.Contains(pAltar3) && !t2.Contains(pRite3), "tier 2 excludes tier 3");

            List<ResearchProjectDef> t3 = GameComponent_RitualResearch.ProjectsToUnlockForAltarTier(3, altars, rituals);
            Check.Equal(6, t3.Count, "tier 3 unlocks all 6 test projects");
        }

        private static ThingDef BuildMinimalAltar(string defName, int tier, ResearchProjectDef research)
        {
            return new ThingDef
            {
                defName = defName,
                researchPrerequisites = new List<ResearchProjectDef> { research },
                comps = new List<CompProperties> { new CompProperties_RitualAltar { tier = tier } }
            };
        }

        private static RitualDef BuildMinimalRitual(string defName, int tier, ResearchProjectDef research)
        {
            return new RitualDef
            {
                defName = defName,
                tier = tier,
                family = RitualFamily.Crafting,
                durationTicks = 60,
                effectWorkerClass = typeof(RitualEffect_ManaSpring),
                researchPrerequisite = research
            };
        }

        private static void AdaptiveChipLayoutPreservesFittingLabels()
        {
            const float minInnerWidth = 311f;
            // Typical 10-char CJK item label (~148px at GameFont.Small) + count "×10" (~28px)
            const float tenCharW = 148f;
            const float countW = 28f;

            float chipW = RitualLayoutMath.MeasureChipWidth(tenCharW, countW, hasCount: true, minInnerWidth);
            Check.True(chipW <= minInnerWidth, "10-char chip fits within min window innerWidth");

            RitualLayoutMath.ComputeChipTextLayout(
                0f, chipW, tenCharW, countW, hasLabel: true, hasCount: true,
                out float labelX, out float availLabelW, out bool needsEllipsis,
                out float countX, out float countBoxW);

            Check.True(!needsEllipsis, "fitting label (<= 10 chars) must not trigger ellipsis truncation");
            Check.True(availLabelW >= tenCharW + RitualLayoutMath.TextSafetyMargin - 0.01f,
                "available label width preserves full measured label width + safety margin");
            Check.True(countX >= labelX + availLabelW + RitualLayoutMath.ChipLabelCountGap - 0.01f,
                "count box starts after label + gap with zero overlap");
            Check.True(countX + countBoxW + RitualLayoutMath.ChipPadRight <= chipW + 0.01f,
                "count box stays inside chip right padding");

            // Overflowing >10-char label clamps cleanly to container width and truncates without touching count
            const float longLabelW = 340f;
            float clampedW = RitualLayoutMath.MeasureChipWidth(longLabelW, countW, hasCount: true, minInnerWidth);
            Check.Equal(minInnerWidth, clampedW, "oversized label clamps to innerWidth");

            RitualLayoutMath.ComputeChipTextLayout(
                0f, clampedW, longLabelW, countW, hasLabel: true, hasCount: true,
                out float ovLabelX, out float ovAvailW, out bool ovEllipsis,
                out float ovCountX, out float ovCountBoxW);

            Check.True(ovEllipsis, "oversized label triggers ellipsis truncation");
            Check.True(ovCountX >= ovLabelX + ovAvailW + RitualLayoutMath.ChipLabelCountGap - 0.01f,
                "truncated label never collides with count text");
            Check.True(ovCountX + ovCountBoxW + RitualLayoutMath.ChipPadRight <= clampedW + 0.01f,
                "count remains inside clamped chip width");
        }

        private static void NarrowChipLayoutClampsCountWithoutIconOverlap()
        {
            float iconClearLeft = RitualLayoutMath.ChipPadLeft + RitualLayoutMath.ChipIconSize + RitualLayoutMath.ChipIconGap;

            RitualLayoutMath.ComputeChipTextLayout(
                0f, RitualLayoutMath.ChipMinWidth, 120f, 68f, hasLabel: true, hasCount: true,
                out _, out float availW, out bool needsEllipsis,
                out float countX, out float countBoxW);

            Check.True(countX >= iconClearLeft - 0.01f,
                $"narrow chip countX ({countX}) must stay >= icon clearance ({iconClearLeft})");
            Check.True(countX + countBoxW + RitualLayoutMath.ChipPadRight <= RitualLayoutMath.ChipMinWidth + 0.01f,
                "clamped count box stays inside narrow chip right padding");
            Check.True(availW == 0f && needsEllipsis,
                "synthetic narrow chip leaves 0 label width and flags ellipsis");

            RitualLayoutMath.ComputeChipTextLayout(
                10f, 30f, 50f, 20f, hasLabel: true, hasCount: true,
                out _, out float tinyAvailW, out _,
                out float tinyCountX, out float tinyCountBoxW);

            Check.True(tinyCountX >= 10f + iconClearLeft - 0.01f,
                "sub-chrome chip width still keeps countX >= chipX + 36f");
            Check.True(tinyCountBoxW == 0f && tinyAvailW == 0f,
                "sub-chrome chip width clamps countBoxW and availLabelW to 0");
        }

        private static void DiagramOffsetMatchesNorthUpRing()
        {
            IntVec3 center = new IntVec3(50, 0, 50);
            for (int size = 3; size <= 7; size += 2)
            {
                int radius = (size - 1) / 2;
                Check.Equal(new IntVec3(0, 0, radius), RitualLayoutMath.DiagramOffset(0, radius, radius),
                    $"size {size} top-center must be North (+Z)");
                Check.Equal(new IntVec3(0, 0, -radius), RitualLayoutMath.DiagramOffset(size - 1, radius, radius),
                    $"size {size} bottom-center must be South (-Z)");
                Check.Equal(new IntVec3(-radius, 0, 0), RitualLayoutMath.DiagramOffset(radius, 0, radius),
                    $"size {size} middle-left must be West (-X)");
                Check.Equal(new IntVec3(radius, 0, 0), RitualLayoutMath.DiagramOffset(radius, size - 1, radius),
                    $"size {size} middle-right must be East (+X)");
                Check.Equal(IntVec3.Zero, RitualLayoutMath.DiagramOffset(radius, radius, radius),
                    $"size {size} center must be altar (0,0,0)");

                var ringCells = new HashSet<IntVec3>(RitualRing.Cells(center, size));
                int mappedRingCount = 0;
                for (int row = 0; row < size; row++)
                {
                    for (int col = 0; col < size; col++)
                    {
                        IntVec3 offset = RitualLayoutMath.DiagramOffset(row, col, radius);
                        bool isBorder = Math.Max(Math.Abs(offset.x), Math.Abs(offset.z)) == radius;
                        if (isBorder)
                        {
                            mappedRingCount++;
                            Check.True(ringCells.Contains(center + offset),
                                $"size {size} diagram border cell ({row},{col}) maps to RitualRing cell");
                        }
                    }
                }

                Check.Equal(ringCells.Count, mappedRingCount,
                    $"size {size} diagram border cell count matches RitualRing.Cells");
            }
        }
    }
}
