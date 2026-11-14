using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    [LabTest]
    public sealed class MechHack_Lifecycle : LabTest
    {
        public override string Name => "mech-hack.lifecycle";

        protected override void Run(TestContext t)
        {
            using var casting = new ResourceTestUtil.FreeCastingScope();
            Pawn caster = t.SpawnCaster(4, 5);
            Pawn mech = MechHack_TestFixture.SpawnMech(t, 10, 5);
            Faction original = mech.Faction;
            MechHack_TestFixture.Cast(t, caster, mech);
            t.Note("assimilating", mech.health.hediffSet.HasHediff(MechHackService.AssimilationDef));
            t.Measure("channelJob", caster.CurJobDef?.defName);
            t.Expect(mech.stances.stunner.Stunned, "assimilating mech was not stunned");
            IntVec3 channelPosition = caster.Position;
            t.Expect(t.StepUntil(() => MechHackService.IsTemporarilyControlled(mech), MechHack_TestFixture.Tuning.maxTicks + 1),
                "assimilation did not transfer control");
            t.Expect(caster.Position == channelPosition, "channeler moved during assimilation");
            t.Step(2);
            t.Expect(caster.CurJobDef != MechHackService.ChannelDef, "completed channel did not finish");
            t.Measure("controlledFaction", mech.Faction?.def.defName);
            t.Note("playerControlled", mech.IsPlayerControlled);
            t.Expect(mech.Faction == caster.Faction && mech.IsPlayerControlled, "mech not player controlled");
            t.Expect(mech.GetOverseer() == null && mech.GetMechControlGroup() == null, "hack assigned an overseer or group");
            MechHack_TestFixture.Draft(t, mech);

            IntVec3 start = mech.Position;
            IntVec3 destination = t.Arena.Cell(12, 5);
            var options = FloatMenuMakerMap.GetOptions(new List<Pawn> { mech }, destination.ToVector3Shifted(), out _);
            FloatMenuOption move = options.FirstOrDefault(o => !o.Disabled && o.Label.Contains("GoHere".Translate().ToString()));
            t.Expect(move != null, "controlled mech has no usable vanilla move option: " + string.Join(" | ", options.Select(o => o.Label)));
            move?.action();
            t.Expect(t.StepUntil(() => mech.Position != start, 120), "controlled mech did not obey move order");
            t.Measure("moved", start + " -> " + mech.Position);

            t.Expect(t.StepUntil(() => !MechHackService.IsTemporarilyControlled(mech), MechHack_TestFixture.Tuning.controlTicks + 1),
                "temporary control did not expire");
            t.Measure("restoredFaction", mech.Faction?.def.defName);
            t.Note("draftedAfterExpiry", mech.Drafted);
            t.Expect(mech.Faction == original && !mech.Drafted, "expiry did not restore original faction undrafted");
        }
    }
}
