using System;
using RimWorld;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;
using WhisperOfTheForest.Turrets;

namespace WhisperOfTheForest.LabTests
{
    [LabTest]
    public sealed class Turret_Inscription : LabTest
    {
        public override string Name => "turret.inscription-work";

        protected override void Run(TestContext t)
        {
            var turret = Turret_TestFixture.Spawn<Building_SpellTurret>(t, "WotF_SpellTurret", 5, 5, ThingDefOf.Steel);
            var comp = turret.GetComp<CompSpellTurret>();
            var node = Mana_TestFixture.AddBattery(t, Turret_TestFixture.Spawn<Building>(t, "WotF_ManaNode", 4, 5, ThingDefOf.BlocksGranite));
            Pawn elf = t.SpawnElf(1, 5);
            Pawn target = t.SpawnTarget(10, 5);
            Turret_TestFixture.Inscribe(t, elf, turret, TestContext.Recipe("old-dart", SpellDefs.FormProjectile, SpellDefs.EffectHarm));
            string oldName = comp.CurrentSpell.name;
            var recipe = TestContext.Recipe("new-dart", SpellDefs.FormProjectile, SpellDefs.EffectHarm, SpellDefs.EffectHarm);
            t.Expect(comp.TryRequestInscription(recipe, out string reason), "request refused: " + reason);
            recipe.name = "mutated";
            recipe.glyphs.Clear();
            t.Expect(comp.PendingSpell.name == "new-dart", "pending recipe aliases caller");
            t.Expect(comp.CurrentSpell.name == oldName, "request replaced current spell immediately");
            var invalid = TestContext.Recipe("invalid", SpellDefs.EffectHarm);
            SpellCompiler.Compile(invalid, out string compilerReason);
            t.Expect(!comp.TryRequestInscription(invalid, out reason) && reason == compilerReason, "compiler reason was not preserved");
            var touch = TestContext.Recipe("touch", SpellDefs.FormTouch, SpellDefs.EffectHarm);
            t.Expect(!comp.TryRequestInscription(touch, out reason), "non-projectile accepted");
            t.Expect(reason == "WotF_Turret_ProjectileOnly".Translate().ToString(), "non-projectile reason missing");
            t.Expect(comp.PendingSpell.name == "new-dart", "rejected request changed pending spell");
            t.Step(1);
            node.SetStored(100f);
            float before = node.Network.Stored;
            float oldCost = comp.ManaPerShot;
            comp.TargetFriendly = true;
            t.Expect(turret.AttackVerb.TryStartCastOn(target), "old spell refused while pending");
            comp.TargetFriendly = false;
            t.Note("pendingShotCost", before - node.Network.Stored);
            t.Expect(Math.Abs(before - node.Network.Stored - oldCost) < 0.001f, "pending shot did not charge old spell cost");

            var giver = (WorkGiver_Scanner)DefDatabase<WorkGiverDef>.GetNamed("WotF_WriteTurretSpell").Worker;
            t.Expect(!giver.HasJobOnThing(target, turret), "non-caster accepted writing work");
            t.Expect(giver.HasJobOnThing(elf, turret), "caster did not get writing work");
            Job job = giver.JobOnThing(elf, turret);
            elf.jobs.TryTakeOrderedJob(job);
            t.Measure("orderedJob", elf.CurJobDef?.defName ?? "none");
            t.Expect(elf.CurJobDef == comp.Props.inscriptionJob, "WorkGiver job did not start");
            t.Step(comp.Props.inscriptionTicks / 2);
            t.Expect(comp.HasPendingSpell && comp.CurrentSpell.name == oldName, "writing completed early");
            t.Expect(t.StepUntil(() => !comp.HasPendingSpell, comp.Props.inscriptionTicks * 3), "writing never completed; job=" + elf.CurJobDef?.defName);
            t.Measure("writtenSpell", comp.CurrentSpell.name);
            t.Expect(comp.CurrentSpell.name == "new-dart", "wrong spell after writing");
            t.Expect(Math.Abs(before - oldCost - node.Network.Stored) < 0.001f, "writing consumed network mana");
            before = node.Network.Stored;
            float expected = SpellCost.Derive(comp.CurrentSpell.glyphs).mana * comp.Props.manaCostFactor;
            comp.TargetFriendly = true;
            t.Expect(turret.AttackVerb.TryStartCastOn(target), "new spell refused");
            comp.TargetFriendly = false;
            t.Note("writtenShotCost", before - node.Network.Stored);
            t.Expect(Math.Abs(before - node.Network.Stored - expected) < 0.001f, "new spell charged wrong mana");
        }
    }
}
