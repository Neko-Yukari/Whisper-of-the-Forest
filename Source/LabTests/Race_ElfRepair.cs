using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// The broken-down building handoff: a bloodline pawn's wait toil uses the mod's clumsy
    /// repair effecter, while a pawn without the bloodline keeps the vanilla repair effect.
    /// </summary>
    [LabTest]
    public sealed class Race_ElfRepair : LabTest
    {
        public override string Name => "race.elf-repair";

        protected override void Run(TestContext t)
        {
            Pawn elf = t.SpawnCaster(4, 4);
            Pawn human = t.SpawnTarget(12, 4);

            var building = (Building)ThingMaker.MakeThing(ThingDefOf.WoodFiredGenerator);
            GenSpawn.Spawn(building, t.Arena.Cell(8, 8), t.Map, WipeMode.Vanish);
            building.SetFaction(Faction.OfPlayer);
            t.Arena.Track(building);

            Thing component = ThingMaker.MakeThing(ThingDefOf.ComponentIndustrial);
            GenSpawn.Spawn(component, t.Arena.Cell(11, 8), t.Map, WipeMode.Vanish);
            t.Arena.Track(component);

            EffecterDef elfEffect = WaitEffecter(elf, building, component);
            EffecterDef humanEffect = WaitEffecter(human, building, component);

            t.Measure("elfRepairEffect", elfEffect == null ? "null" : elfEffect.defName);
            t.Measure("humanRepairEffect", humanEffect == null ? "null" : humanEffect.defName);

            t.Expect(elfEffect != null && elfEffect.defName == "WotF_ElfRepair",
                "a bloodline pawn does not get the clumsy repair effect");
            t.Expect(humanEffect == building.def.repairEffect,
                "a pawn without the bloodline lost the vanilla repair effect");
        }

        private static EffecterDef WaitEffecter(Pawn pawn, Building building, Thing component)
        {
            Job job = JobMaker.MakeJob(JobDefOf.FixBrokenDownBuilding, building, component);
            var driver = (JobDriver_FixBrokenDownBuilding)job.MakeDriver(pawn);
            MethodInfo makeNewToils = typeof(JobDriver_FixBrokenDownBuilding)
                .GetMethod("MakeNewToils", BindingFlags.Instance | BindingFlags.NonPublic);
            var toils = (IEnumerable<Toil>)makeNewToils.Invoke(driver, null);
            Toil wait = toils.FirstOrDefault(toil =>
                toil.defaultCompleteMode == ToilCompleteMode.Delay && toil.defaultDuration > 0);
            if (wait?.preTickActions == null || wait.preTickActions.Count == 0)
            {
                return null;
            }

            object closure = wait.preTickActions[0].Target;
            if (closure == null)
            {
                return null;
            }

            foreach (FieldInfo field in closure.GetType().GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (typeof(Func<EffecterDef>).IsAssignableFrom(field.FieldType))
                {
                    var getter = (Func<EffecterDef>)field.GetValue(closure);
                    return getter?.Invoke();
                }
            }

            return null;
        }
    }
}
