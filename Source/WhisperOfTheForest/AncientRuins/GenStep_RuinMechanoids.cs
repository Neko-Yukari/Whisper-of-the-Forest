using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace WhisperOfTheForest.AncientRuins
{
    /// <summary>
    /// The sleeping guards, built the way vanilla's GenStep_SleepingMechanoids builds them
    /// (GenStep_SleepingMechanoids.cs:36-85): a Combat pawn group for the mechanoid faction at the site
    /// part's threat points, spawned around the ruin, put under one LordJob_SleepThenAssaultColony lord
    /// and sent to sleep. The BaseGen "sleepingMechanoids" symbol is deliberately not used: it picks kinds
    /// with GetCombatPawnKindsForPoints, which only takes kinds whose combatPower fits the points, and the
    /// cheapest Core mechanoid costs more than an early-game ruin has, so it chose none and said nothing.
    ///
    /// The points are the single value the ritual side already computed (RuinSeeking.PointsFor, through the
    /// site part's threatPoints). The only change made here is the one vanilla's own sleeping-mechanoid site
    /// makes (SitePartWorker_SleepingMechanoids.cs:42-47): lift them to the faction's minimum Combat group, so
    /// the group maker always has one affordable kind. Site.cs:324-327 counts dormant pawns as active threat,
    /// so the ruin stays uncleared until the guards are dealt with.
    /// </summary>
    public class GenStep_RuinMechanoids : GenStep
    {
        /// <summary>
        /// The ground these guards hold, published for the broken gate's spacing (GenStep_RuinBrokenGate,
        /// order 441 runs after this step). It is the rect the guards' spawn search actually uses, not
        /// just the temple: vanilla spawns them in <c>around.ExpandedBy(8)</c> when it has a rectangle
        /// (SiteGenStepUtility.cs:21-35), and within 10 cells of the single cell otherwise (:25-28).
        /// </summary>
        public const string GuardAreaVar = "WotF_RuinGuardAreaRect";

        public bool sendWokenUpMessage = true;

        public override int SeedPart => 1934416203;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (parms.sitePart == null)
            {
                WotfLog.Error(WotfLog.ModuleRuin, "the ruin guard step ran without a site part; no guards were generated");
                return;
            }

            float sitePoints = parms.sitePart.parms.threatPoints;
            if (sitePoints <= 0f)
            {
                // The quest node writes 0 when the storyteller disallows violent quests; that is the
                // player's difficulty setting, not a failure.
                WotfLog.Info(WotfLog.ModuleRuin, "the ruin has no threat points (violent quests off?); no guards generated");
                return;
            }

            Faction mechanoids = Faction.OfMechanoids;
            if (mechanoids == null)
            {
                WotfLog.Error(WotfLog.ModuleRuin, "Faction.OfMechanoids is null; the ruin generated without guards");
                return;
            }

            float points = Mathf.Max(sitePoints, mechanoids.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat));

            // Defend the temple when there is one, otherwise the same spot vanilla's step picks.
            CellRect around;
            IntVec3 near = IntVec3.Invalid;
            if (MapGenerator.TryGetVar(GenStep_RuinTemple.TempleRectVar, out CellRect temple) && !temple.IsEmpty)
            {
                around = temple;
            }
            else if (!SiteGenStepUtility.TryFindRootToSpawnAroundRectOfInterest(out around, out near, map))
            {
                WotfLog.Error(WotfLog.ModuleRuin, "no temple and no open room near the map centre to put the guards in; the ruin generated without guards");
                return;
            }

            // Publish the ground the guards actually land on, not just the rect they defend: vanilla's own
            // spawn search uses a ring around the rect (SiteGenStepUtility.TryFindSpawnCellAroundOrNear),
            // or everything within 10 cells of the single cell when there is no rect.
            CellRect guarded = near.IsValid ? CellRect.CenteredOn(near, 20, 20) : around.ExpandedBy(8);
            MapGenerator.SetVar(GuardAreaVar, guarded.ClipInsideMap(map));

            var groupParms = new PawnGroupMakerParms
            {
                groupKind = PawnGroupKindDefOf.Combat,
                tile = map.Tile,
                faction = mechanoids,
                points = points,
                seed = SleepingMechanoidsSitePartUtility.GetPawnGroupMakerSeed(parms.sitePart.parms)
            };

            var guards = new List<Pawn>();
            foreach (Pawn pawn in PawnGroupMakerUtility.GeneratePawns(groupParms))
            {
                if (!SiteGenStepUtility.TryFindSpawnCellAroundOrNear(around, near, map, out IntVec3 spawnCell))
                {
                    WotfLog.Error(WotfLog.ModuleRuin, "no free cell around " + around + " for guard " + pawn.LabelShort
                        + "; " + guards.Count + " guards placed");
                    Find.WorldPawns.PassToWorld(pawn);
                    break;
                }

                GenSpawn.Spawn(pawn, spawnCell, map);
                guards.Add(pawn);
            }

            if (guards.Count == 0)
            {
                WotfLog.Error(WotfLog.ModuleRuin, "the ruin generated no guards at " + points.ToString("0.#")
                    + " points (site " + sitePoints.ToString("0.#") + ")");
                return;
            }

            bool wakeUpIfTargetClose = Rand.Bool;
            foreach (Pawn guard in guards)
            {
                CompWakeUpDormant wake = guard.GetComp<CompWakeUpDormant>();
                if (wake != null)
                {
                    wake.wakeUpIfTargetClose = wakeUpIfTargetClose;
                }
            }

            LordMaker.MakeNewLord(mechanoids, new LordJob_SleepThenAssaultColony(mechanoids, sendWokenUpMessage), map, guards);
            GenStep_SleepingMechanoids.SendMechanoidsToSleepImmediately(guards);

            WotfLog.Info(WotfLog.ModuleRuin, "sleeping guards generated: " + guards.Count + " for "
                + points.ToString("0.#") + " points (site " + sitePoints.ToString("0.#") + ") around " + around);
        }
    }
}
