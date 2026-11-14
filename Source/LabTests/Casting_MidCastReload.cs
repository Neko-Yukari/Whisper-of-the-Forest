using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Cast -> save -> load handoff: a cast taken in the middle of its phases still resolves after the
    /// load. Phase one asserts the cast is running when the save is written, because a cast that had
    /// already finished would leave nothing to restore and the scenario would not exist.
    /// </summary>
    [LabTest(Reload = true)]
    public sealed class Casting_MidCastReload : LabTest
    {
        private int casterId;
        private int targetId;
        private float severityBefore;

        public override string Name => "casting.mid-cast-reload";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(2, 5);
            var mana = BloodlineKit.Gene(t, caster);
            mana.RefreshMaxFromStats();
            mana.Value = mana.MaxMana;
            Pawn target = t.SpawnTarget(12, 5);
            casterId = caster.thingIDNumber;
            targetId = target.thingIDNumber;
            severityBefore = t.InjurySeverity(target);

            SpellRecipe recipe = TestContext.Recipe("saved-cast", SpellDefs.FormProjectile, SpellDefs.EffectHarm);
            if (!t.CastAt(caster, recipe, target))
            {
                return;
            }

            t.Note("castingAtSave", SpellCastingService.IsCasting(caster));
            t.Expect(SpellCastingService.IsCasting(caster), "the cast had finished before the save; there is nothing to restore");
        }

        protected override void AfterReload(TestContext t)
        {
            Thing casterThing = t.Arena.Find(casterId);
            Thing targetThing = t.Arena.Find(targetId);
            t.Arena.Track(casterThing);
            t.Arena.Track(targetThing);

            if (!(casterThing is Pawn caster) || !(targetThing is Pawn target))
            {
                t.Fail("the caster or the target did not survive the save/load");
                return;
            }

            t.Note("castingAfterLoad", SpellCastingService.IsCasting(caster));
            bool hit = t.StepUntil(() => t.InjurySeverity(target) > severityBefore, 600);
            t.Note("hitAfterLoad", hit);
            t.Note("targetSeverity", t.InjurySeverity(target));
            t.Expect(hit, "the restored cast never resolved");
        }
    }
}
