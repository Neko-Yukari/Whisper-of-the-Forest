using RimWorld;
using Verse;
using Verse.AI.Group;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Finds the psychic ritual a pawn belongs to and cancels it. The short staff is the only
    /// caller: it is aimed at a ritual participant, not at a free pawn.
    /// </summary>
    public static class PsychicRitualDisruption
    {
        /// <summary>The live psychic ritual this pawn participates in, or null.</summary>
        public static PsychicRitual RitualOf(Pawn pawn)
        {
            Lord lord = pawn?.GetLord();
            if (lord?.CurLordToil is LordToil_PsychicRitual toil)
            {
                PsychicRitual ritual = toil.RitualData.psychicRitual;
                if (ritual != null && !ritual.canceled && !ritual.succeeded)
                {
                    return ritual;
                }
            }

            return null;
        }

        /// <summary>True when the pawn is in a psychic ritual the short staff can break.</summary>
        public static bool CanDisrupt(Pawn pawn)
        {
            PsychicRitual ritual = RitualOf(pawn);
            return ritual != null && IsAbduction(ritual);
        }

        private static bool IsAbduction(PsychicRitual ritual)
        {
            return ritual.def is PsychicRitualDef_SkipAbduction || ritual.def is PsychicRitualDef_SkipAbductionPlayer;
        }

        /// <summary>
        /// Cancels a skip-abduction ritual and stuns everyone assigned to it. The target is a
        /// participant, so the ritual is already found by the time this runs.
        /// </summary>
        public static bool TryDisrupt(Pawn caster, Pawn target, int stunTicks, out string reason)
        {
            PsychicRitual ritual = RitualOf(target);
            if (ritual == null)
            {
                reason = "WotF_ShortStaff.Reason.NoRitual".Translate();
                return false;
            }

            if (!IsAbduction(ritual))
            {
                reason = "WotF_ShortStaff.Reason.NotAbduction".Translate();
                return false;
            }

            foreach (Pawn participant in ritual.assignments.AllAssignedPawns)
            {
                participant.stances.stunner.StunFor(stunTicks, caster, addBattleLog: true);
            }

            ritual.CancelPsychicRitual("WotF_ShortStaff.CancelReason".Translate(caster.Named("CASTER")));
            reason = null;
            return true;
        }
    }
}
