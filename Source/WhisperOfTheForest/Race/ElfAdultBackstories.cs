using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Race
{
    [HarmonyPatch(typeof(LifeStageWorker_HumanlikeAdult), nameof(LifeStageWorker_HumanlikeAdult.Notify_LifeStageStarted))]
    internal static class Patch_ElfAdultBackstories
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo original = AccessTools.Method(typeof(PawnBioAndNameGenerator), nameof(PawnBioAndNameGenerator.FillBackstorySlotShuffled));
            MethodInfo replacement = AccessTools.Method(typeof(Patch_ElfAdultBackstories), nameof(FillElfBackstorySlot));
            bool replaced = false;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(original))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                    replaced = true;
                }
                yield return instruction;
            }
            if (!replaced)
                throw new InvalidOperationException("Elf adult backstories: Notify_LifeStageStarted has no FillBackstorySlotShuffled call");
        }

        private static void FillElfBackstorySlot(Pawn pawn, BackstorySlot slot,
            List<BackstoryCategoryFilter> backstoryCategories, FactionDef factionType,
            BackstorySlot? mustBeCompatibleTo)
        {
            // Vanilla replaces childhood as well as adulthood on this birthday, including vat-grown
            // childhoods. HAR only redirects the tribal/vat filters, not AdultColonist. Reuse the
            // starting elf's XML filters at all three call sites without changing other generators.
            if (pawn.def.defName == "WotF_Elf")
                backstoryCategories = DefDatabase<PawnKindDef>.GetNamed("WotF_Elf_Colonist").backstoryFiltersOverride;

            PawnBioAndNameGenerator.FillBackstorySlotShuffled(pawn, slot, backstoryCategories,
                factionType, mustBeCompatibleTo);
        }
    }
}
