using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Equipment;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// AI stationing handoff: a staff whose chosen spell is shorter than the weapon Def must still
    /// let the native cast-position search pick a cell inside the spell's reach, not the weapon's.
    /// </summary>
    [LabTest]
    public sealed class Staff_AiStandsInSpellReach : LabTest
    {
        public override string Name => "staff.ai-stands-in-spell-reach";

        protected override void Run(TestContext t)
        {
            GameComponent_Spellbook book = GameComponent_Spellbook.Current;
            if (book == null)
            {
                t.Fail("GameComponent_Spellbook is not present in this game");
                return;
            }

            Pawn caster = t.SpawnCaster(1, 5);
            Pawn target = t.SpawnTarget(6, 5);

            SpellRecipe recipe = TestContext.Recipe("staff-touch", SpellDefs.FormTouch, SpellDefs.EffectHarm);
            t.Expect(book.SaveSlot(caster, 7, recipe, out string saved), "save slot 7 refused: " + saved);

            ThingWithComps staff = GiveStaff(t, caster);
            if (staff == null)
            {
                return;
            }

            staff.GetComp<CompSpellStaff>().SetSlot(7);
            Verb verb = caster.equipment.PrimaryEq.PrimaryVerb;
            t.Expect(verb is Verb_CastSpellStaff, "primary verb is " + verb.GetType().Name);
            float spellReach = verb.EffectiveRange;
            t.Measure("verbPropsRange", verb.verbProps.range.ToString("0.##"));
            t.Measure("effectiveRange", spellReach.ToString("0.##"));
            t.Expect(spellReach <= 2f,
                "touch form should have a short reach, got " + spellReach);
            t.Expect(Mathf.Abs(verb.verbProps.range - spellReach) < 0.01f,
                "the verb still reports the weapon Def range instead of the chosen spell's");

            bool found = CastPositionFinder.TryFindCastPosition(new CastPositionRequest
            {
                caster = caster,
                target = target,
                verb = verb,
                maxRangeFromTarget = verb.EffectiveRange,
                wantCoverFromTarget = false
            }, out IntVec3 dest);

            t.Measure("casterAt", caster.Position.ToString());
            t.Measure("targetAt", target.Position.ToString());
            t.Measure("dest", dest.ToString());
            t.Measure("destToTarget", (dest - target.Position).LengthHorizontal.ToString("0.##"));

            t.Expect(found, "the native cast-position search found no cell");
            t.Expect(dest != caster.Position, "the search left the caster outside its spell reach");
            t.Expect((dest - target.Position).LengthHorizontal <= verb.EffectiveRange + 0.01f,
                "the chosen cell is outside the spell reach: " + (dest - target.Position).LengthHorizontal
                + " > " + verb.EffectiveRange);
        }

        private static ThingWithComps GiveStaff(TestContext t, Pawn caster)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(StaffDefs.ThingDefName);
            if (def == null)
            {
                t.Fail("thing def " + StaffDefs.ThingDefName + " is not loaded");
                return null;
            }

            var staff = (ThingWithComps)ThingMaker.MakeThing(def, ThingDefOf.Steel);
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
