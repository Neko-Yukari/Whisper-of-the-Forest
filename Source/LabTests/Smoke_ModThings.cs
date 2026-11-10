using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Smoke sweep over every building and item this mod defines: the build designator's placement
    /// check, spawning, a rare tick and the gizmo list. Nothing is asserted about behaviour; any
    /// exception or logged error fails the run, which is what catches a broken comp or place worker
    /// without a dedicated test.
    /// </summary>
    [LabTest]
    public sealed class Smoke_ModThings : LabTest
    {
        public override string Name => "smoke.mod-things";

        protected override void Run(TestContext t)
        {
            ModContentPack mod = DefDatabase<ThingDef>.GetNamed("WotF_ManaNode").modContentPack;
            var defs = new List<ThingDef>();
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.modContentPack == mod
                    && (def.category == ThingCategory.Building || def.category == ThingCategory.Item)
                    && !def.IsBlueprint && !def.IsFrame && !def.IsCorpse)
                {
                    defs.Add(def);
                }
            }

            foreach (ThingDef def in defs)
            {
                if (def.BuildableByPlayer)
                {
                    AcceptanceReport report = t.CanPlace(def, 2, 2, Stuff(def));
                    t.Expect(report.Accepted, def.defName + " cannot be placed: " + report.Reason);
                }
            }

            var spawned = new List<Thing>();
            int x = 1;
            int z = 6;
            foreach (ThingDef def in defs)
            {
                if (x + def.size.x > 45)
                {
                    x = 1;
                    z -= 4;
                }

                Thing thing = ThingMaker.MakeThing(def, Stuff(def));
                GenSpawn.Spawn(thing, t.Arena.Cell(x + def.size.x / 2, z), t.Map, WipeMode.Vanish);
                spawned.Add(t.Arena.Track(thing));
                x += def.size.x + 1;
            }

            t.Note("things", spawned.Count);
            t.Step(GenTicks.TickRareInterval);
            foreach (Thing thing in spawned)
            {
                if (thing.Spawned)
                {
                    foreach (Gizmo gizmo in thing.GetGizmos())
                    {
                    }
                }
            }
        }

        private static ThingDef Stuff(ThingDef def)
        {
            return def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
        }
    }
}
