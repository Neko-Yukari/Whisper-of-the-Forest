using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;
using WhisperOfTheForest.Turrets;

namespace WhisperOfTheForest.LabTests
{
    [LabTest]
    public sealed class Turret_FrozenTargets : LabTest
    {
        public override string Name => "turret.frozen-targets";

        protected override void Run(TestContext t)
        {
            var turret = Turret_TestFixture.Spawn<Building_SpellTurret>(t, "WotF_SpellTurret", 5, 5, ThingDefOf.Steel);
            CompSpellTurret comp = turret.GetComp<CompSpellTurret>();
            Pawn writer = t.SpawnElf(3, 5);
            var book = GameComponent_Spellbook.Current;
            var draft = TestContext.Recipe("frozen-copy", SpellDefs.FormProjectile, SpellDefs.EffectHarm);
            t.Expect(book.SaveSlot(writer, 0, draft, out string reason), reason);
            SpellRecipe frozen = book.GetSlot(writer, 0);
            // A previously frozen recipe may differ from today's catalogue. No Def is mutated.
            frozen.targets = new List<SpellTargetClass> { SpellTargetClass.Mechanoid };
            t.Expect(!SpellCompiler.DeriveTargets(frozen).SequenceEqual(frozen.targets),
                "fixture did not distinguish the frozen whitelist from current Defs");
            t.Expect(comp.TryRequestInscription(frozen, out reason), reason);
            t.Measure("source", string.Join(",", frozen.targets));
            t.Measure("pending", string.Join(",", comp.PendingSpell.targets));
            t.Expect(comp.PendingSpell.targets.SequenceEqual(frozen.targets), "inscription overwrote the frozen whitelist");
            frozen.targets.Add(SpellTargetClass.Plant);
            Turret_TestFixture.StartWriting(t, writer, turret);
            t.Expect(t.StepUntil(() => !comp.HasPendingSpell, comp.Props.inscriptionTicks * 3), "writing did not finish");
            t.Measure("current", string.Join(",", comp.CurrentSpell.targets));
            t.Expect(comp.CurrentSpell.targets.SequenceEqual(new[] { SpellTargetClass.Mechanoid }),
                "written spell lost the frozen copy or retained the caller's mutable list");

            t.Expect(comp.TryRequestInscription(draft, out reason), reason);
            t.Measure("newDraft", string.Join(",", comp.PendingSpell.targets));
            t.Expect(comp.PendingSpell.targets.SequenceEqual(SpellCompiler.DeriveTargets(draft)),
                "newly composed spell did not generate its whitelist");
        }
    }
}
