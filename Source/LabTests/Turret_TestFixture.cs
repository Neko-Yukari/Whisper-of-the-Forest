using RimWorld;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;
using WhisperOfTheForest.Turrets;

namespace WhisperOfTheForest.LabTests
{
    internal static class Turret_TestFixture
    {
        internal static T Spawn<T>(TestContext t, string defName, int x, int z, ThingDef stuff = null) where T : Thing
        {
            T thing = (T)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(defName), stuff);
            thing.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(thing, t.Arena.Cell(x, z), t.Map, WipeMode.Vanish);
            return t.Arena.Track(thing);
        }

        internal static void Inscribe(TestContext t, Pawn pawn, Building_SpellTurret turret, SpellRecipe recipe)
        {
            CompSpellTurret comp = turret.GetComp<CompSpellTurret>();
            t.Expect(comp.TryRequestInscription(recipe, out string reason), "request refused: " + reason);
            StartWriting(t, pawn, turret);
            t.Expect(t.StepUntil(() => !comp.HasPendingSpell, comp.Props.inscriptionTicks * 3), "writing did not finish");
        }

        internal static WorkGiver_Scanner Writer =>
            (WorkGiver_Scanner)DefDatabase<WorkGiverDef>.GetNamed("WotF_WriteTurretSpell").Worker;

        internal static void StartWriting(TestContext t, Pawn pawn, Building_SpellTurret turret)
        {
            WorkGiver_Scanner worker = Writer;
            t.Expect(worker.HasJobOnThing(pawn, turret), "WorkGiver refused the writing job");
            Job job = worker.JobOnThing(pawn, turret);
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            t.Measure("orderedJob", pawn.CurJobDef?.defName ?? "none");
            t.Expect(pawn.CurJobDef == turret.GetComp<CompSpellTurret>().Props.inscriptionJob,
                "WorkGiver job did not start");
        }
    }
}
