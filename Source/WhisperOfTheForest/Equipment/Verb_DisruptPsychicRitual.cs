using RimWorld;
using Verse;
using WhisperOfTheForest.Rituals;

namespace WhisperOfTheForest.Equipment
{
    /// <summary>
    /// Short staff attack: only skip-abduction participants are valid targets, and a hit
    /// cancels the ritual after stunning everyone taking part in it.
    /// </summary>
    public class Verb_DisruptPsychicRitual : Verb
    {
        private static bool CanDisruptTarget(LocalTargetInfo target)
        {
            return target.Thing is Pawn pawn && PsychicRitualDisruption.CanDisrupt(pawn);
        }

        public override bool CanHitTarget(LocalTargetInfo targ)
        {
            return CanDisruptTarget(targ) && base.CanHitTarget(targ);
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            return CanDisruptTarget(targ) && base.CanHitTargetFrom(root, targ);
        }

        protected override bool TryCastShot()
        {
            Pawn caster = CasterPawn;
            Pawn target = currentTarget.Thing as Pawn;
            if (caster == null || target == null)
            {
                return false;
            }

            CompShortStaff comp = EquipmentSource.GetComp<CompShortStaff>();
            if (!PsychicRitualDisruption.TryDisrupt(caster, target, comp.Props.stunTicks, out string reason))
            {
                if (caster.Faction == Faction.OfPlayer)
                {
                    Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            Messages.Message("WotF_ShortStaff.Disrupted".Translate(target.Named("TARGET")), target, MessageTypeDefOf.PositiveEvent, false);
            return true;
        }
    }
}
