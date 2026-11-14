using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Race;
using WhisperOfTheForest.Rituals;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// The three life-restoration rites are the same worker with a different ring: a green ring
    /// clears injuries and scars and regrows limbs, while only the star ring restores organs and
    /// removes lasting bad conditions.
    /// </summary>
    [LabTest]
    public sealed class Ritual_HealingRings : LabTest
    {
        public override string Name => "ritual.healing-rings";

        protected override void Run(TestContext t)
        {
            RitualDef minor = DefDatabase<RitualDef>.GetNamedSilentFail("WotF_Ritual_HealingMinor");
            RitualDef medium = DefDatabase<RitualDef>.GetNamedSilentFail("WotF_Ritual_HealingMedium");
            RitualDef grand = DefDatabase<RitualDef>.GetNamedSilentFail("WotF_Ritual_HealingGrand");
            ThingDef altarDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_RitualAltar_Starlight");
            ThingDef nodeDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaNode");
            ThingDef whiteChalk = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ChalkMark_White");
            ThingDef greenChalk = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ChalkMark_Green");
            ThingDef starChalk = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ChalkMark_Star");
            HediffDef addictionDef = DefDatabase<HediffDef>.GetNamedSilentFail("AlcoholAddiction");
            if (minor == null || medium == null || grand == null || altarDef == null || nodeDef == null
                || whiteChalk == null || greenChalk == null || starChalk == null || addictionDef == null)
            {
                t.Fail("healing ritual defs are not loaded");
                return;
            }

            Pawn caster = t.SpawnCaster(4, 4);
            caster.drafter.Drafted = false;
            Pawn patient = t.SpawnTarget(10, 10);
            patient.pather.StopDead();
            Building altar = Spawn<Building>(altarDef, t, 6, 6);
            Building node = Spawn<Building>(nodeDef, t, 10, 6);
            if (altar == null || node == null)
            {
                return;
            }

            CompManaBattery nodeComp = Mana_TestFixture.AddBattery(t, node);
            CompRitualAltar comp = altar.GetComp<CompRitualAltar>();
            if (comp == null || nodeComp == null)
            {
                t.Fail("ritual altar or mana node component is missing");
                return;
            }

            BodyPartRecord arm = Part(patient, "Arm");
            BodyPartRecord hand = Part(patient, "Hand");
            BodyPartRecord kidney = Part(patient, "Kidney");
            if (arm == null || hand == null || kidney == null)
            {
                t.Fail("the patient has no arm, hand or kidney");
                return;
            }

            Hediff_Injury freshCut = AddInjury(patient, arm, 5f, false);
            Hediff_Injury scar = AddInjury(patient, arm, 5f, true);
            Hediff bloodLoss = patient.health.AddHediff(HediffDefOf.BloodLoss);
            bloodLoss.Severity = 0.4f;
            List<Thing> whiteRing = SpawnRing(whiteChalk, t, 6, 6, minor.ringSize);
            nodeComp.SetStored(nodeComp.Capacity);
            t.Expect(comp.TryStart(minor, caster), "the white-ring rite did not start");
            t.Expect(t.StepUntil(() => !comp.IsActive, 2000), "the white-ring rite did not finish");
            t.Expect(!patient.health.hediffSet.hediffs.Contains(freshCut),
                "the white-ring rite left a fresh cut");
            t.Expect(patient.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss) == null,
                "the white-ring rite left blood loss");
            t.Expect(HasPermanentInjury(patient), "the white-ring rite removed a scar");
            DestroyAll(whiteRing);
            AddMissing(patient, hand);
            AddMissing(patient, kidney);
            patient.health.AddHediff(addictionDef);

            List<Thing> greenRing = SpawnRing(greenChalk, t, 6, 6, medium.ringSize);
            nodeComp.SetStored(nodeComp.Capacity);
            t.Expect(comp.TryStart(medium, caster), "the green-ring rite did not start");
            t.Expect(t.StepUntil(() => !comp.IsActive, 3000), "the green-ring rite did not finish");
            t.Expect(patient.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Cut) == null,
                "the green-ring rite left a fresh cut");
            t.Expect(!HasPermanentInjury(patient), "the green-ring rite left a scar");
            t.Expect(patient.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.MissingBodyPart) != null,
                "the green-ring rite should have left the missing kidney for the star ring");
            t.Expect(HasMissingPart(patient, hand) == false, "the green-ring rite did not regrow the hand");
            t.Expect(patient.health.hediffSet.GetFirstHediffOfDef(addictionDef) != null,
                "the green-ring rite removed an addiction it should not touch");

            DestroyAll(greenRing);
            Pawn helper = t.SpawnCaster(9, 6);
            helper.drafter.Drafted = false;
            helper.pather.StopDead();
            SpawnRing(starChalk, t, 6, 6, grand.ringSize);
            Building node2 = Spawn<Building>(nodeDef, t, 10, 7);
            CompManaBattery battery2 = Mana_TestFixture.AddBattery(t, node2);
            battery2.SetStored(battery2.Capacity);
            nodeComp.SetStored(nodeComp.Capacity);
            t.Expect(comp.TryStart(grand, null, caster, new List<Pawn> { helper }), "the star-ring rite did not start");
            t.Expect(t.StepUntil(() => !comp.IsActive, 4000), "the star-ring rite did not finish");
            t.Expect(HasMissingPart(patient, kidney) == false, "the star-ring rite did not restore the kidney");
            t.Expect(patient.health.hediffSet.GetFirstHediffOfDef(addictionDef) == null,
                "the star-ring rite left an addiction");
            t.Measure("addictions", patient.health.hediffSet.hediffs.Count(h => h.def == addictionDef).ToString());
        }

        private static Hediff_Injury AddInjury(Pawn pawn, BodyPartRecord part, float severity, bool permanent)
        {
            var injury = (Hediff_Injury)pawn.health.AddHediff(HediffDefOf.Cut, part);
            injury.Severity = severity;
            if (permanent)
            {
                injury.TryGetComp<HediffComp_GetsPermanent>().IsPermanent = true;
            }

            return injury;
        }

        private static void AddMissing(Pawn pawn, BodyPartRecord part)
        {
            pawn.health.AddHediff(HediffDefOf.MissingBodyPart, part);
        }

        private static bool HasPermanentInjury(Pawn pawn)
        {
            return pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any(h => h.IsPermanent());
        }

        private static bool HasMissingPart(Pawn pawn, BodyPartRecord part)
        {
            return pawn.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().Any(h => h.Part == part);
        }

        private static BodyPartRecord Part(Pawn pawn, string defName)
        {
            return pawn.health.hediffSet.GetNotMissingParts()
                .FirstOrDefault(p => p.def.defName == defName);
        }

        private static List<Thing> SpawnRing(ThingDef chalkDef, TestContext t, int centerX, int centerZ, int size)
        {
            var ring = new List<Thing>();
            int radius = (size - 1) / 2;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius)
                    {
                        continue;
                    }

                    ring.Add(Spawn<Thing>(chalkDef, t, centerX + dx, centerZ + dz));
                }
            }

            return ring;
        }

        private static void DestroyAll(List<Thing> things)
        {
            for (int i = 0; i < things.Count; i++)
            {
                things[i].Destroy(DestroyMode.Vanish);
            }
        }

        private static T Spawn<T>(ThingDef def, TestContext t, int x, int z) where T : Thing
        {
            var thing = (T)ThingMaker.MakeThing(def, def.MadeFromStuff ? ThingDefOf.BlocksGranite : null);
            GenSpawn.Spawn(thing, t.Arena.Cell(x, z), t.Map, WipeMode.Vanish);
            return t.Arena.Track(thing);
        }
    }
}












