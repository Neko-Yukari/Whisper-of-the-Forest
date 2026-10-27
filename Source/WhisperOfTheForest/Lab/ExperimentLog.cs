using System;
using System.IO;
using System.Text;
using Verse;

namespace WhisperOfTheForest.Experimental
{
    /// <summary>
    /// Public observation hook used by all feature folders (missiles, movement, UI, lab).
    /// Actor may be null. Writes a bounded JSONL record; never touches the game state.
    /// </summary>
    public static class ExperimentLog
    {
        private const long MaxLogBytes = 2 * 1024 * 1024;
        private static readonly object Gate = new object();

        private static BoundedJsonlWriter writer;
        private static string writerPath;
        private static string lastKey;
        private static long lastKeyTicks;
        private static int currentTick = -1;

        /// <summary>Tick cached on the main thread; safe to read from any thread.</summary>
        internal static int CurrentTick
        {
            get { return currentTick; }
            set { currentTick = value; }
        }

        internal static void Configure(string path)
        {
            lock (Gate)
            {
                writerPath = path;
                writer = new BoundedJsonlWriter(path, MaxLogBytes, flushEachLine: false);
            }
        }

        internal static void FlushIfDue()
        {
            BoundedJsonlWriter active = writer;
            if (active != null)
            {
                active.FlushIfDue();
            }
        }

        public static void Record(string category, Pawn actor, string detail)
        {
            try
            {
                BoundedJsonlWriter active = EnsureWriter();
                string key = category + "|" + (actor != null ? actor.thingIDNumber.ToString() : "-") + "|" + detail;
                long now = DateTime.UtcNow.Ticks;
                lock (Gate)
                {
                    // Collapse identical repeated events (e.g. a per-tick notification) to 1/second.
                    if (key == lastKey && now - lastKeyTicks < TimeSpan.TicksPerSecond)
                    {
                        return;
                    }

                    lastKey = key;
                    lastKeyTicks = now;
                }

                StringBuilder json = new StringBuilder(160);
                json.Append('{');
                json.Append("\"ts\":").Append(Json.Str(DateTime.UtcNow.ToString("o")));
                json.Append(",\"tick\":").Append(Json.Num(currentTick));
                json.Append(",\"category\":").Append(Json.Str(category ?? "general"));
                json.Append(",\"actor\":");
                if (actor == null)
                {
                    json.Append("null");
                }
                else
                {
                    json.Append('{');
                    json.Append("\"id\":").Append(Json.Num(actor.thingIDNumber));
                    json.Append(",\"name\":").Append(Json.Str(SafeName(actor)));
                    json.Append(",\"def\":").Append(Json.Str(actor.def != null ? actor.def.defName : null));
                    json.Append(",\"pos\":").Append(actor.Spawned ? Json.Cell(actor.Position) : "null");
                    json.Append('}');
                }

                json.Append(",\"detail\":").Append(Json.Str(detail));
                json.Append('}');
                active.WriteLine(json.ToString());
            }
            catch (Exception exception)
            {
                // Logging must never break gameplay.
                Log.ErrorOnce("[WotF-Lab] ExperimentLog.Record failed: " + exception, 0x57544601);
            }
        }

        private static BoundedJsonlWriter EnsureWriter()
        {
            lock (Gate)
            {
                if (writer != null)
                {
                    return writer;
                }

                string path = writerPath ?? DefaultPath();
                writerPath = path;
                writer = new BoundedJsonlWriter(path, MaxLogBytes, flushEachLine: false);
                return writer;
            }
        }

        private static string DefaultPath()
        {
            string folder = Path.Combine(GenFilePaths.SaveDataFolderPath, "WotFExperiments");
            return Path.Combine(folder, "experiment.jsonl");
        }

        private static string SafeName(Pawn pawn)
        {
            try
            {
                return pawn.Name != null ? pawn.Name.ToStringShort : pawn.LabelShort;
            }
            catch
            {
                return "pawn-" + pawn.thingIDNumber;
            }
        }
    }
}
