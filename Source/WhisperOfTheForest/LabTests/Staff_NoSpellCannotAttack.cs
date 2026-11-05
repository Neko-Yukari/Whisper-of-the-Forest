using RimWorld;
using Verse;
using WhisperOfTheForest.Experimental.Equipment;
using WhisperOfTheForest.Experimental.Spells;
using WhisperOfTheForest.Experimental.Testing;

namespace WhisperOfTheForest.Experimental.LabTests
{
    /// <summary>
    /// No book, no cast: a staff whose chosen bookmark is empty (never chosen, or pointing at a
    /// cleared page) must not attack at all. The native verb reports unavailable and the attack
    /// order is refused before the casting service is ever reached.
    /// </summary>
    [LabTest]
    public sealed class Staff_NoSpellCannotAttack : LabTest
    {
        public override string Name => "staff.no-spell-cannot-attack";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(1, 5);
            Pawn target = t.SpawnTarget(9, 5);

            ThingWithComps staff = GiveStaff(t, caster);
            if (staff == null)
            {
                return;
            }

            CompSpellStaff comp = staff.GetComp<CompSpellStaff>();
            Verb verb = caster.equipment.PrimaryEq.PrimaryVerb;

            t.Expect(comp.Slot == CompSpellStaff.NoSlot, "a new staff should have no bookmark, has " + comp.Slot);
            t.Expect(!verb.Available(), "a staff with no chosen bookmark reports available");
            t.Expect(!verb.TryStartCastOn(new LocalTargetInfo(target)),
                "the native verb accepted a cast without a spell");
            t.Expect(!caster.TryStartAttack(new LocalTargetInfo(target)),
                "the native attack order was accepted without a spell");
            t.Step(120);
            t.Expect(!SpellCastingService.IsCasting(caster), "a cast started without a spell");
            t.Expect(t.InjurySeverity(target) == 0f, "the target was injured without a spell: " + t.InjurySeverity(target));

            // A bookmark that exists but was cleared must behave the same as no bookmark at all.
            GameComponent_Spellbook book = GameComponent_Spellbook.Current;
            if (book == null)
            {
                t.Fail("GameComponent_Spellbook is not present in this game");
                return;
            }

            SpellRecipe recipe = TestContext.Recipe("staff-cleared", SpellDefs.FormProjectile, SpellDefs.EffectHarm);
            t.Expect(book.SaveSlot(8, recipe, out string saved), "save slot 8 refused: " + saved);
            comp.SetSlot(8);
            t.Expect(verb.Available(), "the staff stayed unavailable while its bookmark holds a spell");

            book.ClearSlot(8);
            t.Expect(!verb.Available(), "the staff stayed available after its bookmark was cleared");
            t.Expect(!verb.TryStartCastOn(new LocalTargetInfo(target)),
                "the native verb accepted a cast from an empty bookmark");
            t.Expect(!caster.TryStartAttack(new LocalTargetInfo(target)),
                "the native attack order was accepted after the bookmark was cleared");
            t.Step(120);
            t.Expect(!SpellCastingService.IsCasting(caster), "a cast started from an empty bookmark");
        }

        private static ThingWithComps GiveStaff(TestContext t, Pawn caster)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(StaffDefs.ThingDefName);
            if (def == null)
            {
                t.Fail("thing def " + StaffDefs.ThingDefName + " is not loaded");
                return null;
            }

            // Same as the other staff test: equip from the maker. A spawned thing still belongs to
            // the map's ThingOwner, so TryAdd would refuse it and the pawn would stay unarmed.
            var staff = (ThingWithComps)ThingMaker.MakeThing(def);
            caster.equipment.AddEquipment(staff);
            if (caster.equipment.Primary != staff)
            {
                t.Fail("the staff was not equipped");
                return null;
            }

            return staff;
        }
    }
}
