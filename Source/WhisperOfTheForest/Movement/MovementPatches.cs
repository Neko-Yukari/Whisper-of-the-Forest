using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Experimental.Movement
{
    /// <summary>
    /// TerrainStride: the cell base cost passed to <see cref="PathGrid.CalculatedCostAt"/>
    /// is the terrain's own pathCost (see CostToMoveIntoCell IL). Returning 0 there drops
    /// terrain slowdown while impassability checks, building/thing pathCost, snow, sand
    /// and door penalties are still computed by vanilla in the same method. The shared
    /// PathGrid cache is never touched.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.GetPawnCellBaseCostOverride))]
    internal static class Patch_GetPawnCellBaseCostOverride
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, IntVec3 c, ref int? __result)
        {
            // A race/hediff specific override (e.g. water cost) is a property of the pawn,
            // not of the terrain, so it is preserved.
            if (__result.HasValue)
            {
                return;
            }

            if (!StrideApparel.IsWearingTerrainStride(pawn))
            {
                return;
            }

            __result = 0;
            StrideDiagnostics.NoteTerrainCellFreed(pawn, c, GridsUtility.GetTerrain(c, pawn.Map));
        }
    }

    /// <summary>
    /// BlinkStride: after vanilla has moved the pawn into one path node and prepared the
    /// following node (nextCell, cost, door approach notification), hop the pawn onto that
    /// following node and let vanilla prepare the node after it. The hop therefore reuses
    /// the path's own node order, the job, the destination reservation and the move cost
    /// pipeline; the last node is always walked.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_PathFollower), "TryEnterNextPathCell")]
    internal static class Patch_TryEnterNextPathCell
    {
        private const int ErrorOnceKey = 571013;

        private static MethodInfo setupMoveIntoNextCell;
        private static bool setupMoveIntoNextCellLookup;

        [HarmonyPrefix]
        public static void Prefix(Pawn_PathFollower __instance, Pawn ___pawn, out IntVec3 __state)
        {
            __state = ___pawn.Position;
        }

        [HarmonyPostfix]
        public static void Postfix(Pawn_PathFollower __instance, Pawn ___pawn, IntVec3 __state)
        {
            try
            {
                TryBlink(__instance, ___pawn, __state);
            }
            catch (Exception e)
            {
                // Never let an experiment failure escape into the pather tick loop.
                Log.ErrorOnce("[WhisperOfTheForest] BlinkStride patch failed: " + e, ErrorOnceKey);
            }
        }

        private static void TryBlink(Pawn_PathFollower pather, Pawn pawn, IntVec3 positionBeforeCall)
        {
            if (pawn == null || !pawn.Spawned)
            {
                return;
            }

            IntVec3 from = pawn.Position;
            if (from == positionBeforeCall)
            {
                // The call was blocked (door, pawn collision, bash...) and entered no cell.
                return;
            }

            if (StrideApparel.IsWearingAnyStride(pawn))
            {
                StrideDiagnostics.NoteWalked(pawn, positionBeforeCall, from);
            }

            if (!StrideApparel.IsWearingBlinkStride(pawn))
            {
                return;
            }

            if (!pather.Moving)
            {
                return;
            }

            PawnPath path = pather.curPath;
            if (path == null || !path.Found)
            {
                return;
            }

            IntVec3 to = pather.nextCell;
            if (to == from)
            {
                // Arrival, door close, rope and new-path branches return before SetupMoveIntoNextCell.
                return;
            }

            // NodesLeftCount == 1 means 'to' is the destination: walk the last cell normally.
            if (path.NodesLeftCount < 2)
            {
                return;
            }

            string reason = BlinkRule.BlockReason(pawn, from, to);
            if (reason != null)
            {
                StrideDiagnostics.NoteBlocked(pawn, reason);
                return;
            }

            MethodInfo setup = GetSetupMoveIntoNextCell();
            if (setup == null)
            {
                StrideDiagnostics.NoteBlocked(pawn, "missing-setup-method");
                return;
            }

            // Reuses the vanilla cell entry primitive: thing grid update, map regions,
            // drawn position and job target tracking all see a normal position change.
            pawn.Position = to;

            // The pawn covered two cells in one tick; drop the tween cache so the drawn
            // position snaps to the hop instead of sliding through the cell in between.
            PawnTweener tweener = pawn.Drawer != null ? pawn.Drawer.tweener : null;
            if (tweener != null)
            {
                tweener.Notify_Teleported();
            }

            // 'to' was already consumed by vanilla; this consumes the following node and
            // recomputes nextCell/costs, walkability and door approach exactly as usual.
            setup.Invoke(pather, null);

            StrideDiagnostics.NoteBlink(pawn, from, to, path.NodesLeftCount);
        }

        private static MethodInfo GetSetupMoveIntoNextCell()
        {
            if (!setupMoveIntoNextCellLookup)
            {
                setupMoveIntoNextCell = AccessTools.Method(typeof(Pawn_PathFollower), "SetupMoveIntoNextCell");
                setupMoveIntoNextCellLookup = true;
                if (setupMoveIntoNextCell == null)
                {
                    Log.ErrorOnce("[WhisperOfTheForest] Pawn_PathFollower.SetupMoveIntoNextCell not found; BlinkStride disabled for this session.", ErrorOnceKey + 1);
                }
            }

            return setupMoveIntoNextCell;
        }
    }
}
