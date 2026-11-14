using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// The saved book is what the cast gizmo reads: after switching the selected slot, the very
    /// same gizmo must carry the other slot's recipe and cast that one.
    /// </summary>
    [LabTest]
    public sealed class Spellbook_ActiveSlotCast : LabTest
    {
        public override string Name => "spellbook.active-slot-cast";

        protected override void Run(TestContext t)
        {
            using var casting = new ResourceTestUtil.FreeCastingScope();
            GameComponent_Spellbook book = GameComponent_Spellbook.Current;
            if (book == null)
            {
                t.Fail("GameComponent_Spellbook is not present in this game");
                return;
            }

            Pawn caster = t.SpawnCaster(1, 5);
            caster.apparel.DestroyAll();
            caster.apparel.Wear((Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("WotF_Spellbook_Archmage")));
            Pawn target = t.SpawnTarget(9, 5);

            SpellRecipe self = TestContext.Recipe("self-mark", SpellDefs.FormSelf, SpellDefs.EffectHarm);
            // The dart rolls an 8° launch spread, so a legitimate cast can miss and make this
            // handoff test flaky. Compile this one recipe with a straight launch.
            SpellGlyphDef projectileForm = DefDatabase<SpellGlyphDef>.GetNamed(SpellDefs.FormProjectile);
            float spread = projectileForm.projectile.launchSpreadDegrees;
            float wobble = projectileForm.projectile.wobbleAmplitude;
            projectileForm.projectile.launchSpreadDegrees = 0f;
            projectileForm.projectile.wobbleAmplitude = 0f;
            SpellRecipe dart;
            try
            {
                dart = TestContext.Recipe("dart-mark", SpellDefs.FormProjectile, SpellDefs.EffectHarm);
            }
            finally
            {
                projectileForm.projectile.launchSpreadDegrees = spread;
                projectileForm.projectile.wobbleAmplitude = wobble;
            }

            t.Expect(book.SaveSlot(caster, 2, self, out string reasonSelf), "save slot 2 refused: " + reasonSelf);
            t.Expect(book.SaveSlot(caster, 4, dart, out string reasonDart), "save slot 4 refused: " + reasonDart);

            Ability ability = t.SpellAbility(caster);
            if (ability == null)
            {
                return;
            }

            book.SetActiveSlot(caster, 2);
            t.Expect(book.GetActiveSlot(caster) == 2, "active slot should be 2, is " + book.GetActiveSlot(caster));
            Command first = CastGizmo(ability, t);
            if (first == null)
            {
                return;
            }

            t.Expect(first.defaultLabel == "self-mark", "slot 2 gizmo label: expected 'self-mark', got '" + first.defaultLabel + "'");
            t.Expect(first is Command_Action, "slot 2 (self form) should be a plain action gizmo, got " + first.GetType().Name);

            book.SetActiveSlot(caster, 4);
            Command second = CastGizmo(ability, t);
            if (second == null)
            {
                return;
            }

            t.Expect(second.defaultLabel == "dart-mark", "slot 4 gizmo label: expected 'dart-mark', got '" + second.defaultLabel + "'");
            if (!(second is Command_Target targeting))
            {
                t.Fail("slot 4 (projectile form) should be a target gizmo, got " + second.GetType().Name);
                return;
            }

            t.Expect(targeting.targetingParams.validator(new TargetInfo(target)),
                "the slot 4 target gizmo refuses a pawn 8 cells away");
            t.Expect(!targeting.Disabled, "slot 4 cast gizmo is disabled: " + targeting.disabledReason);

            // The gizmo's own callback is the product cast path: firing it must apply slot 4's recipe.
            float before = t.InjurySeverity(target);
            targeting.action(new LocalTargetInfo(target));
            t.StepUntil(() => t.InjurySeverity(target) > before, 300);

            t.Note("targetSeverity", t.InjurySeverity(target));
            t.ExpectGreater(t.InjurySeverity(target), before, "slot 4 target injury severity after the gizmo cast");
        }

        private static Command CastGizmo(Ability ability, TestContext t)
        {
            var commands = new List<Command>();
            foreach (Gizmo gizmo in ability.GetGizmos())
            {
                if (gizmo is Command command)
                {
                    commands.Add(command);
                }
            }

            // The book button is first and always present; the cast button is the second one.
            if (commands.Count < 2)
            {
                t.Fail("expected book + cast gizmos, got " + commands.Count);
                return null;
            }

            return commands[1];
        }
    }
}
