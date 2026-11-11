using System;
using System.Text;
using Verse;

namespace WhisperOfTheForest
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

        public static void Record(string category, Thing actor, string detail)
        {
            // Only the lab configures a writer; a normal game records nothing.
            BoundedJsonlWriter active = writer;
            if (active == null)
            {
                return;
            }

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
            json.Append(",\"category\":").Append(Json.Str(category));
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

        private static string SafeName(Thing thing)
        {
            if (thing is Pawn pawn)
            {
                return pawn.Name != null ? pawn.Name.ToStringShort : pawn.LabelShort;
            }

            return thing.LabelShort;
        }
    }
}
