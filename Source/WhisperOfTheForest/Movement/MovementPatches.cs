using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Movement
{
    [HarmonyPatch(typeof(Pawn_PathFollower), "CostToMoveIntoCell", typeof(Pawn), typeof(IntVec3))]
    internal static class Patch_CostToMoveIntoCell
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Vanilla Pawn_PathFollower.cs:748-767: replace environmental cost before the cap;
            // leave job urgency, escort speed matching and the final minimum (:771-809) intact.
            var matcher = new CodeMatcher(instructions)
                .MatchStartForward(new CodeMatch(OpCodes.Ldloc_0), new CodeMatch(OpCodes.Ldc_R4, 450f))
                .ThrowIfInvalid("TerrainStride: CostToMoveIntoCell cost-cap anchor not found");
            var loadCost = new CodeInstruction(OpCodes.Ldloc_0);
            loadCost.labels.AddRange(matcher.Instruction.labels);
            matcher.Instruction.labels.Clear();
            matcher.Insert(
                loadCost,
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_1),
                CodeInstruction.Call(typeof(Patch_CostToMoveIntoCell), nameof(WithoutEnvironmentalCost)),
                new CodeInstruction(OpCodes.Stloc_0));
            return matcher.InstructionEnumeration();
        }

        private static float WithoutEnvironmentalCost(float cost, Pawn pawn, IntVec3 c)
        {
            if (!StrideApparel.IsWearingTerrainStride(pawn) || !c.WalkableBy(pawn.Map, pawn))
                return cost;

            Building_Door door = c.GetDoor(pawn.Map);
            // Match the door-wait condition in Pawn_PathFollower.cs:487-494; never bypass opening.
            if (door != null && door.SlowsPawns && (!door.Open || door.TicksTillFullyOpened > 0))
                return cost;

            return c.x != pawn.Position.x && c.z != pawn.Position.z
                ? pawn.TicksPerMoveDiagonal
                : pawn.TicksPerMoveCardinal;
        }
    }
}
