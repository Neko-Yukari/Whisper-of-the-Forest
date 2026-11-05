using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Movement
{
    /// <summary>
    /// Read-only snapshot of one pawn's movement experiment counters. Used by in-game
    /// verification to compare walked cells, blinked cells and block reasons against
    /// elapsed ticks; it is also the machine readable part of the experiment log lines.
    /// </summary>
    public struct StrideSnapshot
    {
        public int WalkedCells;
        public int BlinkedCells;
        public int BlockedHops;
        public int TerrainCellsFreed;
        public string LastBlockReason;
        public int FirstTick;
        public int LastTick;

        public int CellsMoved
        {
            get { return WalkedCells + BlinkedCells; }
        }

        public int TicksElapsed
        {
            get { return (FirstTick < 0 || LastTick < 0) ? 0 : LastTick - FirstTick; }
        }
    }

    /// <summary>
    /// Counters and bounded logging for the movement experiment. Logging is throttled
    /// (first few events, then one line per interval, hard cap per pawn/category) so a
    /// long walk cannot spam the shared experiment log every tick.
    /// </summary>
    public static class StrideDiagnostics
    {
        private const int MaxLinesPerCategory = 40;
        private const int LogIntervalTicks = 240;
        private const int MaxTrackedPawns = 256;
        private const int MaxThrottleKeys = 1024;

        private sealed class Counters
        {
            public int WalkedCells;
            public int BlinkedCells;
            public int BlockedHops;
            public int TerrainCellsFreed;
            public string LastBlockReason;
            public int FirstTick = -1;
            public int LastTick = -1;
        }

        private static readonly Dictionary<int, Counters> counters = new Dictionary<int, Counters>();
        private static readonly Dictionary<string, int> lastLogTick = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> logLines = new Dictionary<string, int>();
        private static int logFailures;

        /// <summary>Number of experiment-log writes that threw (must stay 0 in a healthy run).</summary>
        public static int LogFailureCount
        {
            get { return logFailures; }
        }

        public static StrideSnapshot GetSnapshot(Pawn pawn)
        {
            var snapshot = new StrideSnapshot();
            if (pawn == null)
            {
                return snapshot;
            }

            Counters c;
            if (!counters.TryGetValue(pawn.thingIDNumber, out c))
            {
                return snapshot;
            }

            snapshot.WalkedCells = c.WalkedCells;
            snapshot.BlinkedCells = c.BlinkedCells;
            snapshot.BlockedHops = c.BlockedHops;
            snapshot.TerrainCellsFreed = c.TerrainCellsFreed;
            snapshot.LastBlockReason = c.LastBlockReason;
            snapshot.FirstTick = c.FirstTick;
            snapshot.LastTick = c.LastTick;
            return snapshot;
        }

        public static string Describe(Pawn pawn)
        {
            if (pawn == null)
            {
                return "movement-stride: <null pawn>";
            }

            StrideSnapshot s = GetSnapshot(pawn);
            return string.Format(
                "movement-stride {0}: walked={1} blinked={2} moved={3} ticks={4} blocked={5} terrainCellsFreed={6} lastBlock={7} terrainStride={8} blinkStride={9}",
                pawn.LabelShortCap,
                s.WalkedCells,
                s.BlinkedCells,
                s.CellsMoved,
                s.TicksElapsed,
                s.BlockedHops,
                s.TerrainCellsFreed,
                s.LastBlockReason ?? "none",
                StrideApparel.IsWearingTerrainStride(pawn),
                StrideApparel.IsWearingBlinkStride(pawn));
        }

        /// <summary>
        /// Diagnostic for a single cell: what the terrain would charge normally and what
        /// the wearer actually pays. Shows the terrain-only part, not building/snow costs.
        /// </summary>
        public static string DescribeTerrainCost(Pawn pawn, IntVec3 cell)
        {
            if (pawn == null || pawn.Map == null)
            {
                return "movement-terrain: <no pawn or map>";
            }

            TerrainDef terrain = GridsUtility.GetTerrain(cell, pawn.Map);
            if (terrain == null)
            {
                return string.Format("movement-terrain cell={0} terrain=<none>", cell);
            }

            bool stride = StrideApparel.IsWearingTerrainStride(pawn);
            return string.Format(
                "movement-terrain cell={0} terrain={1} pathCost={2} terrainStride={3} effectiveTerrainCost={4}",
                cell,
                terrain.defName,
                terrain.pathCost,
                stride,
                stride ? 0 : terrain.pathCost);
        }

        public static void Reset(Pawn pawn)
        {
            if (pawn != null)
            {
                counters.Remove(pawn.thingIDNumber);
            }
        }

        /// <summary>Player-facing one-liner for the UI or a manual test note.</summary>
        public static string DescribeLocalized(Pawn pawn)
        {
            if (pawn == null)
            {
                return "movement-stride: <null pawn>";
            }

            StrideSnapshot s = GetSnapshot(pawn);
            return "WotF_Movement_StatusLine".Translate(
                pawn.LabelShortCap,
                s.WalkedCells,
                s.BlinkedCells,
                s.BlockedHops,
                s.LastBlockReason ?? "-");
        }

        internal static void NoteWalked(Pawn pawn, IntVec3 from, IntVec3 to)
        {
            Counters c = Touch(pawn);
            c.WalkedCells++;
            LogThrottled(
                pawn,
                "normalStep",
                string.Format("normalStep from={0} to={1} walked={2} blinked={3}", from, to, c.WalkedCells, c.BlinkedCells));
        }

        internal static void NoteBlink(Pawn pawn, IntVec3 from, IntVec3 to, int nodesLeft)
        {
            Counters c = Touch(pawn);
            c.BlinkedCells++;
            LogThrottled(
                pawn,
                "blink",
                string.Format("blink from={0} to={1} nodesLeft={2} blinked={3} walked={4}", from, to, nodesLeft, c.BlinkedCells, c.WalkedCells));
        }

        internal static void NoteBlocked(Pawn pawn, string reason)
        {
            Counters c = Touch(pawn);
            c.BlockedHops++;
            c.LastBlockReason = reason;
            LogThrottled(pawn, "blocked", string.Format("blink skipped reason={0} blocked={1}", reason, c.BlockedHops));
        }

        internal static void NoteTerrainCellFreed(Pawn pawn, IntVec3 cell, TerrainDef terrain)
        {
            Counters c = Touch(pawn);
            c.TerrainCellsFreed++;
            LogThrottled(
                pawn,
                "terrain",
                string.Format(
                    "terrain cost ignored cell={0} terrain={1} pathCost={2} cellsFreed={3}",
                    cell,
                    terrain != null ? terrain.defName : "<none>",
                    terrain != null ? terrain.pathCost : 0,
                    c.TerrainCellsFreed));
        }

        private static Counters Touch(Pawn pawn)
        {
            Counters c;
            if (counters.TryGetValue(pawn.thingIDNumber, out c))
            {
                c.LastTick = GenTicks.TicksGame;
                return c;
            }

            if (counters.Count >= MaxTrackedPawns)
            {
                counters.Clear();
            }

            c = new Counters();
            c.FirstTick = GenTicks.TicksGame;
            c.LastTick = c.FirstTick;
            counters[pawn.thingIDNumber] = c;
            return c;
        }

        /// <summary>
        /// Logging must never interrupt pather bookkeeping, so failures are counted and
        /// swallowed instead of propagating out of a Harmony patch.
        /// </summary>
        internal static void LogThrottled(Pawn pawn, string category, string detail)
        {
            if (pawn == null)
            {
                return;
            }

            string key = pawn.thingIDNumber + ":" + category;
            int lines;
            logLines.TryGetValue(key, out lines);
            if (lines >= MaxLinesPerCategory)
            {
                return;
            }

            int last;
            lastLogTick.TryGetValue(key, out last);
            int now = GenTicks.TicksGame;
            if (lines >= 3 && now - last < LogIntervalTicks)
            {
                return;
            }

            if (logLines.Count >= MaxThrottleKeys)
            {
                logLines.Clear();
                lastLogTick.Clear();
            }

            logLines[key] = lines + 1;
            lastLogTick[key] = now;
            Write(pawn, category, detail);
        }

        /// <summary>For rare, explicit events such as equipping an experiment item.</summary>
        internal static void LogDirect(Pawn pawn, string category, string detail)
        {
            if (pawn == null)
            {
                return;
            }

            Write(pawn, category, detail);
        }

        private static void Write(Pawn pawn, string category, string detail)
        {
            try
            {
                ExperimentLog.Record("movement." + category, pawn, detail);
            }
            catch
            {
                logFailures++;
            }
        }
    }
}
