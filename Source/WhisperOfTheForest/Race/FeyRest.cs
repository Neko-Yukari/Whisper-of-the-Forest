using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Race
{
    internal static class FeyRest
    {
        internal static bool ReplacesSleep(Pawn pawn)
        {
            return Gene_FeyAncestry.Get(pawn) != null
                && !pawn.Downed && !pawn.IsPrisoner
                && !pawn.DevelopmentalStage.Baby() && !pawn.DevelopmentalStage.Newborn()
                && !HealthAIUtility.ShouldSeekMedicalRest(pawn);
        }

        // The vanilla giver is saved with the job; no second flag or custom job format is needed.
        internal static bool IsRestMeditation(Job job)
        {
            return job.def == JobDefOf.Meditate && job.jobGiver is JobGiver_GetRest;
        }

        internal static TimeAssignmentDef MeditationTimeAssignment(Pawn pawn)
        {
            // Only the meditation tick's psyfocus/ timetable exit logic sees this value.
            return IsRestMeditation(pawn.CurJob) ? TimeAssignmentDefOf.Meditate : pawn.GetTimeAssignment();
        }
    }

    [HarmonyPatch(typeof(JobGiver_GetRest), "TryGiveJob")]
    internal static class Patch_FeyRest_GetRest
    {
        private static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result?.def != JobDefOf.LayDown || !FeyRest.ReplacesSleep(pawn))
                return;

            Job restJob = __result;
            __result = null;
            if (!MeditationUtility.CanMeditateNow(pawn))
            {
                JobMaker.ReturnToPool(restJob);
                return;
            }

            Building assigned = pawn.ownership.AssignedMeditationSpot;
            MeditationSpotAndFocus destination;
            if (assigned != null && assigned.Map == pawn.Map
                && MeditationUtility.IsValidMeditationBuildingForPawn(assigned, pawn)
                && MeditationUtility.SafeEnvironmentalConditions(pawn, assigned.Position, pawn.Map))
            {
                destination = new MeditationSpotAndFocus(assigned, MeditationUtility.BestFocusAt(assigned, pawn));
            }
            else
            {
                destination = MeditationUtility.FindMeditationSpot(pawn);
            }

            if (!destination.IsValid
                || !pawn.CanReserveAndReach(destination.spot, PathEndMode.OnCell, pawn.NormalMaxDanger()))
            {
                JobMaker.ReturnToPool(restJob);
                return;
            }

            restJob.def = JobDefOf.Meditate;
            restJob.targetA = destination.spot;
            restJob.targetB = LocalTargetInfo.Invalid;
            restJob.targetC = destination.focus;
            restJob.ignoreJoyTimeAssignment = true;
            __result = restJob;
        }
    }

    [HarmonyPatch(typeof(JobDriver_Meditate), "MakeNewToils")]
    internal static class Patch_FeyRest_MeditationDuration
    {
        private static void Postfix(JobDriver_Meditate __instance, ref IEnumerable<Toil> __result)
        {
            if (FeyRest.IsRestMeditation(__instance.job))
                __result = UntilRested(__instance, __result);
        }

        private static IEnumerable<Toil> UntilRested(JobDriver_Meditate driver, IEnumerable<Toil> toils)
        {
            foreach (Toil toil in toils)
            {
                if (toil.defaultCompleteMode == ToilCompleteMode.Delay)
                {
                    toil.defaultCompleteMode = ToilCompleteMode.Never;
                    toil.AddEndCondition(() => !FeyRest.ReplacesSleep(driver.pawn)
                        ? JobCondition.InterruptForced
                        : driver.pawn.needs.rest.CurLevelPercentage >= 1f
                            ? JobCondition.Succeeded : JobCondition.Ongoing);
                }
                yield return toil;
            }
        }
    }

    [HarmonyPatch]
    internal static class Patch_FeyRest_MeditationExit
    {
        private static MethodBase TargetMethod()
        {
            MethodInfo getter = AccessTools.Method(typeof(TimetableUtility), nameof(TimetableUtility.GetTimeAssignment));
            // Locate the generated pre-tick callback by its call, not a compiler-generated ordinal.
            MethodInfo[] matches = typeof(JobDriver_Meditate).GetNestedTypes(AccessTools.all)
                .Append(typeof(JobDriver_Meditate))
                .SelectMany(AccessTools.GetDeclaredMethods)
                .Where(method => method.Name.Contains("<MakeNewToils>") && method.GetMethodBody() != null)
                .Where(method => PatchProcessor.GetOriginalInstructions(method).Any(instruction => instruction.Calls(getter)))
                .ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException("Expected one meditation pre-tick callback reading GetTimeAssignment; found " + matches.Length);
            return matches[0];
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getter = AccessTools.Method(typeof(TimetableUtility), nameof(TimetableUtility.GetTimeAssignment));
            MethodInfo replacement = AccessTools.Method(typeof(FeyRest), nameof(FeyRest.MeditationTimeAssignment));
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(getter))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                }
                yield return instruction;
            }
        }
    }
}
