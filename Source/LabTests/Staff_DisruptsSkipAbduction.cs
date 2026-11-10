using RimWorld;
using Verse;
using Verse.AI.Group;
using WhisperOfTheForest.Equipment;
using WhisperOfTheForest.Rituals;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Short staff handoff: a strike on a skip-abduction participant cancels the ritual and
    /// stuns everyone assigned to it. The staff refuses any target that is not in a ritual.
    /// </summary>
    [LabTest]
    public sealed class Staff_DisruptsSkipAbduction : LabTest
    {
        public override string Name => "staff.disrupts-skip-abduction";

        protected override void Run(TestContext t)
        {
            if (!TryStart(t, out PsychicRitual ritual, out Pawn invoker, out Lord lord))
            {
                return;
            }

            Strike(t, ritual, invoker);
            lord.lordManager.RemoveLord(lord);
        }

        private static bool TryStart(TestContext t, out PsychicRitual ritual, out Pawn invoker, out Lord lord)
        {
            ritual = null;
            invoker = null;
            lord = null;
            PsychicRitualDef def = DefDatabase<PsychicRitualDef>.GetNamedSilentFail("SkipAbduction");
            if (!(def is PsychicRitualDef_InvocationCircle circle))
            {
                t.Fail("SkipAbduction is not an invocation circle");
                return false;
            }

            WarmRoom(t);
            invoker = t.SpawnTarget(5, 5);
            TargetInfo target = new TargetInfo(t.Arena.Cell(5, 5), t.Map);
            PsychicRitualRoleAssignments assignments = circle.BuildRoleAssignments(target);
            if (!assignments.TryAssign(invoker, circle.InvokerRole, out PsychicRitualRoleDef.Reason reason))
            {
                t.Fail("could not assign the invoker: " + reason.ToPlayerReadable());
                return false;
            }

            lord = circle.MakeNewLord(assignments);
            if (!(lord.CurLordToil is LordToil_PsychicRitual toil))
            {
                t.Fail("the ritual lord has no psychic ritual toil");
                return false;
            }

            ritual = toil.RitualData.psychicRitual;
            t.Expect(ritual != null && !ritual.canceled, "the skip abduction did not start");
            return ritual != null && !ritual.canceled;
        }

        private static void WarmRoom(TestContext t)
        {
            for (int x = 3; x <= 7; x++)
            {
                t.SpawnWall(x, 3);
                t.SpawnWall(x, 7);
            }

            for (int z = 4; z <= 6; z++)
            {
                t.SpawnWall(3, z);
                t.SpawnWall(7, z);
            }

            for (int x = 4; x <= 6; x++)
            {
                for (int z = 4; z <= 6; z++)
                {
                    t.Map.roofGrid.SetRoof(t.Arena.Cell(x, z), RoofDefOf.RoofConstructed);
                }
            }

            Room room = t.Arena.Cell(5, 5).GetRoom(t.Map);
            if (room != null)
            {
                room.Temperature = 21f;
            }
        }
        private static void Strike(TestContext t, PsychicRitual ritual, Pawn invoker)
        {
            Pawn caster = t.SpawnCaster(4, 5);
            ThingWithComps staff = GiveShortStaff(t, caster);
            if (staff == null)
            {
                return;
            }

            Verb verb = caster.equipment.PrimaryEq.PrimaryVerb;
            t.Expect(verb is Verb_DisruptPsychicRitual, "primary verb is " + verb.GetType().Name);
            t.Expect(verb.CanHitTarget(new LocalTargetInfo(invoker)), "the staff refuses a ritual participant");
            t.Expect(verb.TryStartCastOn(new LocalTargetInfo(invoker)), "the short staff refused the strike");

            t.Expect(t.StepUntil(() => ritual.canceled, 120), "the ritual was not canceled");
            t.Expect(invoker.stances.stunner.StunTicksLeft > 0, "the invoker was not stunned");
            t.Measure("invokerStunTicks", invoker.stances.stunner.StunTicksLeft.ToString());
        }

        private static ThingWithComps GiveShortStaff(TestContext t, Pawn caster)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(StaffDefs.ShortThingDefName);
            if (def == null)
            {
                t.Fail("thing def " + StaffDefs.ShortThingDefName + " is not loaded");
                return null;
            }

            var staff = (ThingWithComps)ThingMaker.MakeThing(def);
            caster.equipment.AddEquipment(staff);
            if (caster.equipment.Primary != staff)
            {
                t.Fail("the short staff was not equipped");
                return null;
            }

            return staff;
        }
    }
}