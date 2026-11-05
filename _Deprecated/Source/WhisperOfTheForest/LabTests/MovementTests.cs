using RimWorld;
using Verse;
using WhisperOfTheForest.Experimental.Movement;
using WhisperOfTheForest.Experimental.Testing;

namespace WhisperOfTheForest.Experimental.LabTests
{
    /// <summary>
    /// Deprecated 2026-11-05: divides terrain cost by walls, which is a movement implementation
    /// detail rather than a system handoff; the kept suite covers apparel -> pathing with
    /// movement.terrain-stride. Kept for the record; not compiled and not part of the L2 suite.
    /// </summary>
    [LabTest]
    public sealed class Movement_TerrainStrideKeepsWalls : LabTest
    {
        public override string Name => "movement.terrain-stride-walls";

        protected override void Run(TestContext t)
        {
            Pawn walker = t.SpawnTarget(1, 5);

            // A sealed 3x3 room around the destination: no route around it can ever exist.
            for (int x = 7; x <= 9; x++)
            {
                t.SpawnWall(x, 4, ThingDefOf.Steel);
                t.SpawnWall(x, 6, ThingDefOf.Steel);
            }

            t.SpawnWall(7, 5, ThingDefOf.Steel);
            t.SpawnWall(9, 5, ThingDefOf.Steel);

            if (!LabActions.TryEquipTerrainStride(walker))
            {
                t.Fail("could not equip TerrainStride: " + LabActions.LastError);
                return;
            }

            IntVec3 destination = t.Arena.Cell(8, 5);
            IntVec3 outside = t.Arena.Cell(4, 5);
            bool reachable = t.CanReach(walker, destination);
            bool openReachable = t.CanReach(walker, outside);
            t.Note("roomReachable", reachable);
            t.Note("openReachable", openReachable);
            t.Expect(openReachable, "the open ground in front of the room is not reachable");
            t.Expect(!reachable, "a destination inside a sealed room is reported reachable");

            int ticks = t.WalkTo(walker, destination, 150);
            t.Note("walkTicks", ticks);
            t.Note("positionX", walker.Position.x - t.Arena.MinX);
            t.Expect(ticks < 0, "the strided pawn reached a destination inside a sealed room");
            t.Expect(walker.Position != destination, "the strided pawn stands on the sealed room cell");
        }
    }
}
