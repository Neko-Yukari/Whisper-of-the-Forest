using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// One handoff check for the frozen target whitelist: it survives a save/load, and the native
    /// targeting parameters accept and refuse by category. The reload harness clears every
    /// destroyable non-pawn thing inside the arena before the second half, so the rock case is
    /// checked before the save; the pawn and bare-cell cases run in both halves.
    /// </summary>
    [LabTest(Reload = true)]
    public sealed class Spell_TargetWhitelist : LabTest
    {
        private int casterId;
        private int bystanderId;
        private int mechId;

        public override string Name => "spell.target-whitelist";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(4, 5);
            Pawn bystander = t.SpawnCaster(5, 5);
            Pawn mech = MechHack_TestFixture.SpawnMech(t, 8, 5);
            ThingDef rockDef = DefDatabase<ThingDef>.AllDefs.First(d =>
                d.category == ThingCategory.Building && d.building != null && d.building.isNaturalRock);
            Thing rock = t.Arena.Track(GenSpawn.Spawn(rockDef, t.Arena.Cell(10, 5), t.Map));
            casterId = caster.thingIDNumber;
            bystanderId = bystander.thingIDNumber;
            mechId = mech.thingIDNumber;

            // Freezing only, no ticks: reload scenes share one save.
            var book = GameComponent_Spellbook.Current;
            book.Unlock(TestContext.Glyph("WotF_Effect_Hack"));
            string reason;
            t.Expect(book.SaveSlot(caster, 0,
                TestContext.Recipe("hack-whitelist", SpellDefs.FormProjectile, "WotF_Effect_Hack"), out reason), reason);
            t.Expect(book.SaveSlot(caster, 1,
                TestContext.Recipe("heal-whitelist", SpellDefs.FormProjectile, SpellDefs.EffectHeal), out reason), reason);

            SpellRecipe hack = book.GetSlot(caster, 0);
            SpellRecipe heal = book.GetSlot(caster, 1);
            t.Expect(hack.targets.Contains(SpellTargetClass.Mechanoid), "hack recipe did not freeze the mechanoid class");
            t.Expect(!heal.targets.Contains(SpellTargetClass.Rock) && !heal.targets.Contains(SpellTargetClass.Cell),
                "heal recipe froze a rock or cell class");

            AssertTargeting(t, caster, mech, bystander, "before save");

            SpellCastPlan healPlan = SpellCompiler.Compile(heal, out string fail);
            t.Expect(healPlan != null, "heal no longer compiles: " + fail);
            if (healPlan != null)
            {
                t.Expect(!SpellService.BuildTargetingParameters(caster, healPlan).CanTarget(new TargetInfo(rock)),
                    "heal targeter accepted a natural rock");
            }
        }

        protected override void AfterReload(TestContext t)
        {
            Pawn caster = t.Arena.Track((Pawn)t.Arena.Find(casterId));
            Pawn bystander = t.Arena.Track((Pawn)t.Arena.Find(bystanderId));
            Pawn mech = t.Arena.Track((Pawn)t.Arena.Find(mechId));
            t.Expect(caster != null && bystander != null && mech != null, "a pawn did not survive the reload");

            var book = GameComponent_Spellbook.Current;
            SpellRecipe hack = book.GetSlot(caster, 0);
            SpellRecipe heal = book.GetSlot(caster, 1);
            t.Expect(hack != null && hack.targets.Contains(SpellTargetClass.Mechanoid),
                "saved hack whitelist lost the mechanoid class");
            t.Expect(heal != null
                     && !heal.targets.Contains(SpellTargetClass.Rock)
                     && !heal.targets.Contains(SpellTargetClass.Cell),
                "saved heal whitelist gained a rock or cell class");
            t.Measure("hackAfterLoad", hack == null ? "none" : string.Join(",", hack.targets.Select(c => c.ToString())));
            t.Measure("healAfterLoad", heal == null ? "none" : string.Join(",", heal.targets.Select(c => c.ToString())));

            AssertTargeting(t, caster, mech, bystander, "after load");
        }

        private static void AssertTargeting(TestContext t, Pawn caster, Pawn mech, Pawn bystander, string stage)
        {
            var book = GameComponent_Spellbook.Current;

            SpellCastPlan hackPlan = SpellCompiler.Compile(book.GetSlot(caster, 0), out string hackFail);
            t.Expect(hackPlan != null, stage + ": hack no longer compiles: " + hackFail);
            if (hackPlan != null)
            {
                TargetingParameters hackParams = SpellService.BuildTargetingParameters(caster, hackPlan);
                t.Expect(hackParams.CanTarget(new TargetInfo(mech)),
                    stage + ": hack targeter refused the hostile mechanoid");
                t.Expect(!hackParams.CanTarget(new TargetInfo(bystander)),
                    stage + ": hack targeter accepted a humanlike");
            }

            SpellCastPlan healPlan = SpellCompiler.Compile(book.GetSlot(caster, 1), out string healFail);
            t.Expect(healPlan != null, stage + ": heal no longer compiles: " + healFail);
            if (healPlan != null)
            {
                TargetingParameters healParams = SpellService.BuildTargetingParameters(caster, healPlan);
                t.Expect(healParams.CanTarget(new TargetInfo(bystander)),
                    stage + ": heal targeter refused a humanlike");
                t.Expect(!healParams.CanTarget(new TargetInfo(bystander.Position, bystander.Map)),
                    stage + ": heal targeter accepted a bare cell");
            }
        }
    }
}
