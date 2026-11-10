using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Turrets
{
    public class JobDriver_WriteTurretSpell : JobDriver
    {
        private CompSpellTurret Turret => job.targetA.Thing.TryGetComp<CompSpellTurret>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => !Turret.CanInscribe(pawn) || Turret.InscriptionRevision != job.count);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil write = Toils_General.Wait(Turret.Props.inscriptionTicks, TargetIndex.A);
            write.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            yield return write;
            yield return Toils_General.Do(() => Turret.CompleteInscription(job.count));
        }
    }
}
