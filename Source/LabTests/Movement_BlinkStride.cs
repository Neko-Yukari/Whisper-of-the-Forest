using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Movement;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Apparel to pathing: BlinkStride hops along the pawn's own path - at least one two-cell step,
    /// never more, and every cell it stands on stays walkable.
    /// </summary>
    [LabTest]
    public sealed class Movement_BlinkStride : LabTest
    {
        public override string Name => "movement.blink-stride";

        protected override void Run(TestContext t)
        {
            Pawn walker = t.SpawnTarget(1, 5);

            // A one-cell-wide corridor, so every step is on the row under test and a hop is visible.
            for (int x = 2; x <= 12; x++)
            {
                t.SpawnWall(x, 4, ThingDefOf.Steel);
                t.SpawnWall(x, 6, ThingDefOf.Steel);
            }

            if (!LabActions.TryEquipBlinkStride(walker))
            {
                t.Fail("could not equip BlinkStride: " + LabActions.LastError);
                return;
            }

            t.Expect(TestContext.Wears(walker, StrideApparel.BlinkStrideDefName),
                "BlinkStride is not worn after equipping");

            IntVec3 destination = t.Arena.Cell(12, 5);
            Job job = JobMaker.MakeJob(JobDefOf.Goto, new LocalTargetInfo(destination));
            walker.jobs.TryTakeOrderedJob(job, null, false);

            int hops = 0;
            int jumps = 0;
            int steps = 0;
            var unwalkable = new List<IntVec3>();
            IntVec3 previous = walker.Position;
            for (int i = 0; i < 400; i++)
            {
                t.Step(1);
                IntVec3 now = walker.Position;
                if (now == previous)
                {
                    continue;
                }

                int dx = System.Math.Abs(now.x - previous.x);
                int dz = System.Math.Abs(now.z - previous.z);
                int distance = System.Math.Max(dx, dz);
                steps++;
                if (distance == 2)
                {
                    hops++;
                }
                else if (distance > 2)
                {
                    jumps++;
                }

                if (!t.Walkable(now, walker) && unwalkable.Count < 5)
                {
                    unwalkable.Add(now);
                }

                previous = now;
                if (now == destination)
                {
                    break;
                }
            }

            t.Note("steps", steps);
            t.Note("hops", hops);
            t.Note("jumps", jumps);
            t.Note("arrived", walker.Position == destination);
            t.Expect(walker.Position == destination, "the blink walker did not reach the destination");
            t.Expect(hops >= 1, "no two-cell hop was observed");
            t.Expect(jumps == 0, "a step covered more than two cells");
            t.Expect(unwalkable.Count == 0, "the walker stood on unwalkable cells: "
                                           + string.Join(",", unwalkable.ConvertAll(c => c.ToString()).ToArray()));
        }
    }
}
