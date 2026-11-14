using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Race
{
    public class WotF_FeyAncestryGeneDef : GeneDef
    {
        public float targetLifeExpectancy;
        public float longevityStartAge;
        public float meditationRestEffectiveness;
        public ThingDef tranceCushion;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
                yield return error;
            if (geneClass != typeof(Gene_FeyAncestry))
                yield return "geneClass must be Gene_FeyAncestry";
            if (targetLifeExpectancy <= 0f)
                yield return "targetLifeExpectancy must be positive";
            if (longevityStartAge <= 0f)
                yield return "longevityStartAge must be positive";
            if (meditationRestEffectiveness <= 0f)
                yield return "meditationRestEffectiveness must be positive";
            if (tranceCushion == null)
                yield return "tranceCushion is null";
        }
    }

    public class Gene_FeyAncestry : Gene
    {
        public WotF_FeyAncestryGeneDef Def => (WotF_FeyAncestryGeneDef)def;

        public static Gene_FeyAncestry Get(Pawn pawn)
        {
            Gene_FeyAncestry gene = pawn?.genes?.GetFirstGeneOfType<Gene_FeyAncestry>();
            return gene != null && gene.Active ? gene : null;
        }
    }

    [HarmonyPatch(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.BiologicalAgeTickFactor), MethodType.Getter)]
    internal static class Patch_FeyAncestry_Aging
    {
        private static void Postfix(Pawn_GeneTracker __instance, ref float __result)
        {
            Pawn pawn = __instance.pawn;
            Gene_FeyAncestry gene = Gene_FeyAncestry.Get(pawn);
            if (gene == null || pawn.ageTracker.AgeBiologicalYearsFloat < gene.Def.longevityStartAge)
                return;
            float factor = pawn.RaceProps.lifeExpectancy / gene.Def.targetLifeExpectancy;
            if (factor < 1f)
                __result *= factor;
        }
    }

    [HarmonyPatch(typeof(JobDriver_Meditate), "MeditationTick")]
    internal static class Patch_FeyAncestry_MeditationRest
    {
        private static void Postfix(JobDriver_Meditate __instance)
        {
            Pawn pawn = __instance.pawn;
            Gene_FeyAncestry gene = Gene_FeyAncestry.Get(pawn);
            if (gene == null || pawn.needs.rest == null)
                return;
            Thing focus = __instance.Focus.Thing;
            float strength = focus == null ? 0f : focus.GetStatValueForPawn(StatDefOf.MeditationFocusStrength, pawn);
            pawn.needs.rest.TickResting(gene.Def.meditationRestEffectiveness + strength);
        }
    }

    internal static class FeyTrance
    {
        private static readonly AccessTools.FieldRef<Need, Pawn> NeedPawn = AccessTools.FieldRefAccess<Need, Pawn>("pawn");

        // Replace only the rest predicate at these two call sites, preserving vanilla's other gates.
        internal static RestCategory MeditationRestCategory(Need_Rest rest)
        {
            return Gene_FeyAncestry.Get(NeedPawn(rest)) == null ? rest.CurCategory : RestCategory.Rested;
        }

        internal static Need_Rest RestRequiringBed(Need_Rest rest)
        {
            if (rest == null)
                return null;
            Pawn pawn = NeedPawn(rest);
            if (Gene_FeyAncestry.Get(pawn) == null || pawn.DevelopmentalStage.Baby())
                return rest;
            Building spot = pawn.ownership.AssignedMeditationSpot;
            return spot != null && spot.Spawned && spot.Map == pawn.Map ? null : rest;
        }
    }

    [HarmonyPatch(typeof(MeditationUtility), nameof(MeditationUtility.CanMeditateNow))]
    internal static class Patch_FeyAncestry_MeditationFatigue
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getter = AccessTools.PropertyGetter(typeof(Need_Rest), nameof(Need_Rest.CurCategory));
            MethodInfo replacement = AccessTools.Method(typeof(FeyTrance), nameof(FeyTrance.MeditationRestCategory));
            bool found = false;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(getter))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                    found = true;
                }
                yield return instruction;
            }
            if (!found)
                throw new InvalidOperationException("CanMeditateNow no longer reads Need_Rest.CurCategory.");
        }
    }

    [HarmonyPatch(typeof(Alert_NeedColonistBeds), nameof(Alert_NeedColonistBeds.AvailableColonistBeds))]
    internal static class Patch_FeyAncestry_BedAlert
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo rest = AccessTools.Field(typeof(Pawn_NeedsTracker), nameof(Pawn_NeedsTracker.rest));
            MethodInfo replacement = AccessTools.Method(typeof(FeyTrance), nameof(FeyTrance.RestRequiringBed));
            bool found = false;
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.LoadsField(rest))
                {
                    yield return new CodeInstruction(OpCodes.Call, replacement);
                    found = true;
                }
            }
            if (!found)
                throw new InvalidOperationException("AvailableColonistBeds no longer reads Pawn_NeedsTracker.rest.");
        }
    }

    [HarmonyPatch(typeof(MeditationUtility), nameof(MeditationUtility.AllMeditationSpotCandidates))]
    internal static class Patch_TranceCushion_MeditationCandidates
    {
        private static void Postfix(Pawn pawn, ref IEnumerable<LocalTargetInfo> __result)
        {
            if (!pawn.IsPrisonerOfColony)
                __result = WithCushions(pawn, __result);
        }

        private static IEnumerable<LocalTargetInfo> WithCushions(Pawn pawn, IEnumerable<LocalTargetInfo> original)
        {
            ThingDef cushion = DefDatabase<WotF_FeyAncestryGeneDef>.GetNamed("WotF_FeyAncestry").tranceCushion;
            foreach (Building building in pawn.Map.listerBuildings.AllBuildingsColonistOfDef(cushion))
            {
                if (MeditationUtility.IsValidMeditationBuildingForPawn(building, pawn))
                    yield return building;
            }
            foreach (LocalTargetInfo candidate in original)
                yield return candidate;
        }
    }
}
