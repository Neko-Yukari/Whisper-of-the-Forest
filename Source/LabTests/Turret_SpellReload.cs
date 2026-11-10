using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;
using WhisperOfTheForest.Turrets;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Save/load handoff for a turret's spell: the turret keeps its own copy of the spell it was
    /// given, and the copy, the pending order and the four target filters all survive the reload.
    ///
    /// This test must not advance game time: the runner parks every reloading test's scene and then
    /// writes one shared save, so ticks spent here would let another parked scene's dart land before
    /// that save. The writing route through the work giver is covered by turret.inscription-work.
    /// </summary>
    [LabTest(Reload = true)]
    public sealed class Turret_SpellReload : LabTest
    {
        private int turretId;
        public override string Name => "turret.spell-reload";

        protected override void Run(TestContext t)
        {
            var turret = Turret_TestFixture.Spawn<Building_SpellTurret>(t, "WotF_SpellTurret", 5, 5, ThingDefOf.Steel);
            turretId = turret.thingIDNumber;
            CompSpellTurret comp = turret.GetComp<CompSpellTurret>();
            Pawn elf = t.SpawnElf(3, 5);
            SpellRecipe recipe = TestContext.Recipe("saved-copy", SpellDefs.FormProjectile, SpellDefs.EffectHarm, SpellDefs.EffectHarm);
            var book = GameComponent_Spellbook.Current;
            t.Expect(book.SaveSlot(elf, 0, recipe, out string reason), reason);
            t.Expect(book.SaveSlot(elf, 1, TestContext.Recipe("touch", SpellDefs.FormTouch, SpellDefs.EffectHarm), out reason), reason);
            var candidates = comp.GetInscriptionCandidates().Where(c => c.Owner == elf).ToList();
            t.Expect(candidates.Any(c => c.Recipe.name == "saved-copy" && c.UnavailableReason == null), "projectile missing from candidates");
            t.Expect(candidates.Any(c => c.Recipe.name == "touch" && c.UnavailableReason != null), "non-projectile candidate lacks rejection reason");
            t.Expect(comp.TryRequestInscription(book.GetSlot(elf, 0), out reason), reason);
            t.Expect(book.SaveSlot(elf, 0, TestContext.Recipe("book changed", SpellDefs.FormProjectile, SpellDefs.EffectHarm), out reason), reason);

            // Same completion the writing job performs, without paying its game-time wait.
            comp.CompleteInscription(comp.InscriptionRevision);
            t.Expect(comp.CurrentSpell.name == "saved-copy", "the requested spell was not installed");
            recipe.name = "source changed";
            recipe.glyphs.Clear();
            t.Expect(comp.CurrentSpell.name == "saved-copy", "source changed turret spell");
            t.Expect(comp.TryRequestInscription(TestContext.Recipe("pending-copy", SpellDefs.FormProjectile, SpellDefs.EffectHarm), out reason), reason);

            // Parked scenes share this map until the merged save, so this turret must never pick one
            // of their pawns as a target while they are waiting.
            comp.TargetHumanlikes = false;
            comp.TargetAnimals = false;
            comp.TargetMechanoids = false;
            comp.TargetEntities = false;
            comp.TargetHostile = false;
            comp.TargetNeutral = true;
            comp.TargetFriendly = true;
            t.Expect(!turret.TryFindNewTarget().IsValid, "the parked turret acquired a target");
        }

        protected override void AfterReload(TestContext t)
        {
            var turret = (Building_SpellTurret)t.Arena.Find(turretId);
            t.Expect(turret != null, "turret missing after reload");
            if (turret == null) return;
            CompSpellTurret comp = turret.GetComp<CompSpellTurret>();
            t.Expect(comp.CurrentSpell.name == "saved-copy", "current recipe lost");
            t.Expect(comp.CurrentSpell.glyphs.SequenceEqual(new[] { SpellDefs.FormProjectile, SpellDefs.EffectHarm, SpellDefs.EffectHarm }), "current glyph copy lost");
            t.Expect(comp.PendingSpell?.name == "pending-copy", "pending recipe lost");
            t.Expect(!comp.TargetHumanlikes && !comp.TargetAnimals && !comp.TargetMechanoids && !comp.TargetEntities, "filters lost");
            t.Expect(!comp.TargetHostile && comp.TargetNeutral && comp.TargetFriendly, "relation filters lost");
            t.Measure("relationsAfterLoad", "hostile=" + comp.TargetHostile + ", neutral=" + comp.TargetNeutral + ", friendly=" + comp.TargetFriendly);
            t.Measure("currentAfterLoad", comp.CurrentSpell.ToString());
            t.Measure("pendingAfterLoad", comp.PendingSpell?.ToString());
        }
    }
}
