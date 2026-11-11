using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    [LabTest(Reload = true)]
    public sealed class MechHack_Reload : LabTest
    {
        private int casterId;
        private int targetId;
        private int controlledId;
        private int channelId;
        private int startTick;
        private int assimilationTicks;
        private int hackControlTicks;

        public override string Name => "mech-hack.reload";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(4, 5);
            Pawn target = MechHack_TestFixture.SpawnMech(t, 10, 5);
            Pawn controlled = MechHack_TestFixture.SpawnMech(t, 14, 5);
            casterId = caster.thingIDNumber;
            targetId = target.thingIDNumber;
            controlledId = controlled.thingIDNumber;

            // Persistence fixture only; the separate lifecycle test covers reaching these states.
            // No ticks here: all reload scenes share a single save, including in-flight projectiles.
            MechHackService.TakeControl(controlled, caster, MechHack_TestFixture.Tuning.controlTicks);
            MechHack_TestFixture.Draft(t, controlled);
            var hack = (HackEffect)((EffectGlyphDef)TestContext.Glyph("WotF_Effect_Hack")).Compile(new List<SpellGlyphDef>(), null);
            t.Expect(MechHackService.Apply(hack, caster, target, target.Position, t.Map) == 1, "reload fixture could not start assimilation");
            hackControlTicks = hack.ControlTicks;
            channelId = caster.CurJob.loadID;
            startTick = t.Tick;
            assimilationTicks = MechHackService.AssimilationTicks(target, hack.HackStrength, hack.BaseTicks, hack.MinTicks, hack.MaxTicks);
            t.Note("channelBeforeSave", channelId);
        }

        protected override void AfterReload(TestContext t)
        {
            Pawn caster = (Pawn)t.Arena.Find(casterId);
            Pawn target = (Pawn)t.Arena.Find(targetId);
            Pawn controlled = (Pawn)t.Arena.Find(controlledId);
            t.Arena.Track(caster);
            t.Arena.Track(target);
            t.Arena.Track(controlled);
            var assimilation = target.health.hediffSet.GetFirstHediffOfDef(MechHackService.AssimilationDef);
            var control = controlled.health.hediffSet.GetFirstHediffOfDef(MechHackService.ControlledDef);
            t.Note("channelAfterLoad", caster.CurJob?.loadID ?? -1);
            t.Measure("controlledAfterLoad", controlled.Faction?.def.defName);
            t.Expect(MechHackService.IsChanneling(caster, channelId), "saved channel job was lost");
            t.Expect(assimilation != null && assimilation.TryGetComp<HediffComp_MechAssimilation>().Caster == caster,
                "saved assimilation lost its caster reference");
            t.Expect(control != null && controlled.Faction == Faction.OfPlayer && controlled.Drafted,
                "saved control or draft state was lost");
            int elapsed = t.Tick - startTick;
            t.Note("elapsedAcrossReload", elapsed);
            t.Measure("assimilationTip", assimilation?.TipStringExtra);
            t.Measure("controlTip", control?.TipStringExtra);
            // Loading advances the map before the runner pauses it; compare at the observed tick.
            string expectedAssimilation = string.Format("WotF_Hack_Remaining".Translate(),
                (assimilationTicks - elapsed).ToStringTicksToPeriod());
            string expectedControl = string.Format("WotF_Hack_Remaining".Translate(),
                (hackControlTicks - elapsed).ToStringTicksToPeriod());
            t.Expect(assimilation?.TipStringExtra.Trim() == expectedAssimilation
                && control?.TipStringExtra.Trim() == expectedControl,
                "saved remaining time differs from elapsed game time: expected " + expectedAssimilation + "; " + expectedControl);
            t.Step(1);
            t.Expect(target.health.hediffSet.HasHediff(MechHackService.AssimilationDef) && target.stances.stunner.Stunned,
                "restored channel did not keep assimilating");
            caster.jobs.EndCurrentJob(JobCondition.InterruptForced);
            t.Expect(!target.health.hediffSet.HasHediff(MechHackService.AssimilationDef),
                "restored channel lost its interruption cleanup");
            controlled.health.RemoveHediff(control);
            t.Expect(controlled.Faction == Faction.OfMechanoids && !controlled.Drafted,
                "saved original faction was not restored when control ended");
        }
    }
}
