using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Spells
{
    [HarmonyPatch(typeof(JobGiver_AIFightEnemy), "TryGiveJob")]
    internal static class Patch_JobGiver_AIFightEnemy_Ensnare
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result?.def != JobDefOf.Goto
                || !pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed(SpellEffectDefs.HediffEnsnare)))
                return;
            Thing enemy = pawn.mindState.enemyTarget;
            if (enemy == null)
                return;
            Verb verb = pawn.TryGetAttackVerb(enemy, !pawn.IsColonist && !pawn.IsColonySubhuman);
            if (verb == null || !verb.CanHitTarget(enemy))
                return;

            // Moving to better cover is impossible while snared; keep the AI's reconsideration time.
            Job walking = __result;
            __result = JobMaker.MakeJob(JobDefOf.Wait_Combat, walking.expiryInterval,
                checkOverrideOnExpiry: walking.checkOverrideOnExpire);
            JobMaker.ReturnToPool(walking);
        }
    }
}
