using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Rituals;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Diagnostics for the beast-call rite. Two things can leave the altar without an animal: the
    /// ritual job ends any way but Succeeded (the altar then cancels the rite and its mana and
    /// offerings are gone), or the one taming roll refuses the summoned animal. Neither writes a line
    /// of its own, so both are recorded here; one line per rite at most.
    /// </summary>
    internal static class RitualWatchPatches
    {
        [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
        internal static class Patch_RitualJobEnd
        {
            private static void Prefix(Pawn_JobTracker __instance, JobCondition condition)
            {
                Job job = __instance.curJob;
                JobDriver driver = __instance.curDriver;
                Pawn pawn = driver == null ? null : driver.pawn;
                if (pawn == null || job == null || job.def == null || job.def.defName != "WotF_PerformRitual")
                {
                    return;
                }

                Log.Message("[WotF-Lab] ritual job ended condition=" + condition
                    + " toil=" + (driver == null ? -1 : driver.CurToilIndex)
                    + " ticksLeft=" + (driver == null ? -1 : driver.ticksLeftThisToil)
                    + " downed=" + pawn.Downed
                    + " drafted=" + pawn.Drafted
                    + " mental=" + (pawn.InMentalState ? pawn.MentalStateDef.defName : "none")
                    + " food=" + (pawn.needs?.food?.CurLevel ?? -1f).ToString("0.##")
                    + " rest=" + (pawn.needs?.rest?.CurLevel ?? -1f).ToString("0.##")
                    + " hediffs=" + pawn.health.hediffSet.hediffs.Count
                    + " altarSpawned=" + (job.targetA.Thing != null && job.targetA.Thing.Spawned));
            }
        }

        [HarmonyPatch(typeof(RitualEffect_BeastCall), nameof(RitualEffect_BeastCall.TryTame))]
        internal static class Patch_BeastCallTaming
        {
            private struct Before
            {
                internal bool CanTame;
                internal float Wildness;
                internal bool Scaria;
            }

            // TameUtility.CanTame is false for any animal that is already tame, so the state that
            // decided the roll has to be read before the call, not after it.
            private static void Prefix(Pawn animal, out Before __state)
            {
                __state = new Before
                {
                    CanTame = TameUtility.CanTame(animal),
                    Wildness = animal.GetStatValue(StatDefOf.Wildness),
                    Scaria = animal.health.hediffSet.HasHediff(HediffDefOf.Scaria)
                };
            }

            private static void Postfix(Pawn animal, float chance, bool __result, Before __state)
            {
                Log.Message("[WotF-Lab] beast-call taming chance=" + chance.ToString("0.###")
                    + " canTame=" + __state.CanTame
                    + " wildness=" + __state.Wildness.ToString("0.##")
                    + " scaria=" + __state.Scaria
                    + " spawned=" + animal.Spawned
                    + " tamed=" + __result);
            }
        }
    }
}

