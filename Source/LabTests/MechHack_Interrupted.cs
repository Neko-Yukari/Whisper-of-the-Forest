using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    [LabTest]
    public sealed class MechHack_Interrupted : LabTest
    {
        public override string Name => "mech-hack.interrupted";

        protected override void Run(TestContext t)
        {
            using var casting = new ResourceTestUtil.FreeCastingScope();
            Pawn caster = t.SpawnCaster(4, 5);
            Pawn mech = MechHack_TestFixture.SpawnMech(t, 10, 5);
            Faction original = mech.Faction;
            MechHack_TestFixture.Cast(t, caster, mech);
            t.Expect(mech.stances.stunner.Stunned, "assimilation did not stun target");
            int oldChannel = caster.CurJob.loadID;
            t.WalkTo(caster, t.Arena.Cell(3, 5), 1);
            t.Step(2);
            t.Measure("jobAfterOrder", caster.CurJobDef?.defName);
            t.Measure("factionAfterOrder", mech.Faction?.def.defName);
            t.Expect(!MechHackService.IsChanneling(caster, oldChannel), "move order did not interrupt channel");
            t.Expect(!mech.health.hediffSet.HasHediff(MechHackService.AssimilationDef), "interruption retained assimilation");
            t.Expect(mech.Faction == original && mech.HostileTo(caster), "interrupted target lost its original hostility");
            t.Expect(!mech.stances.stunner.Stunned, "interrupted target retained the assimilation stun");
            t.Expect(!MechHackService.IsTemporarilyControlled(mech), "interrupted target became controlled");
        }
    }
}
