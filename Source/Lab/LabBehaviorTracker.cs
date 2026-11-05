using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Bounded, read-only A17 observation for pawns created by the lab setup/behaviors fixture.
    /// It never drives pawns or changes jobs; it only counts real job/map/projectile events.
    /// </summary>
    internal static class LabBehaviorTracker
    {
        private sealed class Counters
        {
            public int SowStarts;
            public int SowCompletions;
            public int Shots;
            public bool SowActive;
            public IntVec3 SowCell;
            public string CropDefName;
            public string LastShotTarget;
        }

        private static readonly HashSet<int> Prepared = new HashSet<int>();
        private static readonly Dictionary<int, Counters> Counts = new Dictionary<int, Counters>();

        public static void Clear()
        {
            Prepared.Clear();
            Counts.Clear();
        }

        public static void RegisterPrepared(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            if (Prepared.Count >= 8)
            {
                Prepared.Clear();
                Counts.Clear();
            }

            Prepared.Add(pawn.thingIDNumber);
            if (!Counts.ContainsKey(pawn.thingIDNumber))
            {
                Counts[pawn.thingIDNumber] = new Counters();
            }
        }

        public static bool IsPrepared(Pawn pawn)
        {
            return pawn != null && Prepared.Contains(pawn.thingIDNumber);
        }

        /// <summary>Called from the lab main-thread pump; observes real Sow jobs and planted cells.</summary>
        public static void Tick()
        {
            if (!LabHost.Enabled || Current.Game == null || Find.CurrentMap == null)
            {
                return;
            }

            Map map = Find.CurrentMap;
            foreach (int id in Prepared)
            {
                Pawn pawn = FindPawn(map, id);
                if (pawn == null)
                {
                    continue;
                }

                Counters c = Counts[id];
                Job job = pawn.CurJob;
                bool sowJob = job != null && job.def == JobDefOf.Sow;
                if (sowJob && job.targetA.IsValid)
                {
                    IntVec3 cell = job.targetA.Cell;
                    if (!c.SowActive || c.SowCell != cell)
                    {
                        c.SowActive = true;
                        c.SowCell = cell;
                        c.CropDefName = job.plantDefToSow != null ? job.plantDefToSow.defName : null;
                        c.SowStarts++;
                        ExperimentLog.Record(
                            "lab.behavior",
                            pawn,
                            "sow-start cell=" + cell + " crop=" + (c.CropDefName ?? "none") + " starts=" + c.SowStarts);
                    }

                    continue;
                }

                if (c.SowActive)
                {
                    // Job ended: the sow either produced a Plant or failed. Only count a real planted def.
                    Plant plant = c.SowCell.GetPlant(map);
                    bool planted = plant != null && plant.Growth > 0.001f
                        && (c.CropDefName == null || plant.def.defName == c.CropDefName);
                    if (planted)
                    {
                        c.SowCompletions++;
                        ExperimentLog.Record(
                            "lab.behavior",
                            pawn,
                            "sow-complete cell=" + c.SowCell + " crop=" + plant.def.defName +
                            " growth=" + plant.Growth + " completions=" + c.SowCompletions);
                    }

                    c.SowActive = false;
                }
            }
        }

        public static void RecordShot(Pawn shooter, LocalTargetInfo target)
        {
            if (!LabHost.Enabled || !IsPrepared(shooter))
            {
                return;
            }

            Counters c = Counts[shooter.thingIDNumber];
            c.Shots++;
            c.LastShotTarget = target.Cell.ToString();
            if (c.Shots <= 5 || c.Shots % 25 == 0)
            {
                ExperimentLog.Record(
                    "lab.behavior",
                    shooter,
                    "shot target=" + c.LastShotTarget + " shots=" + c.Shots);
            }
        }

        public static string SnapshotForPawn(Pawn pawn)
        {
            if (pawn == null || !IsPrepared(pawn))
            {
                return "{\"prepared\":false}";
            }

            Counters c = Counts[pawn.thingIDNumber];
            StringBuilder json = new StringBuilder(192);
            json.Append('{');
            json.Append("\"prepared\":true");
            json.Append(",\"sowStarts\":").Append(Json.Num(c.SowStarts));
            json.Append(",\"sowCompletions\":").Append(Json.Num(c.SowCompletions));
            json.Append(",\"shots\":").Append(Json.Num(c.Shots));
            json.Append(",\"sowActive\":").Append(Json.Bool(c.SowActive));
            json.Append(",\"sowCell\":").Append(c.SowCell.IsValid ? Json.Cell(c.SowCell) : "null");
            json.Append(",\"crop\":").Append(Json.Str(c.CropDefName));
            json.Append(",\"lastShotTarget\":").Append(Json.Str(c.LastShotTarget));
            json.Append('}');
            return json.ToString();
        }

        public static string SnapshotAll()
        {
            StringBuilder json = new StringBuilder(256);
            json.Append('[');
            bool first = true;
            Map map = Current.Game != null ? Find.CurrentMap : null;
            if (map != null)
            {
                foreach (int id in Prepared)
                {
                    Pawn pawn = FindPawn(map, id);
                    if (pawn == null)
                    {
                        continue;
                    }

                    if (!first)
                    {
                        json.Append(',');
                    }

                    first = false;
                    json.Append('{');
                    json.Append("\"id\":").Append(Json.Num(pawn.thingIDNumber));
                    json.Append(",\"name\":").Append(Json.Str(pawn.Name != null ? pawn.Name.ToStringShort : pawn.LabelShort));
                    string state = SnapshotForPawn(pawn);
                    json.Append(",\"state\":").Append(state);
                    json.Append('}');
                }
            }

            json.Append(']');
            return json.ToString();
        }

        private static Pawn FindPawn(Map map, int id)
        {
            foreach (Thing thing in map.spawnedThings)
            {
                if (thing != null && thing.thingIDNumber == id && thing is Pawn pawn)
                {
                    return pawn;
                }
            }

            return null;
        }
    }
}
