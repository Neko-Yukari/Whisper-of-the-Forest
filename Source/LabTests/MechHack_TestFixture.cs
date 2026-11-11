using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    internal static class MechHack_TestFixture
    {
        public static HackGlyphDef Tuning => (HackGlyphDef)TestContext.Glyph("WotF_Effect_Hack");

        public static Pawn SpawnMech(TestContext t, int x, int z)
        {
            Pawn mech = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                DefDatabase<PawnKindDef>.GetNamed("Mech_Scyther"), Faction.OfMechanoids,
                canGeneratePawnRelations: false));
            GenSpawn.Spawn(mech, t.Arena.Cell(x, z), t.Map, WipeMode.Vanish);
            t.Arena.Track(mech);
            mech.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture,
                Tuning.maxTicks + Tuning.controlTicks), JobCondition.InterruptForced);
            return mech;
        }

        public static void Cast(TestContext t, Pawn caster, Pawn mech)
        {
            var book = GameComponent_Spellbook.Current;
            book.Unlock(TestContext.Glyph("WotF_Effect_Hack"));
            var recipe = TestContext.Recipe("hack", SpellDefs.FormProjectile, "WotF_Effect_Hack");
            t.Expect(book.SaveSlot(caster, 0, recipe, out string reason), reason);
            book.SetActiveSlot(caster, 0);
            Command_Target cast = t.SpellAbility(caster).GetGizmos().OfType<Command_Target>().Single();
            t.Expect(!cast.Disabled, "hack gizmo disabled: " + cast.disabledReason);
            SpellRecipe saved = book.GetSlot(caster, 0);
            t.Expect(saved?.targets != null && saved.targets.Contains(SpellTargetClass.Mechanoid),
                "the frozen whitelist lost the mechanoid class");
            t.Expect(cast.targetingParams.CanTarget(new TargetInfo(mech)), "hack native targeter excludes mechanoids");
            t.Expect(cast.targetingParams.validator(new TargetInfo(mech)), "hack gizmo rejected target");
            cast.action(mech);
            t.Expect(t.StepUntil(() => mech.health.hediffSet.HasHediff(MechHackService.AssimilationDef),
                Tuning.maxTicks), "hack never reached assimilation");
        }

        public static void Draft(TestContext t, Pawn mech)
        {
            Command_Toggle draft = mech.GetGizmos().OfType<Command_Toggle>()
                .FirstOrDefault(g => g.hotKey == KeyBindingDefOf.Command_ColonistDraft);
            t.Expect(draft != null && !draft.Disabled, "controlled mech has no usable vanilla draft gizmo");
            if (draft != null && !draft.Disabled) draft.toggleAction();
            t.Expect(mech.Drafted, "draft gizmo did not draft the controlled mech");
        }
    }
}
