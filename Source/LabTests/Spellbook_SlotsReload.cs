using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// The book is a GameComponent, so its slots and the pawn's selected slot must come back from a
    /// save. The runner saves mid-test and loads the file under the same thing ids.
    /// </summary>
    [LabTest(Reload = true)]
    public sealed class Spellbook_SlotsSurviveReload : LabTest
    {
        private int casterId;
        private Pawn casterReference;

        public override string Name => "spellbook.slots-survive-reload";

        protected override void Run(TestContext t)
        {
            GameComponent_Spellbook book = GameComponent_Spellbook.Current;
            if (book == null)
            {
                t.Fail("GameComponent_Spellbook is not present in this game");
                return;
            }

            Pawn caster = t.SpawnCaster(1, 5);
            casterId = caster.thingIDNumber;
            casterReference = caster;

            SpellRecipe touch = TestContext.Recipe("touch-saved", SpellDefs.FormTouch, SpellDefs.EffectHarm);
            SpellRecipe dart = TestContext.Recipe("dart-saved", SpellDefs.FormProjectile, SpellDefs.EffectHarm);

            t.Expect(book.SaveSlot(caster, 1, touch, out string reasonTouch), "save slot 1 refused: " + reasonTouch);
            t.Expect(book.SaveSlot(caster, 3, dart, out string reasonDart), "save slot 3 refused: " + reasonDart);
            book.SetActiveSlot(caster, 3);
            t.Expect(book.GetActiveSlot(caster) == 3, "active slot should be 3 before the save, is " + book.GetActiveSlot(caster));
            t.Note("activeBeforeSave", book.GetActiveSlot(caster));
        }

        protected override void AfterReload(TestContext t)
        {
            GameComponent_Spellbook book = GameComponent_Spellbook.Current;
            if (book == null)
            {
                t.Fail("GameComponent_Spellbook did not survive the save/load");
                return;
            }

            Thing casterThing = t.Arena.Find(casterId);
            t.Arena.Track(casterThing);
            if (!(casterThing is Pawn caster))
            {
                t.Fail("the caster pawn did not survive the save/load");
                return;
            }

            int active = book.GetActiveSlot(caster);
            SpellRecipe slotOne = book.GetSlot(caster, 1);
            SpellRecipe slotThree = book.GetSlot(caster, 3);
            t.Note("activeAfterLoad", active);
            t.Measure("slotOne", slotOne == null ? "null" : slotOne.ToString());
            t.Measure("slotThree", slotThree == null ? "null" : slotThree.ToString());

            t.Expect(active == 3, "the selected slot was " + active + " after the load, expected 3");
            t.Expect(slotOne != null && slotOne.ToString().Contains("touch-saved"),
                "slot 1 lost its recipe: " + (slotOne?.ToString() ?? "null"));
            t.Expect(slotThree != null && slotThree.ToString().Contains("dart-saved"),
                "slot 3 lost its recipe: " + (slotThree?.ToString() ?? "null"));

            // The gizmo must read the restored slot, not a fresh default.
            Ability ability = t.SpellAbility(caster);
            if (ability == null)
            {
                return;
            }

            var commands = new List<Command>();
            foreach (Gizmo gizmo in ability.GetGizmos())
            {
                if (gizmo is Command command)
                {
                    commands.Add(command);
                }
            }

            t.Note("gizmoCount", commands.Count);
            if (commands.Count < 2)
            {
                t.Fail("expected book + cast gizmos after the load, got " + commands.Count);
                return;
            }

            t.Expect(commands[1].defaultLabel == "dart-saved",
                "cast gizmo label after the load: expected 'dart-saved', got '" + commands[1].defaultLabel + "'");
        }
    }
}
