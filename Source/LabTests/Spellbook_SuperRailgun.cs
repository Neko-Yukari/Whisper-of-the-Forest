using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    [LabTest]
    public sealed class Spellbook_SuperRailgun : LabTest
    {
        public override string Name => "spellbook.super-railgun";

        protected override void Run(TestContext t)
        {
            using var casting = new ResourceTestUtil.FreeCastingScope();
            t.Expect(TestLogWatch.IsOwn("Could not find type named WhisperOfTheForest.Experimental.Spells.GameComponent_Spellbook"),
                "the load gate exempted a removed class belonging to this mod");

            SpellGlyphDef railgun = TestContext.Glyph(SpellDefs.FormRailgun);
            GameComponent_Spellbook book = GameComponent_Spellbook.Current;
            t.Expect(railgun.kind == SpellGlyphKind.Super, "railgun kind is " + railgun.kind);
            t.Expect(railgun.compatibleAugments != null && railgun.compatibleAugments.Count == 0,
                "railgun must declare an empty compatibleAugments list");
            book.Unlock(railgun);
            var chain = new List<string> { railgun.defName };
            var augments = DefDatabase<SpellGlyphDef>.AllDefsListForReading
                .Where(glyph => glyph.kind == SpellGlyphKind.Augment).ToList();
            t.Expect(augments.Count > 0, "no augment defs were loaded");
            foreach (SpellGlyphDef augment in augments)
            {
                book.Unlock(augment);
                bool accepted = SpellCompiler.CanAppend(chain, augment, out string reason);
                t.Expect(!accepted, "Super accepted " + augment.defName);
                t.Expect(reason == "WotF_Spells.Reason.AugmentNotAccepted".Translate().ToString(),
                    augment.defName + " was not refused by compatibility: " + reason);
            }
            t.Note("augmentsRefused", augments.Count);

            // Prove the list, not a hidden Super-kind ban, controls the arranging boundary.
            List<string> original = railgun.compatibleAugments;
            try
            {
                railgun.compatibleAugments = new List<string> { augments[0].defName };
                t.Expect(SpellCompiler.CanAppend(chain, augments[0], out string reason),
                    "declared compatibility was ignored: " + reason);
            }
            finally
            {
                railgun.compatibleAugments = original;
            }

            SpellRecipe recipe = TestContext.Recipe("super-railgun", railgun.defName);
            SpellCastPlan plan = SpellCompiler.Compile(recipe, out string compileReason);
            t.Expect(plan != null, "single Super rune did not compile: " + compileReason);
            if (plan == null) return;
            t.Expect(plan.form == SpellFormKind.Special && plan.railgun != null,
                "single Super rune did not produce a complete railgun plan");

            Pawn caster = t.SpawnCaster(1, 5);
            caster.apparel.DestroyAll();
            caster.apparel.Wear((Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("WotF_Spellbook_Archmage")));
            caster.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(
                DefDatabase<ThingDef>.GetNamed("WotF_SpellStaff"), ThingDefOf.Steel));
            BloodlineKit.Gene(t, caster).GainExperience(BloodlineKit.XpFromOneTo(BloodlineKit.Def.maxLevel));
            t.Note("casterEntropyLimit", caster.GetStatValue(StatDefOf.PsychicEntropyMax));
            Thing wall = t.SpawnWall(9, 5, ThingDefOf.Plasteel);
            t.Expect(book.SaveSlot(caster, 0, recipe, out string saveReason), "save Super slot: " + saveReason);
            book.SetActiveSlot(caster, 0);
            Command_Target command = t.SpellAbility(caster).GetGizmos().OfType<Command_Target>().Single();
            bool targetAccepted = command.targetingParams.CanTarget(new TargetInfo(wall));
            t.Note("nativeTargetAccepted", targetAccepted);
            t.Expect(!command.Disabled, "Super gizmo disabled: " + command.disabledReason);
            t.Expect(targetAccepted, "native targeter rejected the wall");
            if (command.Disabled || !targetAccepted) return;

            int before = wall.HitPoints;
            command.action(new LocalTargetInfo(wall));
            t.Expect(t.StepUntil(() => wall.Destroyed || wall.HitPoints < before, 600),
                "Super cast did not damage wall: hp=" + wall.HitPoints + "/" + before);
            t.Note("wallHpBefore", before);
            t.Note("wallHpAfter", wall.Destroyed ? 0 : wall.HitPoints);
            t.Expect(t.StepUntil(() => !SpellCastingService.IsCasting(caster), 600),
                "Super cast did not finish recovery");
        }
    }
}
