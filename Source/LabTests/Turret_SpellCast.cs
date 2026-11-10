using System;
using RimWorld;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;
using WhisperOfTheForest.Turrets;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Turret cast handoff: the turret's own verb pays the nearest mana network and launches the
    /// frozen projectile plan, and a turret outside every network reports itself unavailable.
    /// </summary>
    [LabTest]
    public sealed class Turret_SpellCast : LabTest
    {
        public override string Name => "turret.spell-cast";

        protected override void Run(TestContext t)
        {
            ThingDef nodeDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaNode");
            ThingDef turretDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_SpellTurret");
            if (nodeDef == null || turretDef == null)
            {
                t.Fail("mana node or spell turret def is not loaded");
                return;
            }

            // The build designator runs every place worker each frame; a throw there spams UIRootUpdate.
            AcceptanceReport placement = t.CanPlace(turretDef, 14, 14, ThingDefOf.Steel);
            t.Expect(placement.Accepted, "turret placement was refused: " + placement.Reason);

            Building node = Spawn<Building>(nodeDef, t, 4, 5);
            Building_SpellTurret turret = Spawn<Building_SpellTurret>(turretDef, t, 5, 5, ThingDefOf.Steel);
            Pawn target = t.SpawnTarget(9, 5);
            t.Step(1);

            CompManaBattery nodeComp = Mana_TestFixture.AddBattery(t, node);
            ManaNetwork network = nodeComp?.Network;
            if (network == null)
            {
                t.Fail("the mana node has no network after spawning");
                return;
            }

            nodeComp.SetStored(100f);
            Verb verb = turret.AttackVerb;
            t.Expect(verb is Verb_CastTurretSpell, "turret attack verb is " + verb.GetType().Name);
            t.Expect(!verb.Available(), "new turret with mana must remain unavailable without a spell");
            t.Expect(!turret.TryFindNewTarget().IsValid, "empty turret selected a target");
            t.Expect(turret.GetInspectString().Contains("WotF_Turret_NoSpell".Translate().ToString()), "empty state missing from inspect string");
            t.Expect(network.Stored == 100f, "empty turret spent mana");
            Pawn writer = t.SpawnElf(3, 5);
            Turret_TestFixture.Inscribe(t, writer, turret,
                TestContext.Recipe("dart", SpellDefs.FormProjectile, SpellDefs.EffectHarm));
            writer.drafter.Drafted = true;
            t.Expect(verb.Available(), "inscribed turret with mana reports unavailable");

            float beforeSeverity = t.InjurySeverity(target);
            float beforeHealth = target.health.summaryHealth.SummaryHealthPercent;
            turret.GetComp<CompSpellTurret>().TargetFriendly = true;
            bool ordered = verb.TryStartCastOn(new LocalTargetInfo(target));
            turret.GetComp<CompSpellTurret>().TargetFriendly = false;
            t.Expect(ordered, "the turret cast was refused");
            t.Measure("manaAfterShot", network.Stored.ToString("0.##"));
            CompSpellTurret spellTurret = turret.GetComp<CompSpellTurret>();
            float expected = SpellCost.Derive(spellTurret.CurrentSpell.glyphs).mana * spellTurret.Props.manaCostFactor;
            t.Note("expectedManaSpent", expected);
            t.Expect(Math.Abs(100f - network.Stored - expected) < 0.001f,
                "shot cost differs from caster derivation: spent=" + (100f - network.Stored) + " expected=" + expected);

            bool hurt = t.StepUntil(
                () => t.InjurySeverity(target) > beforeSeverity
                      || target.health.summaryHealth.SummaryHealthPercent < beforeHealth,
                400);
            t.Expect(hurt, "the turret dart did not harm its target");
            var dry = Spawn<Building_SpellTurret>(turretDef, t, 20, 20, ThingDefOf.Steel);
            writer.drafter.Drafted = false;
            Turret_TestFixture.Inscribe(t, writer, dry,
                TestContext.Recipe("dry-dart", SpellDefs.FormProjectile, SpellDefs.EffectHarm));
            t.Expect(!dry.AttackVerb.Available(), "an inscribed turret outside every mana network reports available");
        }

        private static T Spawn<T>(ThingDef def, TestContext t, int x, int z, ThingDef stuff = null) where T : Thing
        {
            var thing = (T)ThingMaker.MakeThing(def, stuff);
            thing.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(thing, t.Arena.Cell(x, z), t.Map, WipeMode.Vanish);
            return t.Arena.Track(thing);
        }
    }
}
