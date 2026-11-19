using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    public readonly struct HealingCandidateSpec : IComparable<HealingCandidateSpec>
    {
        public readonly float Priority;
        public readonly float Cost;
        public readonly bool CanPartialHeal;
        public readonly int SortKey;
        public readonly int SourceIndex;

        public HealingCandidateSpec(float priority, float cost, bool canPartialHeal, int sortKey, int sourceIndex)
        {
            Priority = priority;
            Cost = cost;
            CanPartialHeal = canPartialHeal;
            SortKey = sortKey;
            SourceIndex = sourceIndex;
        }

        public int CompareTo(HealingCandidateSpec other)
        {
            int byPriority = other.Priority.CompareTo(Priority);
            if (byPriority != 0)
            {
                return byPriority;
            }

            int byCost = other.Cost.CompareTo(Cost);
            if (byCost != 0)
            {
                return byCost;
            }

            return SortKey.CompareTo(other.SortKey);
        }
    }

    /// <summary>
    /// Life restoration in three grades, driven by a life pool computed from ritual strength:
    /// the host is healed of their single highest-priority curable condition first, then nearby player
    /// colonists in ascending distance from the altar, one condition per pawn per round until the pool
    /// runs out. Any condition with continuous severity is partially eased when the remaining pool is
    /// smaller than its full cost (updating Severity directly to avoid triggering scar formation in
    /// HediffComp_GetsPermanent.CompPostInjuryHeal); indivisible conditions such as missing limbs,
    /// missing organs and single-stage fixed conditions are skipped when unaffordable so cheaper
    /// conditions can still be treated.
    /// </summary>
    public sealed class RitualEffect_Healing : RitualEffectWorker
    {
        private const float PoolEpsilon = 0.0001f;
        private const float InjuryMinPartialSeverity = 0.05f;
        private const float ConditionMinPartialDelta = 0.0015f;

        private enum CandidateKind
        {
            FreshInjury,
            PermanentInjury,
            MissingPart,
            Condition
        }

        private readonly struct ResolvedCandidate
        {
            public readonly CandidateKind Kind;
            public readonly Hediff Hediff;
            public readonly Hediff_MissingPart MissingPart;
            public readonly float Priority;
            public readonly float Cost;
            public readonly bool CanPartialHeal;
            public readonly int UniqueKey;

            public ResolvedCandidate(
                CandidateKind kind,
                Hediff hediff,
                Hediff_MissingPart missingPart,
                float priority,
                float cost,
                bool canPartialHeal,
                int uniqueKey)
            {
                Kind = kind;
                Hediff = hediff;
                MissingPart = missingPart;
                Priority = priority;
                Cost = cost;
                CanPartialHeal = canPartialHeal;
                UniqueKey = uniqueKey;
            }

            public bool IsInjury => Kind == CandidateKind.FreshInjury || Kind == CandidateKind.PermanentInjury;

            public string DisplayLabel()
            {
                if (Kind == CandidateKind.MissingPart)
                {
                    return MissingPart.Part.Label + " (" + MissingPart.LabelBase + ")";
                }

                return Hediff.Part != null
                    ? Hediff.Part.Label + " " + Hediff.LabelBase
                    : Hediff.LabelBase;
            }

            public string DebugLabel()
            {
                if (Kind == CandidateKind.MissingPart)
                {
                    return "MissingBodyPart(" + MissingPart.Part.def.defName + ")";
                }

                string partName = Hediff.Part != null ? Hediff.Part.def.defName : "wholeBody";
                return Hediff.def.defName + "(" + partName + ", " + Kind + ", sev=" + Hediff.Severity.ToString("0.###") + ")";
            }
        }

        public override void Apply(RitualContext context)
        {
            RitualHealingTableDef table = context.Def.healingTable;
            if (table == null)
            {
                WotfLog.Error(WotfLog.ModuleRitual,
                    context.Def.defName + " invoked RitualEffect_Healing without a healingTable.");
                return;
            }

            HealingTier tier = TierFor(context.Def.ringSize);
            List<Pawn> recipients = CollectRecipients(context);
            float totalPool = context.Def.HealingPoolFor(context.RitualStrength);
            float pool = totalPool;

            var healedPawns = new HashSet<Pawn>();
            var skippedSet = new HashSet<(Pawn, int)>();
            int curedCount = 0;
            int partialCount = 0;

            bool hasBestSkipped = false;
            Pawn bestSkippedPawn = null;
            string bestSkippedLabel = null;
            float bestSkippedCost = 0f;
            float bestSkippedPriority = float.NegativeInfinity;

            bool[] active = new bool[recipients.Count];
            for (int i = 0; i < active.Length; i++)
            {
                active[i] = true;
            }

            var resolved = new List<ResolvedCandidate>();
            var specs = new List<HealingCandidateSpec>();
            int round = 0;
            int step = 0;

            while (pool > PoolEpsilon)
            {
                round++;
                bool progressedInRound = false;
                for (int i = 0; i < recipients.Count && pool > PoolEpsilon; i++)
                {
                    if (!active[i])
                    {
                        continue;
                    }

                    Pawn pawn = recipients[i];
                    if (pawn == null || pawn.Dead || !pawn.Spawned)
                    {
                        active[i] = false;
                        WotfLog.Warning(WotfLog.ModuleRitual,
                            "healing skipped despawned or dead recipient during allocation: " + (pawn?.LabelShort ?? "<null>"));
                        continue;
                    }

                    GatherCandidates(pawn, tier, table, resolved, specs);
                    specs.Sort();
                    PickCandidate(specs, pool, out int chosenSpecIndex, out bool isPartial);

                    int inspectLimit = chosenSpecIndex >= 0 ? chosenSpecIndex : specs.Count;
                    int distSq = (pawn.Position - context.Altar.Position).LengthHorizontalSquared;
                    bool isHost = pawn == context.Caster;

                    for (int s = 0; s < inspectLimit; s++)
                    {
                        ResolvedCandidate skipped = resolved[specs[s].SourceIndex];
                        skippedSet.Add((pawn, skipped.UniqueKey));
                        if (!hasBestSkipped || skipped.Priority > bestSkippedPriority)
                        {
                            hasBestSkipped = true;
                            bestSkippedPawn = pawn;
                            bestSkippedLabel = skipped.DisplayLabel();
                            bestSkippedCost = skipped.Cost;
                            bestSkippedPriority = skipped.Priority;
                        }

                        WotfLog.Debug(WotfLog.ModuleRitual,
                            "  r=" + round + " pawn=" + pawn.LabelShort
                            + (isHost ? "(host" : "(distSq=" + distSq) + (isHost ? ", distSq=" + distSq + ")" : ")")
                            + " target=" + skipped.DebugLabel()
                            + " prio=" + skipped.Priority.ToString("0.#")
                            + " cost=" + skipped.Cost.ToString("0.#")
                            + " -> skipped_unaffordable (pool=" + pool.ToString("0.#") + ")");
                    }

                    if (chosenSpecIndex < 0)
                    {
                        active[i] = false;
                        continue;
                    }

                    step++;
                    progressedInRound = true;
                    ResolvedCandidate chosen = resolved[specs[chosenSpecIndex].SourceIndex];
                    float beforePool = pool;

                    if (!isPartial)
                    {
                        if (chosen.Kind == CandidateKind.MissingPart)
                        {
                            pawn.health.RestorePart(chosen.MissingPart.Part);
                        }
                        else
                        {
                            pawn.health.RemoveHediff(chosen.Hediff);
                        }

                        pool = Mathf.Max(0f, pool - chosen.Cost);
                        curedCount++;
                        healedPawns.Add(pawn);

                        WotfLog.Debug(WotfLog.ModuleRitual,
                            "  r=" + round + " #" + step + " pawn=" + pawn.LabelShort
                            + (isHost ? "(host, distSq=" + distSq + ")" : "(distSq=" + distSq + ")")
                            + " target=" + chosen.DebugLabel()
                            + " prio=" + chosen.Priority.ToString("0.#")
                            + " cost=" + chosen.Cost.ToString("0.#")
                            + " -> cured (pool " + beforePool.ToString("0.#") + "->" + pool.ToString("0.#") + ")");
                    }
                    else
                    {
                        float beforeSeverity = chosen.Hediff.Severity;
                        float afterSeverity = PartialHealSeverity(
                            beforeSeverity,
                            chosen.Hediff.def.minSeverity,
                            chosen.IsInjury,
                            pool,
                            chosen.Cost);

                        // Bypass Hediff_Injury.Heal so HediffComp_GetsPermanent.CompPostInjuryHeal never
                        // converts a partially mended fresh wound into a permanent scar.
                        chosen.Hediff.Severity = afterSeverity;
                        pawn.health.Notify_HediffChanged(chosen.Hediff);

                        float spent = pool;
                        pool = 0f;
                        partialCount++;
                        healedPawns.Add(pawn);

                        WotfLog.Debug(WotfLog.ModuleRitual,
                            "  r=" + round + " #" + step + " pawn=" + pawn.LabelShort
                            + (isHost ? "(host, distSq=" + distSq + ")" : "(distSq=" + distSq + ")")
                            + " target=" + chosen.DebugLabel()
                            + " sev=" + beforeSeverity.ToString("0.###") + "->" + afterSeverity.ToString("0.###")
                            + " prio=" + chosen.Priority.ToString("0.#")
                            + " cost=" + chosen.Cost.ToString("0.#")
                            + " -> partial_healed spent=" + spent.ToString("0.#")
                            + " (pool " + beforePool.ToString("0.#") + "->0)");
                    }
                }

                if (!progressedInRound)
                {
                    break;
                }
            }

            float spentPool = totalPool - pool;
            bool anyRemaining = HasAnyCurable(recipients, tier, table, resolved, specs);
            string outcome;

            if (curedCount == 0 && partialCount == 0)
            {
                if (hasBestSkipped)
                {
                    outcome = "insufficient_pool";
                    Messages.Message(
                        string.Format(
                            "WotF_Ritual_Healing_PoolTooLow".Translate(),
                            totalPool.ToString("0.#"),
                            bestSkippedPawn.LabelShort,
                            bestSkippedLabel,
                            bestSkippedCost.ToString("0.#")),
                        context.Altar,
                        MessageTypeDefOf.RejectInput,
                        false);
                }
                else
                {
                    outcome = "no_targets";
                    Messages.Message(
                        "WotF_Ritual_Healing_NoneNeeded".Translate(),
                        context.Altar,
                        MessageTypeDefOf.NeutralEvent,
                        false);
                }
            }
            else if (!anyRemaining)
            {
                outcome = "all_healed";
                Messages.Message(
                    string.Format(
                        "WotF_Ritual_Healing_AllDone".Translate(),
                        healedPawns.Count,
                        curedCount,
                        spentPool.ToString("0.#"),
                        totalPool.ToString("0.#")),
                    context.Altar,
                    MessageTypeDefOf.PositiveEvent,
                    false);
            }
            else if (partialCount > 0)
            {
                outcome = "pool_exhausted";
                Messages.Message(
                    string.Format(
                        "WotF_Ritual_Healing_PartialDone".Translate(),
                        healedPawns.Count,
                        curedCount,
                        partialCount,
                        spentPool.ToString("0.#"),
                        totalPool.ToString("0.#")),
                    context.Altar,
                    MessageTypeDefOf.PositiveEvent,
                    false);
            }
            else
            {
                outcome = pool <= PoolEpsilon ? "pool_exhausted" : "insufficient_for_remaining";
                Messages.Message(
                    string.Format(
                        "WotF_Ritual_Healing_PoolSpent".Translate(),
                        healedPawns.Count,
                        curedCount,
                        spentPool.ToString("0.#"),
                        totalPool.ToString("0.#")),
                    context.Altar,
                    MessageTypeDefOf.PositiveEvent,
                    false);
            }

            WotfLog.Info(WotfLog.ModuleRitual,
                "healing " + context.Def.defName
                + " strength=" + context.RitualStrength.ToString("0.##")
                + " pool=" + spentPool.ToString("0.#") + "/" + totalPool.ToString("0.#")
                + " candidates=" + recipients.Count
                + " healedPawns=" + healedPawns.Count
                + " cured=" + curedCount
                + " partial=" + partialCount
                + " skipped=" + skippedSet.Count
                + " outcome=" + outcome);
        }

        public static HealingTier TierFor(int ringSize)
        {
            if (ringSize >= 7)
            {
                return HealingTier.Grand;
            }

            return ringSize >= 5 ? HealingTier.Medium : HealingTier.Minor;
        }

        /// <summary>
        /// Deterministic recipient order: the host comes first, then pawns in ascending squared distance
        /// from the altar, breaking ties by thingIDNumber.
        /// </summary>
        public static int[] OrderRecipients(
            IReadOnlyList<IntVec3> positions,
            IReadOnlyList<int> thingIds,
            IntVec3 altarPos,
            int casterIndex)
        {
            int[] order = new int[positions.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, delegate(int a, int b)
            {
                bool aHost = a == casterIndex;
                bool bHost = b == casterIndex;
                if (aHost != bHost)
                {
                    return aHost ? -1 : 1;
                }

                int distA = (positions[a] - altarPos).LengthHorizontalSquared;
                int distB = (positions[b] - altarPos).LengthHorizontalSquared;
                if (distA != distB)
                {
                    return distA.CompareTo(distB);
                }

                return thingIds[a].CompareTo(thingIds[b]);
            });
            return order;
        }

        /// <summary>
        /// Picks the highest-priority actionable candidate for one turn: a candidate whose full cost fits
        /// in <paramref name="pool"/> is cured outright; a candidate that exceeds <paramref name="pool"/>
        /// is partially healed when it has continuous severity, or skipped when indivisible.
        /// </summary>
        public static void PickCandidate(
            IReadOnlyList<HealingCandidateSpec> sortedCandidates,
            float pool,
            out int chosenIndex,
            out bool isPartial)
        {
            for (int i = 0; i < sortedCandidates.Count; i++)
            {
                HealingCandidateSpec candidate = sortedCandidates[i];
                if (candidate.Cost <= pool + PoolEpsilon)
                {
                    chosenIndex = i;
                    isPartial = false;
                    return;
                }

                if (candidate.CanPartialHeal)
                {
                    chosenIndex = i;
                    isPartial = true;
                    return;
                }
            }

            chosenIndex = -1;
            isPartial = false;
        }

        /// <summary>
        /// Proportional severity reduction for a continuous-severity condition when <paramref name="pool"/>
        /// covers only part of <paramref name="cost"/>, clamped strictly above the removal / minSeverity floor.
        /// </summary>
        public static float PartialHealSeverity(
            float currentSeverity,
            float minSeverity,
            bool isInjury,
            float pool,
            float cost)
        {
            float fraction = Mathf.Clamp01(pool / cost);
            float floor = isInjury ? InjuryMinPartialSeverity : Mathf.Max(minSeverity + 0.0005f, 0.001f);
            float target = minSeverity + (currentSeverity - minSeverity) * (1f - fraction);
            return Mathf.Max(floor, target);
        }

        public static bool IsCurableBadCondition(HediffDef def, bool isAddedOrImplant, RitualHealingTableDef table)
        {
            if (def == null || !def.isBad || def.pregnant || isAddedOrImplant || def.countsAsAddedPartOrImplant)
            {
                return false;
            }

            // Psychic entropy is the caster's resource, anesthetic is an active surgical state, and blood
            // loss is already collected across all three tiers.
            if (def == HediffDefOf.PsychicEntropy
                || def == HediffDefOf.Anesthetic
                || def == HediffDefOf.BloodLoss)
            {
                return false;
            }

            return def.everCurableByItem || table.AllowsUncurableByItem(def);
        }

        private static List<Pawn> CollectRecipients(RitualContext context)
        {
            var raw = new List<Pawn>();
            float radius = context.Def.effectRadius;
            IntVec3 altarPos = context.Altar.Position;

            foreach (Pawn pawn in context.Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Dead || !pawn.RaceProps.Humanlike || pawn.Faction != Faction.OfPlayer)
                {
                    continue;
                }

                if (pawn != context.Caster && !pawn.Position.InHorDistOf(altarPos, radius))
                {
                    continue;
                }

                raw.Add(pawn);
            }

            var positions = new IntVec3[raw.Count];
            var thingIds = new int[raw.Count];
            int casterIndex = -1;
            for (int i = 0; i < raw.Count; i++)
            {
                positions[i] = raw[i].Position;
                thingIds[i] = raw[i].thingIDNumber;
                if (raw[i] == context.Caster)
                {
                    casterIndex = i;
                }
            }

            int[] order = OrderRecipients(positions, thingIds, altarPos, casterIndex);
            var ordered = new List<Pawn>(order.Length);
            for (int i = 0; i < order.Length; i++)
            {
                ordered.Add(raw[order[i]]);
            }

            return ordered;
        }

        private static bool HasAnyCurable(
            List<Pawn> recipients,
            HealingTier tier,
            RitualHealingTableDef table,
            List<ResolvedCandidate> resolved,
            List<HealingCandidateSpec> specs)
        {
            for (int i = 0; i < recipients.Count; i++)
            {
                Pawn pawn = recipients[i];
                if (pawn == null || pawn.Dead || !pawn.Spawned)
                {
                    continue;
                }

                GatherCandidates(pawn, tier, table, resolved, specs);
                if (resolved.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void GatherCandidates(
            Pawn pawn,
            HealingTier tier,
            RitualHealingTableDef table,
            List<ResolvedCandidate> resolved,
            List<HealingCandidateSpec> specs)
        {
            resolved.Clear();
            specs.Clear();

            bool bleedEmergency = HealthUtility.TicksUntilDeathDueToBloodLoss(pawn) < table.bleedDeathWindowTicks;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;

            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff is Hediff_Injury injury)
                {
                    bool permanent = injury.IsPermanent();
                    if (permanent && tier < HealingTier.Medium)
                    {
                        continue;
                    }

                    HealingBodyPartEntry partEntry = table.PartFor(injury.Part?.def);
                    HealingInjuryRule rule = permanent ? table.permanentInjury : table.freshInjury;
                    float cost = (rule.baseCost + injury.Severity * rule.costPerSeverity) * partEntry.costFactor;
                    float emergency = (!permanent
                        && (injury.IsCurrentlyLifeThreatening
                            || (bleedEmergency && injury.BleedRate >= table.bleedRateEmergencyThreshold)))
                        ? table.lifeThreateningPriorityBonus
                        : 0f;
                    float bleedTerm = permanent ? 0f : injury.BleedRate * rule.priorityPerBleedRate;
                    float priority = (rule.basePriority + injury.Severity * rule.priorityPerSeverity + bleedTerm)
                        * partEntry.priorityWeight + emergency;
                    bool canPartial = injury.Severity > InjuryMinPartialSeverity + 0.001f;

                    AddCandidate(
                        resolved,
                        specs,
                        new ResolvedCandidate(
                            permanent ? CandidateKind.PermanentInjury : CandidateKind.FreshInjury,
                            injury,
                            null,
                            priority,
                            cost,
                            canPartial,
                            injury.loadID),
                        injury.loadID);
                }
                else if (hediff.def == HediffDefOf.BloodLoss)
                {
                    AddConditionCandidate(hediff, table, resolved, specs);
                }
                else if (tier == HealingTier.Grand)
                {
                    bool isAddedOrImplant = hediff is Hediff_AddedPart
                        || hediff is Hediff_Implant
                        || hediff is Hediff_MissingPart;
                    if (IsCurableBadCondition(hediff.def, isAddedOrImplant, table))
                    {
                        AddConditionCandidate(hediff, table, resolved, specs);
                    }
                }
            }

            if (tier >= HealingTier.Medium)
            {
                // GetMissingPartsCommonAncestors collapses a severed limb's subtree into its root missing
                // part and skips natural child parts removed under an installed prosthetic or bionic.
                List<Hediff_MissingPart> missingAncestors = pawn.health.hediffSet.GetMissingPartsCommonAncestors();
                for (int i = 0; i < missingAncestors.Count; i++)
                {
                    Hediff_MissingPart missing = missingAncestors[i];
                    if (tier == HealingTier.Medium && IsOrgan(missing.Part))
                    {
                        continue;
                    }

                    HealingBodyPartEntry partEntry = table.PartFor(missing.Part.def);
                    float cost = partEntry.regrowCost;
                    float emergency = (missing.IsCurrentlyLifeThreatening
                        || (bleedEmergency && missing.BleedRate >= table.bleedRateEmergencyThreshold))
                        ? table.lifeThreateningPriorityBonus
                        : 0f;
                    float priority = partEntry.regrowPriority + emergency;
                    int sortKey = missing.loadID >= 0 ? missing.loadID : missing.Part.Index;

                    AddCandidate(
                        resolved,
                        specs,
                        new ResolvedCandidate(
                            CandidateKind.MissingPart,
                            missing,
                            missing,
                            priority,
                            cost,
                            canPartialHeal: false,
                            uniqueKey: -1000 - missing.Part.Index),
                        sortKey);
                }
            }
        }

        private static void AddConditionCandidate(
            Hediff hediff,
            RitualHealingTableDef table,
            List<ResolvedCandidate> resolved,
            List<HealingCandidateSpec> specs)
        {
            HealingConditionEntry entry = table.ConditionFor(hediff);
            float partCost = 1f;
            float partPriority = 1f;
            if (entry.applyPartFactors && hediff.Part != null)
            {
                HealingBodyPartEntry partEntry = table.PartFor(hediff.Part.def);
                partCost = partEntry.costFactor;
                partPriority = partEntry.priorityWeight;
            }

            float cost = (entry.baseCost + hediff.Severity * entry.costPerSeverity) * partCost;
            float immuneFactor = hediff.FullyImmune() ? table.immunePriorityFactor : 1f;
            float emergency = hediff.IsCurrentlyLifeThreatening ? table.lifeThreateningPriorityBonus : 0f;
            float priority = (entry.priority + hediff.Severity * entry.priorityPerSeverity)
                * partPriority * immuneFactor + emergency;
            bool canPartial = entry.allowPartialHeal
                && entry.costPerSeverity > 0f
                && hediff.Severity > hediff.def.minSeverity + ConditionMinPartialDelta;

            AddCandidate(
                resolved,
                specs,
                new ResolvedCandidate(
                    CandidateKind.Condition,
                    hediff,
                    null,
                    priority,
                    cost,
                    canPartial,
                    hediff.loadID),
                hediff.loadID);
        }

        private static void AddCandidate(
            List<ResolvedCandidate> resolved,
            List<HealingCandidateSpec> specs,
            ResolvedCandidate candidate,
            int sortKey)
        {
            int index = resolved.Count;
            resolved.Add(candidate);
            specs.Add(new HealingCandidateSpec(
                candidate.Priority,
                candidate.Cost,
                candidate.CanPartialHeal,
                sortKey,
                index));
        }

        private static bool IsOrgan(BodyPartRecord part)
        {
            foreach (BodyPartTagDef tag in part.def.tags)
            {
                if (tag == BodyPartTagDefOf.BloodFiltrationSource
                    || tag == BodyPartTagDefOf.BloodFiltrationLiver
                    || tag == BodyPartTagDefOf.BloodFiltrationKidney
                    || tag == BodyPartTagDefOf.BloodPumpingSource
                    || tag == BodyPartTagDefOf.BreathingSource
                    || tag == BodyPartTagDefOf.BreathingSourceCage
                    || tag == BodyPartTagDefOf.BreathingPathway
                    || tag == BodyPartTagDefOf.ConsciousnessSource
                    || tag == BodyPartTagDefOf.MetabolismSource)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
