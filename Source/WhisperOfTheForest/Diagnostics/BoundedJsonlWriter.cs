using System;
using System.IO;
using System.Text;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Append-only JSONL file with a hard size cap. When the cap is reached the current file is
    /// rotated once to "*.1" so a long session cannot fill the disk or grow without bound.
    /// Lab results flush immediately (one command at a time); experiment records are buffered.
    /// </summary>
    internal sealed class BoundedJsonlWriter
    {
        private const long DefaultFlushIntervalTicks = TimeSpan.TicksPerSecond * 2;

        private readonly string path;
        private readonly long maxBytes;
        private readonly bool flushEachLine;
        private readonly object gate = new object();
        private readonly StringBuilder buffer = new StringBuilder();
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private long lastFlushTicks = DateTime.UtcNow.Ticks;
        private bool failedLogged;

        public BoundedJsonlWriter(string path, long maxBytes, bool flushEachLine)
        {
            this.path = path;
            this.maxBytes = maxBytes;
            this.flushEachLine = flushEachLine;
        }

        public void WriteLine(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            lock (gate)
            {
                buffer.Append(json);
                buffer.Append('\n');

                if (flushEachLine || buffer.Length >= 8192 || DateTime.UtcNow.Ticks - lastFlushTicks >= DefaultFlushIntervalTicks)
                {
                    FlushLocked();
                }
            }
        }

        public void FlushIfDue()
        {
            lock (gate)
            {
                if (buffer.Length > 0 && DateTime.UtcNow.Ticks - lastFlushTicks >= DefaultFlushIntervalTicks)
                {
                    FlushLocked();
                }
            }
        }

        public void Flush()
        {
            lock (gate)
            {
                FlushLocked();
            }
        }

        private void FlushLocked()
        {
            if (buffer.Length == 0)
            {
                return;
            }

            try
            {
                EnsureDirectory();
                string text = buffer.ToString();
                if (File.Exists(path) && new FileInfo(path).Length + Encoding.UTF8.GetByteCount(text) > maxBytes)
                {
                    // Keep exactly one previous file. The pending batch starts the new file.
                    string backup = path + ".1";
                    if (File.Exists(backup))
                    {
                        File.Delete(backup);
                    }

                    File.Move(path, backup);
                }

                File.AppendAllText(path, text, Utf8NoBom);
                buffer.Clear();
            }
            catch (Exception exception)
            {
                // Keep the buffer so a later flush can retry, but never let it grow without bound.
                if (buffer.Length > 256 * 1024)
                {
                    buffer.Clear();
                }

                if (!failedLogged)
                {
                    failedLogged = true;
                    Verse.Log.Error("[WotF-Lab] Failed writing " + path + ": " + exception);
                }
            }
            finally
            {
                lastFlushTicks = DateTime.UtcNow.Ticks;
            }
        }

        private void EnsureDirectory()
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
