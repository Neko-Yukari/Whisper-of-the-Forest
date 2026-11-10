using RimWorld;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Turrets
{
    public class WorkGiver_WriteTurretSpell : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompSpellTurret turret = t.TryGetComp<CompSpellTurret>();
            return turret != null && turret.CanInscribe(pawn) && !t.IsForbidden(pawn)
                && pawn.CanReserveAndReach(t, PathEndMode.Touch, pawn.NormalMaxDanger(), 1, -1, null, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompSpellTurret turret = t.TryGetComp<CompSpellTurret>();
            Job job = JobMaker.MakeJob(turret.Props.inscriptionJob, t);
            // Keep a replacement request from borrowing work already spent on the old request.
            job.count = turret.InscriptionRevision;
            return job;
        }
    }
}
