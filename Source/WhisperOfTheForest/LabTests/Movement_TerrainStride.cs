using RimWorld;
using Verse;
using WhisperOfTheForest.Experimental.Movement;
using WhisperOfTheForest.Experimental.Testing;

namespace WhisperOfTheForest.Experimental.LabTests
{
    /// <summary>
    /// Apparel to pathing: with the terrain mantle worn, mud costs no more than plain ground. The
    /// test first proves the same pawn really walks mud slower without it, so a flat result cannot
    /// come from a lane that never had a cost in the first place. Both lanes are walled, otherwise
    /// the pawn would simply path around the mud and the two measurements would be identical by
    /// accident.
    /// </summary>
    [LabTest]
    public sealed class Movement_TerrainStride : LabTest
    {
        public override string Name => "movement.terrain-stride";

        private const int From = 1;
        private const int To = 8;
        private const int ConcreteZ = 3;
        private const int MudZ = 7;

        protected override void Run(TestContext t)
        {
            Pawn walker = t.SpawnTarget(From, ConcreteZ);
            for (int x = From; x <= To; x++)
            {
                t.SetTerrain(x, MudZ, TerrainDefOf.Mud);
            }

            for (int x = From; x <= To; x++)
            {
                t.SpawnWall(x, ConcreteZ - 1, ThingDefOf.Steel);
                t.SpawnWall(x, ConcreteZ + 1, ThingDefOf.Steel);
                t.SpawnWall(x, MudZ - 1, ThingDefOf.Steel);
                t.SpawnWall(x, MudZ + 1, ThingDefOf.Steel);
            }

            int plainTicks = Lap(t, walker, ConcreteZ);
            int mudTicks = Lap(t, walker, MudZ);

            t.Expect(plainTicks > 0 && mudTicks > 0, "a lap did not finish: plain=" + plainTicks + " mud=" + mudTicks);
            if (plainTicks <= 0 || mudTicks <= 0)
            {
                return;
            }

            t.Expect(mudTicks > plainTicks,
                "the mud lane is not slower than concrete for a bare pawn: plain=" + plainTicks + " mud=" + mudTicks);

            if (!LabActions.TryEquipTerrainStride(walker))
            {
                t.Fail("could not equip TerrainStride: " + LabActions.LastError);
                return;
            }

            t.Expect(TestContext.Wears(walker, StrideApparel.TerrainStrideDefName),
                "TerrainStride is not worn after equipping");
            int stridedTicks = Lap(t, walker, MudZ);

            t.Note("concreteTicks", plainTicks);
            t.Note("mudTicksBare", mudTicks);
            t.Note("mudTicksStride", stridedTicks);
            t.Expect(stridedTicks > 0, "the strided mud lap did not finish");
            t.Expect(stridedTicks <= plainTicks + 2,
                "wearing TerrainStride did not remove the mud cost: concrete=" + plainTicks + " mud=" + stridedTicks);
        }

        private static int Lap(TestContext t, Pawn walker, int z)
        {
            t.Place(walker, From, z);
            t.Step(2);
            return t.WalkTo(walker, t.Arena.Cell(To, z), 400);
        }
    }
}
