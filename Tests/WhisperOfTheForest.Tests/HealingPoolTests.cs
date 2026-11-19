using System;
using System.Collections.Generic;
using Verse;
using WhisperOfTheForest.Rituals;

namespace WhisperOfTheForest.Tests
{
    internal static class HealingPoolTests
    {
        internal static IEnumerable<(string, Action)> All => new (string, Action)[]
        {
            ("healing: host comes first, then ascending distance from the altar", HostFirstThenDistance),
            ("healing: unaffordable indivisible part is skipped while continuous severity partially heals", SkipIndivisibleAndPartialContinuous),
            ("healing: unlisted modded body part resolves to the single-limb entry", UnlistedPartUsesSingleLimbEntry),
            ("healing: star-ring curability blocks pregnancy and uncurable mechanics unless whitelisted", ConditionCurabilityGate),
        };

        private static void HostFirstThenDistance()
        {
            int[] order = RitualEffect_Healing.OrderRecipients(
                new[] { new IntVec3(6, 0, 0), new IntVec3(1, 0, 0), new IntVec3(8, 0, 0), new IntVec3(1, 0, 0) },
                new[] { 10, 30, 20, 15 },
                new IntVec3(0, 0, 0),
                casterIndex: 2);
            Check.Equal("2,3,1,0", string.Join(",", order), "recipient order");
        }

        private static void SkipIndivisibleAndPartialContinuous()
        {
            var candidates = new List<HealingCandidateSpec>
            {
                new HealingCandidateSpec(priority: 200f, cost: 50f, canPartialHeal: false, sortKey: 1, sourceIndex: 0),
                new HealingCandidateSpec(priority: 100f, cost: 20f, canPartialHeal: true, sortKey: 2, sourceIndex: 1),
                new HealingCandidateSpec(priority: 30f, cost: 4f, canPartialHeal: true, sortKey: 3, sourceIndex: 2),
            };
            candidates.Sort();

            RitualEffect_Healing.PickCandidate(candidates, pool: 8f, out int chosen, out bool isPartial);
            Check.Equal(1, chosen, "skips unaffordable indivisible item and picks highest continuous item");
            Check.True(isPartial, "continuous item exceeding pool is partially healed instead of skipped");

            float after = RitualEffect_Healing.PartialHealSeverity(
                currentSeverity: 10f,
                minSeverity: 0f,
                isInjury: true,
                pool: 8f,
                cost: 20f);
            Check.True(Math.Abs(after - 6f) < 0.001f, $"expected severity 6, got {after}");
        }

        private static void UnlistedPartUsesSingleLimbEntry()
        {
            var listedHead = new BodyPartDef { defName = "T_Heal_Head", hitPoints = 25 };
            var moddedWing = new BodyPartDef { defName = "T_Heal_ModdedWing", hitPoints = 99 };
            DefDatabase<BodyPartDef>.Add(listedHead);
            DefDatabase<BodyPartDef>.Add(moddedWing);

            var singleLimb = new HealingBodyPartEntry
            {
                costFactor = 1f,
                priorityWeight = 1.2f,
                regrowCost = 48f,
                regrowPriority = 180f
            };
            var headEntry = new HealingBodyPartEntry
            {
                part = listedHead,
                costFactor = 2f,
                priorityWeight = 3f,
                regrowCost = 70f,
                regrowPriority = 280f
            };
            var table = new RitualHealingTableDef
            {
                defName = "T_Heal_Table_Parts",
                unlistedLimb = singleLimb,
                bodyParts = new List<HealingBodyPartEntry> { headEntry }
            };
            table.ResolveReferences();

            Check.True(ReferenceEquals(headEntry, table.PartFor(listedHead)), "listed part resolves to its entry");
            Check.True(ReferenceEquals(singleLimb, table.PartFor(moddedWing)), "unlisted modded part resolves to unlistedLimb");
        }

        private static void ConditionCurabilityGate()
        {
            var ordinaryDisease = new HediffDef { defName = "T_Heal_Disease", isBad = true, everCurableByItem = true };
            var labor = new HediffDef { defName = "T_Heal_Labor", isBad = true, pregnant = true, everCurableByItem = true };
            var geneNeed = new HediffDef { defName = "T_Heal_GeneNeed", isBad = true, everCurableByItem = false };
            var crumbledMind = new HediffDef { defName = "T_Heal_Crumbled", isBad = true, everCurableByItem = false };
            DefDatabase<HediffDef>.Add(ordinaryDisease);
            DefDatabase<HediffDef>.Add(labor);
            DefDatabase<HediffDef>.Add(geneNeed);
            DefDatabase<HediffDef>.Add(crumbledMind);

            var table = new RitualHealingTableDef
            {
                defName = "T_Heal_Table_Conds",
                conditions = new List<HealingConditionEntry>
                {
                    new HealingConditionEntry { hediff = crumbledMind, baseCost = 100f, priority = 270f, allowUncurableByItem = true }
                }
            };
            table.ResolveReferences();

            Check.True(RitualEffect_Healing.IsCurableBadCondition(ordinaryDisease, false, table), "ordinary curable disease");
            Check.True(!RitualEffect_Healing.IsCurableBadCondition(labor, false, table), "pregnancy/labor blocked");
            Check.True(!RitualEffect_Healing.IsCurableBadCondition(geneNeed, false, table), "uncurable mechanic blocked");
            Check.True(!RitualEffect_Healing.IsCurableBadCondition(ordinaryDisease, true, table), "added part/implant blocked");
            Check.True(RitualEffect_Healing.IsCurableBadCondition(crumbledMind, false, table), "whitelisted uncurable condition allowed");
        }
    }
}
