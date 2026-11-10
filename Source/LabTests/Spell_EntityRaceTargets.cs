using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;
using WhisperOfTheForest.Turrets;

namespace WhisperOfTheForest.LabTests
{
    [LabTest]
    public sealed class Spell_EntityRaceTargets : LabTest
    {
        public override string Name => "spell.entity-race-targets";

        protected override void Run(TestContext t)
        {
            var turret = Turret_TestFixture.Spawn<Building_SpellTurret>(t, "WotF_SpellTurret", 5, 5, ThingDefOf.Steel);
            CompSpellTurret comp = turret.GetComp<CompSpellTurret>();
            Pawn writer = t.SpawnElf(3, 5);
            Turret_TestFixture.Inscribe(t, writer, turret,
                TestContext.Recipe("entity-filter", SpellDefs.FormProjectile, SpellDefs.EffectHarm));
            comp.TargetHostile = comp.TargetNeutral = comp.TargetFriendly = true;
            comp.TargetHumanlikes = comp.TargetAnimals = comp.TargetMechanoids = false;
            comp.TargetEntities = true;

            Pawn caster = t.SpawnCaster(6, 5);
            SpellRecipe entityOnly = TestContext.Recipe("frozen-entity-filter", SpellDefs.FormProjectile, SpellDefs.EffectHarm);
            entityOnly.targets = new List<SpellTargetClass> { SpellTargetClass.Entity };
            TargetingParameters targeting = SpellService.BuildTargetingParameters(caster, SpellCompiler.Compile(entityOnly, out _));
            Pawn entity = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                DefDatabase<PawnKindDef>.GetNamed("Fingerspike"), Faction.OfEntities, canGeneratePawnRelations: false));
            GenSpawn.Spawn(entity, t.Arena.Cell(10, 5), t.Map);
            t.Arena.Track(entity);
            t.Measure("race", entity.def.defName + " flesh=" + entity.RaceProps.FleshType.defName
                + " anomaly=" + entity.RaceProps.IsAnomalyEntity);
            Check(t, entity, comp, targeting, "entities");
            entity.SetFaction(Faction.OfPlayer);
            Check(t, entity, comp, targeting, "player");
            entity.SetFaction(null);
            Check(t, entity, comp, targeting, "none");
        }

        private static void Check(TestContext t, Pawn entity, CompSpellTurret comp, TargetingParameters targeting, string stage)
        {
            SpellTargetClass category = SpellTargeting.ClassOf(entity);
            bool nativeAccepted = targeting.CanTarget(new TargetInfo(entity));
            bool turretAccepted = comp.AllowsTarget(entity);
            t.Measure(stage, "category=" + category + " vanillaEntity=" + entity.IsEntity
                + " native=" + nativeAccepted + " turret=" + turretAccepted);
            t.Expect(category == SpellTargetClass.Entity, stage + ": changing faction changed the entity's race category");
            t.Expect(nativeAccepted, stage + ": native entity-only targeting refused the entity race");
            t.Expect(turretAccepted, stage + ": entity-only turret refused the entity race");
            comp.TargetEntities = false;
            t.Expect(!comp.AllowsTarget(entity), stage + ": disabled entity switch still allowed the entity race");
            comp.TargetEntities = true;
        }
    }
}
