using RimWorld;
using Verse;
using WhisperOfTheForest.Experimental.Spells;
using WhisperOfTheForest.Experimental.Testing;

namespace WhisperOfTheForest.Experimental.LabTests
{
    /// <summary>Touch Heal treats an existing, non-permanent injury and nothing else.</summary>
    [LabTest]
    public sealed class Effect_HealTouch : LabTest
    {
        public override string Name => "effect.heal-touch";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(2, 5);
            Pawn patient = t.SpawnTarget(3, 5);
            t.Wound(patient, 4f);

            float before = t.InjurySeverity(patient);
            t.Note("severityBefore", before);
            t.ExpectAtLeast(before, 3.9f, "test wound severity");

            SpellRecipe recipe = TestContext.Recipe("touch-heal", SpellDefs.FormTouch, SpellDefs.EffectHeal);
            if (!t.CastAt(caster, recipe, patient))
            {
                return;
            }

            float after = t.InjurySeverity(patient);
            t.Note("severityAfter", after);
            t.Expect(after < before - 0.5f, "heal did not reduce the wound: " + before + " -> " + after);
            t.Expect(t.InjurySeverity(caster) <= 0f, "a touch heal must not create an injury on the caster");
        }
    }

    /// <summary>An explosion at the impact point damages what stands there.</summary>
    [LabTest]
    public sealed class Effect_Explosion : LabTest
    {
        public override string Name => "effect.explosion";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(2, 5);
            Pawn target = t.SpawnTarget(10, 5);

            SpellRecipe recipe = TestContext.Recipe("dart-explosion", SpellDefs.FormProjectile,
                SpellDefs.EffectExplosion);
            if (!t.CastAt(caster, recipe, target))
            {
                return;
            }

            float before = t.InjurySeverity(target);
            bool hit = t.StepUntil(() => t.InjurySeverity(target) > before, 600);
            t.Note("hitWithin600Ticks", hit);
            t.Note("targetSeverity", t.InjurySeverity(target));
            t.ExpectGreater(t.InjurySeverity(target), before, "target injury severity after the explosion");
        }
    }

    /// <summary>Grow finishes the plant under the touch and leaves a mature plant untouched.</summary>
    [LabTest]
    public sealed class Effect_Grow : LabTest
    {
        public override string Name => "effect.grow";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(2, 5);
            t.SetTerrain(3, 5, TerrainDefOf.Soil);
            Plant plant = t.SpawnPlant(3, 5, 0.2f);
            t.Note("growthBefore", plant.Growth);

            SpellRecipe recipe = TestContext.Recipe("touch-grow", SpellDefs.FormTouch, SpellDefs.EffectGrow);
            if (!t.CastAt(caster, recipe, plant))
            {
                return;
            }

            t.Note("growthAfter", plant.Growth);
            t.Note("mature", plant.LifeStage == PlantLifeStage.Mature);
            t.Expect(plant.Growth >= 0.999f, "grow left the plant at growth " + plant.Growth);
            t.Expect(plant.LifeStage == PlantLifeStage.Mature, "grow did not mature the plant");
        }
    }
}
