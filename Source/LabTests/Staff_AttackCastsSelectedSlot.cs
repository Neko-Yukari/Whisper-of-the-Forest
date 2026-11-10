using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Equipment;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Staff attack handoff: the bookmark chosen on the staff's comp is what the weapon's own verb
    /// casts. Ordering the native attack starts the shared casting service, and the cast reaches the
    /// target through the ordinary payload path.
    /// </summary>
    [LabTest]
    public sealed class Staff_AttackCastsSelectedSlot : LabTest
    {
        public override string Name => "staff.attack-casts-selected-slot";

        protected override void Run(TestContext t)
        {
            GameComponent_Spellbook book = GameComponent_Spellbook.Current;
            if (book == null)
            {
                t.Fail("GameComponent_Spellbook is not present in this game");
                return;
            }

            Pawn caster = t.SpawnCaster(1, 5);
            Pawn target = t.SpawnTarget(9, 5);

            SpellRecipe recipe = TestContext.Recipe("staff-dart", SpellDefs.FormProjectile, SpellDefs.EffectHarm);
            t.Expect(book.SaveSlot(caster, 6, recipe, out string saved), "save slot 6 refused: " + saved);

            ThingWithComps staff = GiveStaff(t, caster);
            if (staff == null)
            {
                return;
            }

            CompSpellStaff comp = staff.GetComp<CompSpellStaff>();
            comp.SetSlot(6);
            t.Expect(comp.Slot == 6, "staff slot should be 6, is " + comp.Slot);

            // The equipped weapon's gizmos are what Pawn_EquipmentTracker asks this comp for, so
            // checking the enumerable proves the slot selection is reachable on the pawn.
            var gizmos = new List<Gizmo>(staff.GetComp<CompEquippable>().CompGetEquippedGizmosExtra());
            t.Expect(gizmos.Count == 1, "expected one staff slot gizmo, got " + gizmos.Count);
            if (gizmos.Count == 1 && gizmos[0] is Command command)
            {
                t.Expect(command.defaultLabel != null && command.defaultLabel.Contains(recipe.name),
                    "staff gizmo label does not name the selected spell: " + command.defaultLabel);
            }

            Verb verb = caster.equipment.PrimaryEq.PrimaryVerb;
            t.Expect(verb is Verb_CastSpellStaff, "primary verb is " + verb.GetType().Name);
            t.Expect(verb.Available(), "staff with a saved bookmark reports unavailable");
            t.ExpectAtLeast(verb.EffectiveRange, 8f, "staff reach for an 8-cell target");
            t.Expect(verb.CanHitTarget(new LocalTargetInfo(target)), "staff refuses a pawn 8 cells away");

            // Two observables, because vanilla records harm in two different ways: an ordinary hit leaves
            // a Hediff_Injury, while damage that exceeds a small part's hit points destroys the part and
            // leaves only a Hediff_MissingPart (a toe has ~10 hp, so a single staff hit does this about
            // one run in ten). SummaryHealthPercent covers both, the severity sum covers only the first.
            float beforeSeverity = t.InjurySeverity(target);
            float beforeHealth = target.health.summaryHealth.SummaryHealthPercent;
            // The same call Pawn.TryStartAttack makes; going through the verb directly keeps the
            // test independent of the random colonist backstory's Violent work-tag gate.
            bool ordered = verb.TryStartCastOn(new LocalTargetInfo(target));
            t.Expect(ordered, "the native attack order was refused");
            t.Expect(SpellCastingService.IsCasting(caster), "the staff attack did not start a cast");

            bool hurt = t.StepUntil(
                () => t.InjurySeverity(target) > beforeSeverity
                      || target.health.summaryHealth.SummaryHealthPercent < beforeHealth,
                400);
            t.Note("targetSeverity", t.InjurySeverity(target));
            t.Note("targetHealth", target.health.summaryHealth.SummaryHealthPercent);
            t.Measure("targetWounds", DescribeWounds(target));
            t.Expect(hurt, "the target took no harm from the staff cast (health "
                           + beforeHealth.ToString("0.###") + " -> "
                           + target.health.summaryHealth.SummaryHealthPercent.ToString("0.###")
                           + ", wounds: " + DescribeWounds(target) + ")");

            // The native chain parks a Stance_Cooldown on the caster that references this staff's
            // verb. The arena removes the pawn into the world-pawn set, which does not tick, so a
            // test that returned mid-cooldown would save a reference to a weapon this test destroys
            // ("load ID ... is not deep-saved" on every later save). Let the weapon's own cooldown
            // run out, and prove it did.
            int cooldownTicks = verb.verbProps.AdjustedCooldownTicks(verb, caster);
            t.Step(cooldownTicks + 5);
            t.Expect(!caster.stances.FullBodyBusy, "a busy stance outlived the staff cooldown");
        }

        private static string DescribeWounds(Pawn pawn)
        {
            var wounds = new List<string>();
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff is Hediff_Injury injury)
                {
                    wounds.Add(injury.def.defName + "@" + (injury.Part?.def.defName ?? "?")
                              + "=" + injury.Severity.ToString("0.##")
                              + (HediffUtility.IsPermanent(injury) ? "(permanent)" : string.Empty));
                }
                else if (hediff is Hediff_MissingPart missing)
                {
                    wounds.Add("missing@" + (missing.Part?.def.defName ?? "?"));
                }
            }

            return wounds.Count == 0 ? "none" : string.Join(" ", wounds.ToArray());
        }

        private static ThingWithComps GiveStaff(TestContext t, Pawn caster)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(StaffDefs.ThingDefName);
            if (def == null)
            {
                t.Fail("thing def " + StaffDefs.ThingDefName + " is not loaded");
                return null;
            }

            // A weapon is equipped straight from the maker, never from the map: a spawned thing
            // already belongs to the map's ThingOwner and Pawn.equipment.TryAdd refuses it
            // ("already in another container"), which leaves the pawn unarmed. Same path as
            // PawnGenerator. The pawn is what the arena cleans up, and it destroys its equipment.
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
