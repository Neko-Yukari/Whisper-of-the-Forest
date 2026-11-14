using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    [LabTest(Reload = true)]
    public sealed class Casting_TouchCellRelease : LabTest
    {
        private int casterId;
        private int witnessId;
        private int goneCasterId;
        private int goneWitnessId;
        private bool cellAccepted;
        private bool cellCastStarted;

        public override string Name => "casting.touch-cell-release";

        protected override void Run(TestContext t)
        {
            using var casting = new ResourceTestUtil.FreeCastingScope();
            Pawn caster = t.SpawnCaster(3, 4);
            Pawn witness = t.SpawnTarget(5, 4);
            IntVec3 cell = t.Arena.Cell(4, 4);
            casterId = caster.thingIDNumber;
            witnessId = witness.thingIDNumber;
            Command_Target command = Prepare(t, caster, "touch-cell");
            cellAccepted = command.targetingParams.CanTarget(new TargetInfo(cell, t.Map));
            t.Expect(cellAccepted, "native targeter refused the adjacent empty cell");
            t.Expect(!command.targetingParams.CanTarget(new TargetInfo(t.Arena.Cell(8, 4), t.Map)),
                "touch targeting reach expanded to a distant cell");
            if (!cellAccepted) return;
            command.action(cell);
            cellCastStarted = SpellCastingService.IsCasting(caster);
            t.Expect(cellCastStarted, "cell click never started a cast");

            // A picked Thing disappears before the merged save. Its missing reference must not
            // turn into a cell cast after loading, even though the spell also allows Cell.
            Pawn goneCaster = t.SpawnCaster(12, 4);
            Pawn goneTarget = t.SpawnTarget(13, 4);
            Pawn goneWitness = t.SpawnTarget(14, 4);
            goneCasterId = goneCaster.thingIDNumber;
            goneWitnessId = goneWitness.thingIDNumber;
            Command_Target goneCommand = Prepare(t, goneCaster, "touch-gone-thing");
            t.Expect(goneCommand.targetingParams.CanTarget(new TargetInfo(goneTarget)), "native targeter refused the pawn");
            goneCommand.action(goneTarget);
            t.Expect(SpellCastingService.IsCasting(goneCaster), "thing click never started a cast");
            goneTarget.Destroy(DestroyMode.Vanish);
            // No tick advancement: both target identities must survive the single merged save.
        }

        protected override void AfterReload(TestContext t)
        {
            t.Note("cellAccepted", cellAccepted);
            t.Note("cellCastStarted", cellCastStarted);
            Pawn caster = t.Arena.Track((Pawn)t.Arena.Find(casterId));
            Pawn witness = t.Arena.Track((Pawn)t.Arena.Find(witnessId));
            Pawn goneCaster = t.Arena.Track((Pawn)t.Arena.Find(goneCasterId));
            Pawn goneWitness = t.Arena.Track((Pawn)t.Arena.Find(goneWitnessId));
            HediffDef freeze = DefDatabase<HediffDef>.GetNamed("WotF_Hediff_Freeze");
            bool frozen = t.StepUntil(() => witness.health.hediffSet.HasHediff(freeze), 120);
            t.Note("cellEffectApplied", frozen);
            t.Expect(frozen, "accepted adjacent cell never applied its freeze effect at release");
            t.Expect(t.StepUntil(() => !SpellCastingService.IsCasting(caster)
                && !SpellCastingService.IsCasting(goneCaster), 120), "touch casts did not settle");
            bool goneApplied = goneWitness.health.hediffSet.HasHediff(freeze);
            t.Note("goneThingEffectApplied", goneApplied);
            t.Expect(!goneApplied, "missing Thing incorrectly resolved as an area cell cast");
        }

        private static Command_Target Prepare(TestContext t, Pawn caster, string name)
        {
            var book = GameComponent_Spellbook.Current;
            book.Unlock(TestContext.Glyph("WotF_Augment_AOE"));
            book.Unlock(TestContext.Glyph("WotF_Effect_Freeze"));
            var recipe = TestContext.Recipe(name, SpellDefs.FormTouch, "WotF_Effect_Freeze", "WotF_Augment_AOE");
            t.Expect(book.SaveSlot(caster, 0, recipe, out string reason), reason);
            book.SetActiveSlot(caster, 0);
            var command = t.SpellAbility(caster).GetGizmos().OfType<Command_Target>().Single();
            t.Expect(!command.Disabled, "touch gizmo disabled: " + command.disabledReason);
            return command;
        }
    }
}
