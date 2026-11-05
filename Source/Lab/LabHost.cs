using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Verse;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest
{
    /// <summary>
    /// File-based experiment driver. Disabled unless the game is started with
    /// "-wotflab=D:/path/to/lab". Requests are read from "requests.txt" and results are appended
    /// to "results.jsonl". All work happens in Root_Play.Update on the Unity main thread; there
    /// is no socket, no background game access and no arbitrary code execution.
    /// </summary>
    public static class LabHost
    {
        public const int ProtocolVersion = 1;
        public const string Version = "0.1.0";
        public const string SwitchPrefix = "-wotflab=";

        private const int MaxCommandsPerPump = 8;
        private const int MaxRequestChunkBytes = 64 * 1024;
        private const long MaxResultsBytes = 8 * 1024 * 1024;

        private static readonly Queue<string> PendingLines = new Queue<string>();
        private static readonly long PollInterval = Stopwatch.Frequency / 4;

        private static long requestOffset;
        private static long lastPoll;
        private static bool harmonyPatched;
        private static BoundedJsonlWriter results;

        public static bool Enabled { get; private set; }
        public static string LabDirectory { get; private set; }
        public static string RequestPath { get; private set; }
        public static string ResultPath { get; private set; }

        internal static void MarkHarmonyPatched()
        {
            harmonyPatched = true;
        }

        public static void ConfigureFromCommandLine()
        {
            try
            {
                string directory = null;
                foreach (string raw in Environment.GetCommandLineArgs())
                {
                    if (raw != null && raw.StartsWith(SwitchPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        directory = raw.Substring(SwitchPrefix.Length);
                    }
                }

                if (string.IsNullOrWhiteSpace(directory))
                {
                    return;
                }

                directory = Path.GetFullPath(directory.Trim().Trim('"').TrimEnd('/', '\\'));
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                LabDirectory = directory;
                RequestPath = Path.Combine(directory, "requests.txt");
                ResultPath = Path.Combine(directory, "results.jsonl");
                results = new BoundedJsonlWriter(ResultPath, MaxResultsBytes, flushEachLine: true);
                Enabled = true;

                // Commands already in the file belong to a previous run; only new lines are executed.
                requestOffset = File.Exists(RequestPath) ? new FileInfo(RequestPath).Length : 0;

                ExperimentLog.Configure(Path.Combine(directory, "experiment.jsonl"));
                ExperimentLog.Record("lab", null, "lab enabled dir=" + directory);
                WriteStatusFile();
                Log.Message("[WotF-Lab] File lab enabled: " + directory);
            }
            catch (Exception exception)
            {
                Enabled = false;
                Log.Error("[WotF-Lab] Could not enable file lab: " + exception);
            }
        }

        public static void OnMainThreadUpdate()
        {
            if (!Enabled)
            {
                return;
            }

            try
            {
                if (Current.Game != null && Find.TickManager != null)
                {
                    ExperimentLog.CurrentTick = Find.TickManager.TicksGame;
                }
            }
            catch
            {
                ExperimentLog.CurrentTick = -1;
            }

            long now = Stopwatch.GetTimestamp();
            if (now - lastPoll < PollInterval)
            {
                return;
            }

            lastPoll = now;

            try
            {
                PumpRequests();
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[WotF-Lab] Request pump failed: " + exception, 0x57544602);
            }

            try
            {
                LabTestRunner.OnMainThreadUpdate();
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[WotF-Lab] Test runner pump failed: " + exception, 0x57544604);
            }

            try
            {
                LabBehaviorTracker.Tick();
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[WotF-Lab] Behavior observation failed: " + exception, 0x57544603);
            }

            ExperimentLog.FlushIfDue();
        }

        private static void PumpRequests()
        {
            if (!File.Exists(RequestPath))
            {
                return;
            }

            if (PendingLines.Count == 0)
            {
                try
                {
                    ReadNewRequestLines();
                }
                catch (IOException)
                {
                    // A transient sharing violation while the external writer appends; retry next poll.
                    return;
                }
            }

            int executed = 0;
            while (PendingLines.Count > 0 && executed < MaxCommandsPerPump)
            {
                string line = PendingLines.Dequeue();
                executed++;
                ExecuteLine(line);
            }
        }

        private static void ReadNewRequestLines()
        {
            using (FileStream stream = new FileStream(RequestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length < requestOffset)
                {
                    // The file was truncated or replaced; start over.
                    requestOffset = 0;
                }

                long available = stream.Length - requestOffset;
                if (available <= 0)
                {
                    return;
                }

                int toRead = (int)Math.Min(available, MaxRequestChunkBytes);
                byte[] buffer = new byte[toRead];
                stream.Seek(requestOffset, SeekOrigin.Begin);
                int read = stream.Read(buffer, 0, toRead);
                int lastNewline = -1;
                for (int i = read - 1; i >= 0; i--)
                {
                    if (buffer[i] == (byte)'\n')
                    {
                        lastNewline = i;
                        break;
                    }
                }

                if (lastNewline < 0)
                {
                    if (available > MaxRequestChunkBytes)
                    {
                        Log.Error("[WotF-Lab] Dropping oversized request line (" + available + " bytes, no newline)");
                        requestOffset = stream.Length;
                    }

                    return;
                }

                string text = Encoding.UTF8.GetString(buffer, 0, lastNewline + 1);
                requestOffset += lastNewline + 1;
                foreach (string raw in text.Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (line.Length > 1024)
                    {
                        WriteError("oversized", "request line exceeds 1024 characters");
                        continue;
                    }

                    PendingLines.Enqueue(line);
                }
            }
        }

        private static void ExecuteLine(string line)
        {
            string id = "?";
            try
            {
                line = line.TrimStart('\uFEFF');
                string[] tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2)
                {
                    WriteError(id, "expected '<id> <command> [key=value ...]'");
                    return;
                }

                id = tokens[0];
                string command = tokens[1];
                Dictionary<string, string> arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 2; i < tokens.Length; i++)
                {
                    int equals = tokens[i].IndexOf('=');
                    if (equals <= 0)
                    {
                        continue;
                    }

                    string key = tokens[i].Substring(0, equals);
                    string value = tokens[i].Substring(equals + 1).Trim('"');
                    arguments[key] = value;
                }

                string result = LabCommands.Execute(id, command, arguments);
                WriteRaw(result);
            }
            catch (LabCommandException exception)
            {
                WriteError(id, exception.Message);
            }
            catch (Exception exception)
            {
                Log.Error("[WotF-Lab] Command '" + line + "' failed: " + exception);
                WriteError(id, "internal error: " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        internal static void WriteError(string id, string message)
        {
            WriteRaw("{\"id\":" + Json.Str(id) + ",\"ok\":false,\"error\":" + Json.Str(message) + "}");
        }

        private static void WriteRaw(string json)
        {
            if (results == null)
            {
                return;
            }

            results.WriteLine(json);
            results.Flush();
        }

        private static void WriteStatusFile()
        {
            try
            {
                StringBuilder json = new StringBuilder();
                json.Append('{');
                json.Append("\"lab\":").Append(Json.Str(Version));
                json.Append(",\"protocol\":").Append(Json.Num(ProtocolVersion));
                json.Append(",\"request\":").Append(Json.Str(RequestPath));
                json.Append(",\"results\":").Append(Json.Str(ResultPath));
                json.Append(",\"startedUtc\":").Append(Json.Str(DateTime.UtcNow.ToString("o")));
                json.Append('}');
                File.WriteAllText(Path.Combine(LabDirectory, "lab-status.json"), json.ToString(), new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                Log.Warning("[WotF-Lab] Could not write lab-status.json: " + exception.Message);
            }
        }

        internal static bool IsHarmonyPatched
        {
            get { return harmonyPatched; }
        }
    }

    internal sealed class LabCommandException : Exception
    {
        public LabCommandException(string message) : base(message)
        {
        }
    }
}
