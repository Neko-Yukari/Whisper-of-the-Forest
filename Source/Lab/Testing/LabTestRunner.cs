using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Verse;

namespace WhisperOfTheForest.Testing
{
    /// <summary>
    /// Runs every <see cref="LabTest"/> in the assembly, one arena and one outcome at a time, and
    /// writes a structured report. Called from the lab pump on the game's main thread, so the whole
    /// run is one synchronous session: game time advances with <see cref="TickManager.DoSingleTick"/>,
    /// nothing waits for real time.
    ///
    /// A test that throws or fails an assertion never stops the run. Tests that need a real
    /// save/load are the only ones that pause the run for a frame: the save is written, the game
    /// loads it, and the same test continues in phase two.
    /// </summary>
    internal static class LabTestRunner
    {
        private const int ReadyUpdateTimeout = 6000;
        private const int MaxSamplesInReport = 6;

        private sealed class Entry
        {
            internal string Name;
            internal Type Type;
            internal bool ExpectedFailure;
            internal string ExpectedFailureReason;
            internal bool Reload;
        }

        private sealed class RunState
        {
            internal string RunId;
            internal string Filter;
            internal string Fixture;
            internal int Repeats = 1;
            internal int Pass = 1;
            internal bool ReloadFixture;
            internal readonly List<Entry> Entries = new List<Entry>();
            internal string Directory;
            internal long StartedUtcTicks;
            internal int StartTick;
            internal int Speed;
            internal bool SpeedChanged;
            internal Game GameBeforeFixture;
            internal bool FixtureLoaded;
            internal TestLogWatch RunLog;
            internal TestLogWatch LoadLog;
            internal LabLoadCheck.Result Load;
            internal readonly List<TestOutcome> Outcomes = new List<TestOutcome>();
            internal readonly List<Entry> Pending = new List<Entry>();
            internal int Index = -1;
            internal int UpdatesWaiting;
            internal bool CancelRequested;

            // Phase state of the test at Pending[Index]: created at phase one, carried through the load.
            internal Entry Current;
            internal LabTest Instance;
            internal TestOutcome Outcome;
            internal string ReloadSaveName;
            internal TestContext Context;
            internal TestArena Arena;
            internal IntVec3 ArenaOrigin;
            internal int ArenaWidth;
            internal int ArenaHeight;
            internal Game GameBeforeReload;
            internal long PhaseStartedMilliseconds;
        }

        private enum Stage
        {
            LoadFixture,
            AwaitFixture,
            RunTest,
            AwaitReload,
            Report,
            Done
        }

        private static RunState state;
        private static Stage stage = Stage.Done;
        private static string lastSummary = "no run yet";

        /// <summary>
        /// The fixture is loaded once per game process. Tests build and clean their own scenes, so a
        /// second run in the same instance reuses the map it already has; only a fresh instance or an
        /// explicit reload request touches the fixture save.
        /// </summary>
        private static bool fixtureLoadedInProcess;
        private static string fixtureLoadedName;

        internal static bool Running => state != null;

        internal static string Status => lastSummary;

        // ------------------------------------------------------------------ commands

        internal static bool Begin(string filter, string fixture, int repeats, bool reloadFixture,
            out string json, out string error)
        {
            json = null;
            error = null;
            if (Running)
            {
                error = "a test run is already in progress";
                return false;
            }

            List<Entry> entries = Discover(filter);
            if (entries.Count == 0)
            {
                error = "no test matches filter '" + filter + "'";
                return false;
            }

            var run = new RunState
            {
                RunId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"),
                Filter = filter,
                Fixture = fixture,
                Repeats = Math.Min(Math.Max(1, repeats), 200),
                ReloadFixture = reloadFixture,
                StartedUtcTicks = DateTime.UtcNow.Ticks,
                StartTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0
            };

            // Taken before any fixture load: a save that fails to restore (a removed Def, a broken
            // reference) logs its errors here, and starting the watch afterwards would hide them.
            run.LoadLog = TestLogWatch.Now();

            run.Directory = Path.Combine(LabHost.LabDirectory, "test-runs");
            System.IO.Directory.CreateDirectory(run.Directory);
            run.Entries.AddRange(entries);
            run.Pending.AddRange(entries);
            state = run;
            if (NeedsFixtureLoad(run))
            {
                stage = Stage.LoadFixture;
            }
            else
            {
                EnterTesting();
            }
            lastSummary = "running filter='" + filter + "' tests=" + entries.Count + " repeat=" + run.Repeats;
            Log.Message("[WotF-Lab] test run " + run.RunId + " started: filter='" + filter + "' fixture='"
                        + (string.IsNullOrEmpty(fixture) ? "current map" : fixture) + "' tests=" + entries.Count
                        + " repeat=" + run.Repeats + " reload=" + (reloadFixture ? "yes" : "no"));
            json = "\"run\":" + Str(run.RunId) + ",\"tests\":" + entries.Count
                   + ",\"repeat\":" + run.Repeats
                   + ",\"fixtureLoaded\":" + ((stage == Stage.LoadFixture) ? "true" : "false")
                   + ",\"file\":" + Str(ReportPath(run, run.RunId));
            return true;
        }

        /// <summary>True when this run has to (re)load the fixture save before testing.</summary>
        private static bool NeedsFixtureLoad(RunState run)
        {
            if (string.IsNullOrEmpty(run.Fixture))
            {
                return false;
            }

            if (run.ReloadFixture)
            {
                return true;
            }

            return !(fixtureLoadedInProcess && fixtureLoadedName == run.Fixture)
                   || Current.Game == null || Find.CurrentMap == null;
        }

        internal static string Cancel()
        {
            if (state == null)
            {
                return "\"cancelled\":false,\"note\":\"no run in progress\"";
            }

            state.CancelRequested = true;
            return "\"cancelled\":true";
        }

        internal static string List(string filter)
        {
            List<Entry> entries = Discover(filter);
            var builder = new StringBuilder(256);
            builder.Append('[');
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append("{\"name\":\"").Append(entries[i].Name).Append('"')
                    .Append(",\"type\":\"").Append(entries[i].Type.Name).Append('"')
                    .Append(",\"expectedFailure\":").Append(entries[i].ExpectedFailure ? "true" : "false")
                    .Append(",\"reload\":").Append(entries[i].Reload ? "true" : "false")
                    .Append('}');
            }

            builder.Append(']');
            return builder.ToString();
        }

        // ------------------------------------------------------------------ pump

        /// <summary>Called from the lab pump once per frame while the game is not loading.</summary>
        internal static void OnMainThreadUpdate()
        {
            if (state == null)
            {
                return;
            }

            try
            {
                Pump();
            }
            catch (Exception exception)
            {
                Log.Error("[WotF-Lab] test runner failed: " + exception);
                if (state?.Context != null)
                {
                    state.Context.Fail("runner exception: " + exception.GetType().Name + ": " + exception.Message);
                }

                try
                {
                    FinishCurrent(exception.GetType().Name + ": " + exception.Message);
                }
                catch (Exception inner)
                {
                    Log.Error("[WotF-Lab] test runner cleanup failed: " + inner);
                }

                CompleteRun();
            }
        }

        private static void Pump()
        {
            switch (stage)
            {
                case Stage.LoadFixture:
                    BeginFixtureLoad();
                    return;
                case Stage.AwaitFixture:
                    if (TryEnterLoadedGame(ref state.GameBeforeFixture))
                    {
                        state.FixtureLoaded = true;
                        fixtureLoadedInProcess = true;
                        fixtureLoadedName = state.Fixture;
                        state.Load = LabLoadCheck.Run(DefsRoot(), typeof(LabTestRunner).Assembly.GetName().Name);
                        state.RunLog = TestLogWatch.Now();
                        Log.Message("[WotF-Lab] L1 " + LabLoadCheck.Describe(state.Load));
                        stage = Stage.RunTest;
                        state.Index = 0;
                    }
                    else if (!WaitForMoreFrames("fixture load"))
                    {
                        FailWholeRun("fixture '" + state.Fixture + "' did not finish loading");
                    }

                    return;
                case Stage.RunTest:
                    PumpTests();
                    return;
                case Stage.AwaitReload:
                    if (TryEnterLoadedGame(ref state.GameBeforeReload))
                    {
                        CompleteReloadedTest();
                    }
                    else if (!WaitForMoreFrames("reload"))
                    {
                        state.Context.Fail("save/load did not finish within the frame budget");
                        FinishCurrent("reload timeout");
                    }

                    return;
                case Stage.Report:
                    WriteReport();
                    CompleteRun();
                    return;
            }
        }

        /// <summary>Starts the test phase: L1 checks (about the game as loaded) and the first pass.</summary>
        private static void EnterTesting()
        {
            if (state.Load == null)
            {
                state.Load = LabLoadCheck.Run(DefsRoot(), typeof(LabTestRunner).Assembly.GetName().Name);
                Log.Message("[WotF-Lab] L1 " + LabLoadCheck.Describe(state.Load));
            }

            state.RunLog = TestLogWatch.Now();
            state.Pending.Clear();
            state.Pending.AddRange(state.Entries);
            state.Index = 0;
            stage = Stage.RunTest;
        }

        /// <summary>
        /// Advances to the next repetition of the same test list inside this instance, or reports.
        /// </summary>
        private static void FinishPass()
        {
            if (state.Pass < state.Repeats)
            {
                state.Pass++;
                Log.Message("[WotF-Lab] test run " + state.RunId + " pass " + state.Pass + "/" + state.Repeats);
                state.Pending.Clear();
                state.Pending.AddRange(state.Entries);
                state.Index = 0;
                stage = Stage.RunTest;
                return;
            }

            stage = Stage.Report;
        }

        private static void BeginFixtureLoad()
        {
            if (string.IsNullOrEmpty(state.Fixture))
            {
                EnterTesting();
                return;
            }

            // The lab pump also runs while the game is still generating its quicktest map. Queueing a
            // load then would race the generation event, so wait for a playable game first.
            if (LongEventHandler.AnyEventNowOrWaiting || Current.Game == null || Find.CurrentMap == null)
            {
                if (!WaitForMoreFrames("a playable game before the fixture load"))
                {
                    FailWholeRun("the game never became playable; the fixture was not loaded");
                }

                return;
            }

            state.GameBeforeFixture = Current.Game;
            state.UpdatesWaiting = 0;
            Log.Message("[WotF-Lab] test run loading fixture " + state.Fixture);
            GameDataSaveLoader.LoadGame(state.Fixture);
            stage = Stage.AwaitFixture;
        }

        /// <summary>
        /// Starts tests until one needs a reload or the list ends. A synchronous test runs to
        /// completion inside its start call, so a run of short tests costs one frame each at most.
        /// </summary>
        private static void PumpTests()
        {
            for (int guard = 0; guard < 128; guard++)
            {
                if (state == null || stage != Stage.RunTest)
                {
                    return;
                }

                if (state.Current != null)
                {
                    // A reloading test is between its two halves; Stage.AwaitReload drives it.
                    return;
                }

                if (state.CancelRequested)
                {
                    for (int i = state.Index; i < state.Pending.Count; i++)
                    {
                        state.Outcomes.Add(new TestOutcome
                        {
                            Name = state.Pending[i].Name,
                            Status = "skipped",
                            Message = "run cancelled"
                        });
                    }

                    state.Index = state.Pending.Count;
                    stage = Stage.Report;
                    return;
                }

                if (state.Index >= state.Pending.Count)
                {
                    FinishPass();
                    return;
                }

                StartNextTest();
                if (state.Current != null)
                {
                    return;
                }
            }
        }

        private static void StartNextTest()
        {
            state.Current = state.Pending[state.Index];
            state.UpdatesWaiting = 0;

            if (Find.TickManager == null || Current.Game == null || Find.CurrentMap == null)
            {
                FailCurrentWithoutScene("no game/map is loaded");
                return;
            }

            // Deterministic scenes: pause the clock for the whole run (real time must not advance the
            // game between two steps of the same test) and restore the player's speed afterwards.
            TickManager tickManager = Find.TickManager;
            if (!state.SpeedChanged)
            {
                state.Speed = (int)tickManager.CurTimeSpeed;
                state.SpeedChanged = true;
            }

            tickManager.CurTimeSpeed = TimeSpeed.Paused;

            try
            {
                state.Arena = TestArena.Create(Find.CurrentMap, state.Current.Name);
            }
            catch (Exception exception)
            {
                FailCurrentWithoutScene("arena: " + exception.Message);
                return;
            }

            state.Context = new TestContext(state.Arena, "run", false);
            state.Outcome = new TestOutcome
            {
                Name = state.Current.Name,
                Pass = state.Pass,
                TickStart = Find.TickManager.TicksGame
            };
            state.PhaseStartedMilliseconds = Stopwatch.GetTimestamp();

            Log.Message("[WotF-Lab] test '" + state.Current.Name + "' start tick=" + state.Outcome.TickStart);
            RunBody();
        }

        private static void RunBody()
        {
            TestLogWatch watch = TestLogWatch.Now();
            LabTest instance = CreateTest(state.Current, out string error);
            if (instance == null)
            {
                state.Context.Fail(error);
                RecordWatch(watch);
                FinishCurrent(null);
                return;
            }

            state.Instance = instance;

            try
            {
                instance.Execute(state.Context);
            }
            catch (Exception exception)
            {
                state.Context.Fail("threw " + exception.GetType().Name + ": " + exception.Message);
                Log.Warning("[WotF-Lab] test '" + state.Current.Name + "' threw: " + exception);
            }

            RecordWatch(watch);
            FinishCurrent(null);
        }

        private static void RecordWatch(TestLogWatch watch)
        {
            if (state.Outcome == null || watch == null)
            {
                return;
            }

            watch.Capture();
            state.Outcome.LogErrors += watch.Errors;
            state.Outcome.LogOwnErrors += watch.OwnErrors;
            state.Outcome.LogTruncated |= watch.Truncated;
            for (int i = 0; i < watch.Samples.Count && state.Outcome.ErrorTexts.Count < MaxSamplesInReport; i++)
            {
                state.Outcome.ErrorTexts.Add(watch.Samples[i]);
            }

            if (watch.OwnErrors > 0)
            {
                state.Context.Fail("the game logged " + watch.OwnErrors + " error(s) from this mod during this test");
            }
        }

        /// <summary>Ends one test: disposes its arena and decides between reload and pass/fail.</summary>
        private static void FinishCurrent(string forcedError)
        {
            if (state.Outcome == null || state.Context == null)
            {
                state.Current = null;
                state.Index++;
                return;
            }

            state.Outcome.TickEnd = Find.TickManager != null ? Find.TickManager.TicksGame : state.Outcome.TickStart;
            state.Outcome.ElapsedMilliseconds =
                (long)((Stopwatch.GetTimestamp() - state.PhaseStartedMilliseconds) * 1000.0 / Stopwatch.Frequency);

            if (forcedError != null)
            {
                state.Context.Fail(forcedError);
            }

            if (state.Current.Reload && state.Outcome.Phase == null)
            {
                // A reload only adds information when phase one did everything it meant to do.
                if (!state.Context.Failed && forcedError == null)
                {
                    BeginReloadOfCurrent();
                    return;
                }
            }

            CompleteOutcome();
        }

        private static void BeginReloadOfCurrent()
        {
            // Clean up what phase one created so the save contains only the reload scenario's own
            // scene: the test body decides what must survive, everything else is removed here.
            state.ArenaOrigin = state.Arena.Origin;
            state.ArenaWidth = state.Arena.Width;
            state.ArenaHeight = state.Arena.Height;
            state.Outcome.Phase = "reload";

            string save = "wotf-l2-" + state.RunId;
            state.ReloadSaveName = save;
            try
            {
                GameDataSaveLoader.SaveGame(save);
            }
            catch (Exception exception)
            {
                state.Context.Fail("save failed: " + exception.Message);
                CompleteOutcome();
                return;
            }

            state.GameBeforeReload = Current.Game;
            state.UpdatesWaiting = 0;
            Log.Message("[WotF-Lab] test '" + state.Current.Name + "' reloading save " + save);
            GameDataSaveLoader.LoadGame(save);
            stage = Stage.AwaitReload;
        }

        private static void CompleteReloadedTest()
        {
            Map map = Find.CurrentMap;
            if (state.ArenaOrigin.IsValid && map != null)
            {
                state.Arena = TestArena.Reattach(map, state.ArenaOrigin, state.ArenaWidth, state.ArenaHeight);
            }
            else if (map != null)
            {
                state.Arena = TestArena.Create(map, state.Current.Name);
            }

            if (state.Arena == null)
            {
                state.Context.Fail("no map after reload");
                CompleteOutcome();
                return;
            }

            state.Context = new TestContext(state.Arena, "after-reload", true);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            TestLogWatch watch = TestLogWatch.Now();
            LabTest instance = state.Instance;
            string error = null;
            if (instance == null)
            {
                instance = CreateTest(state.Current, out error);
            }

            if (instance == null)
            {
                state.Context.Fail(error);
            }
            else
            {
                try
                {
                    instance.ExecuteAfterReload(state.Context);
                }
                catch (Exception exception)
                {
                    state.Context.Fail("threw " + exception.GetType().Name + ": " + exception.Message);
                    Log.Warning("[WotF-Lab] test '" + state.Current.Name + "' threw after reload: " + exception);
                }
            }

            RecordWatch(watch);
            stage = Stage.RunTest;
            CompleteOutcome();
        }

        private static void CompleteOutcome()
        {
            try
            {
                state.Arena?.Dispose();
            }
            catch (Exception exception)
            {
                Log.Warning("[WotF-Lab] arena cleanup failed: " + exception);
            }

            TestOutcome outcome = state.Outcome;
            bool failed = state.Context != null && state.Context.Failed;
            outcome.Message = state.Context != null && state.Context.Errors.Count > 0
                ? string.Join(" | ", new List<string>(state.Context.Errors).ToArray())
                : null;
            if (state.Context != null)
            {
                foreach (Measurement measurement in state.Context.Measurements)
                {
                    outcome.Measurements.Add(measurement.Name + "=" + measurement.Value);
                }
            }

            if (state.Current.ExpectedFailure)
            {
                outcome.Status = failed ? "expectedFailure" : "unexpectedPass";
                if (!failed)
                {
                    outcome.Message = "the documented failure no longer reproduces ("
                                      + state.Current.ExpectedFailureReason + "); update the test and its note";
                }
            }
            else
            {
                outcome.Status = failed ? "failed" : "passed";
            }

            state.Outcomes.Add(outcome);
            lastSummary = outcome.Name + " " + outcome.Status
                          + (outcome.Message == null ? string.Empty : ": " + outcome.Message);
            Log.Message("[WotF-Lab] test '" + outcome.Name + "' " + outcome.Status + " ("
                        + outcome.ElapsedMilliseconds + " ms, ticks " + outcome.TickStart + ".." + outcome.TickEnd
                        + ")" + (outcome.Message == null ? string.Empty : " " + outcome.Message));

            state.Current = null;
            state.Instance = null;
            state.Outcome = null;
            state.Context = null;
            state.Arena = null;
            state.ArenaOrigin = IntVec3.Invalid;
            DeleteReloadSave();
            state.Index++;
        }

        /// <summary>The reload test's save is an artifact of the run, not a fixture: remove it.</summary>
        private static void DeleteReloadSave()
        {
            if (state?.ReloadSaveName == null)
            {
                return;
            }

            try
            {
                string path = GenFilePaths.FilePathForSavedGame(state.ReloadSaveName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                Log.Warning("[WotF-Lab] could not delete the reload save: " + exception.Message);
            }

            state.ReloadSaveName = null;
        }

        private static void FailCurrentWithoutScene(string message)
        {
            state.Outcome = new TestOutcome
            {
                Name = state.Current.Name,
                Status = state.Current.ExpectedFailure ? "expectedFailure" : "failed",
                Message = message,
                TickStart = Find.TickManager != null ? Find.TickManager.TicksGame : 0
            };
            state.Outcome.TickEnd = state.Outcome.TickStart;
            state.Outcomes.Add(state.Outcome);
            lastSummary = state.Outcome.Name + " " + state.Outcome.Status + ": " + message;
            Log.Error("[WotF-Lab] test '" + state.Current.Name + "' could not start: " + message);
            state.Outcome = null;
            state.Current = null;
            state.Context = null;
            state.Arena = null;
            state.Index++;
        }

        private static void FailWholeRun(string message)
        {
            Log.Error("[WotF-Lab] test run aborted: " + message);
            for (int i = state.Index; i < state.Pending.Count; i++)
            {
                state.Outcomes.Add(new TestOutcome
                {
                    Name = state.Pending[i].Name,
                    Status = "failed",
                    Message = message
                });
            }

            state.Index = state.Pending.Count;
            stage = Stage.Report;
        }

        private static bool WaitForMoreFrames(string what)
        {
            state.UpdatesWaiting++;
            if (state.UpdatesWaiting % 600 == 0)
            {
                Log.Message("[WotF-Lab] still waiting for " + what + " (" + state.UpdatesWaiting + " frames)");
            }

            return state.UpdatesWaiting < ReadyUpdateTimeout;
        }

        /// <summary>
        /// True once a game that replaces <paramref name="previous"/> is fully playable again. The
        /// long-event flag alone is not enough: it clears a frame before the new map is assigned.
        /// </summary>
        private static bool TryEnterLoadedGame(ref Game previous)
        {
            if (LongEventHandler.AnyEventNowOrWaiting)
            {
                return false;
            }

            if (Current.ProgramState != ProgramState.Playing || Current.Game == null
                || ReferenceEquals(Current.Game, previous))
            {
                return false;
            }

            if (Find.TickManager == null || Find.CurrentMap == null || Find.CurrentMap.mapPawns == null)
            {
                return false;
            }

            previous = Current.Game;
            return true;
        }

        // ------------------------------------------------------------------ report

        private static void WriteReport()
        {
            try
            {
                state.RunLog?.Capture();
                state.LoadLog?.Capture();
                string json = BuildReport();
                File.WriteAllText(ReportPath(state, state.RunId), json, new UTF8Encoding(false));
                File.WriteAllText(ReportPath(state, "latest"), json, new UTF8Encoding(false));
                Log.Message("[WotF-Lab] test run " + state.RunId + " report: " + ReportPath(state, state.RunId));
            }
            catch (Exception exception)
            {
                Log.Error("[WotF-Lab] could not write the test report: " + exception);
            }
        }

        private static string BuildReport()
        {
            var json = new TestJson();
            json.StartObject()
                .Property("run", state.RunId)
                .Property("filter", state.Filter)
                .Property("fixture", state.Fixture ?? string.Empty)
                .Property("fixtureLoadedThisRun", state.FixtureLoaded)
                .Property("repeat", state.Repeats)
                .Property("startedUtc", new DateTime(state.StartedUtcTicks, DateTimeKind.Utc).ToString("o"))
                .Property("elapsedMs", (long)((DateTime.UtcNow.Ticks - state.StartedUtcTicks) / TimeSpan.TicksPerMillisecond))
                .Property("tickStart", state.StartTick)
                .Property("tickEnd", Find.TickManager != null ? Find.TickManager.TicksGame : state.StartTick)
                .Property("game", VersionLabel());

            json.Key("load").StartObject()
                .Property("passed", state.Load != null && state.Load.Passed)
                .Property("defsChecked", state.Load?.DefsChecked ?? 0)
                .Property("logErrorsDuringRun", state.RunLog?.Errors ?? 0)
                .Property("logErrorsDuringLoad", state.LoadLog?.Errors ?? 0)
                .Property("ownErrorsDuringRun", state.RunLog?.OwnErrors ?? 0)
                .Property("ownErrorsDuringLoad", state.LoadLog?.OwnErrors ?? 0)
                .Property("logTruncated", state.RunLog?.Truncated ?? false);
            json.Key("notes").StartArray();
            if (state.Load != null)
            {
                foreach (string note in state.Load.Notes)
                {
                    json.Value(note);
                }
            }

            json.EndArray();
            json.Key("errorSamples").StartArray();
            var samples = new List<string>();
            if (state.LoadLog != null)
            {
                samples.AddRange(state.LoadLog.Samples);
            }

            if (state.RunLog != null)
            {
                samples.AddRange(state.RunLog.Samples);
            }

            var seenSample = new HashSet<string>();
            foreach (string sample in samples)
            {
                if (seenSample.Add(sample))
                {
                    json.Value(sample);
                }
            }

            json.EndArray();
            json.Key("failures").StartArray();
            if (state.Load != null)
            {
                foreach (string failure in state.Load.Failures)
                {
                    json.Value(failure);
                }
            }

            json.EndArray().EndObject();

            int passed = 0;
            int failed = 0;
            int expected = 0;
            foreach (TestOutcome outcome in state.Outcomes)
            {
                if (outcome.Status == "passed")
                {
                    passed++;
                }
                else if (outcome.Status == "expectedFailure")
                {
                    expected++;
                }
                else
                {
                    failed++;
                }
            }

            json.Key("summary").StartObject()
                .Property("total", state.Outcomes.Count)
                .Property("passed", passed)
                .Property("failed", failed)
                .Property("expectedFailure", expected)
                .Property("repeat", state.Repeats)
                .EndObject();

            // Per-test stability across the repeated passes of this same run.
            json.Key("stability").StartArray();
            foreach (string name in StableNames())
            {
                int runs = 0;
                int failures = 0;
                foreach (TestOutcome outcome in state.Outcomes)
                {
                    if (outcome.Name != name)
                    {
                        continue;
                    }

                    runs++;
                    if (outcome.Status != "passed")
                    {
                        failures++;
                    }
                }

                json.StartObject()
                    .Property("name", name)
                    .Property("runs", runs)
                    .Property("failures", failures)
                    .EndObject();
            }

            json.EndArray();

            json.Key("tests").StartArray();
            foreach (TestOutcome outcome in state.Outcomes)
            {
                json.StartObject()
                    .Property("name", outcome.Name)
                    .Property("pass", outcome.Pass)
                    .Property("status", outcome.Status)
                    .Property("message", outcome.Message)
                    .Property("elapsedMs", outcome.ElapsedMilliseconds)
                    .Property("tickStart", outcome.TickStart)
                    .Property("tickEnd", outcome.TickEnd)
                    .Property("logErrors", outcome.LogErrors)
                    .Property("logOwnErrors", outcome.LogOwnErrors)
                    .Property("logTruncated", outcome.LogTruncated);
                json.Key("measurements").StartArray();
                foreach (string measurement in outcome.Measurements)
                {
                    json.Value(measurement);
                }

                json.EndArray();
                json.Key("errors").StartArray();
                foreach (string error in outcome.ErrorTexts)
                {
                    json.Value(error);
                }

                json.EndArray().EndObject();
            }

            json.EndArray().EndObject();
            return json.ToString();
        }

        private static void CompleteRun()
        {
            try
            {
                if (state?.SpeedChanged == true && Find.TickManager != null
                    && Current.ProgramState == ProgramState.Playing)
                {
                    Find.TickManager.CurTimeSpeed = (TimeSpeed)state.Speed;
                }
            }
            catch (Exception exception)
            {
                Log.Warning("[WotF-Lab] could not restore game speed: " + exception.Message);
            }

            state = null;
            stage = Stage.Done;
            lastSummary = "run finished";
        }

        private static List<string> StableNames()
        {
            var names = new List<string>();
            foreach (TestOutcome outcome in state.Outcomes)
            {
                if (!names.Contains(outcome.Name))
                {
                    names.Add(outcome.Name);
                }
            }

            names.Sort(string.CompareOrdinal);
            return names;
        }

        private static string ReportPath(RunState run, string name)
        {
            return Path.Combine(run.Directory, name + ".json");
        }

        private static string VersionLabel()
        {
            try
            {
                return typeof(Root).Assembly.GetName().Version.ToString();
            }
            catch
            {
                return "unknown";
            }
        }

        private static string DefsRoot()
        {
            // <mod>/1.7/Assemblies/WhisperOfTheForest.dll -> <mod>/1.7/Defs
            string assembly = typeof(LabTestRunner).Assembly.Location;
            string assemblies = Path.GetDirectoryName(assembly);
            string version = Path.GetDirectoryName(assemblies);
            return version == null ? null : Path.Combine(version, "Defs");
        }

        private static string Escape(string value)
        {
            return value?.Replace("\\", "\\\\") ?? string.Empty;
        }

        private static string Str(string value)
        {
            return Json.Str(value);
        }

        // ------------------------------------------------------------------ discovery

        private static List<Entry> Discover(string filter)
        {
            var entries = new List<Entry>();
            string[] parts = string.IsNullOrWhiteSpace(filter)
                ? new string[0]
                : filter.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (Type type in typeof(LabTest).Assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(LabTest).IsAssignableFrom(type)
                    || type.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                var attribute = type.GetCustomAttribute<LabTestAttribute>();
                LabTest instance;
                try
                {
                    instance = (LabTest)Activator.CreateInstance(type);
                }
                catch (Exception exception)
                {
                    Log.Error("[WotF-Lab] test " + type.Name + " cannot be created: " + exception.Message);
                    continue;
                }

                string name = instance.Name;
                if (string.IsNullOrEmpty(name))
                {
                    Log.Error("[WotF-Lab] test " + type.Name + " has no name");
                    continue;
                }

                if (!Matches(name, type.Name, parts))
                {
                    continue;
                }

                entries.Add(new Entry
                {
                    Name = name,
                    Type = type,
                    ExpectedFailure = attribute != null && attribute.ExpectedFailure,
                    ExpectedFailureReason = attribute?.ExpectedFailureReason,
                    Reload = attribute != null && attribute.Reload
                });
            }

            entries.Sort((a, b) => string.CompareOrdinal(a.Type.FullName, b.Type.FullName));
            return entries;
        }

        private static bool Matches(string name, string typeName, string[] parts)
        {
            if (parts.Length == 0)
            {
                return true;
            }

            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                if (name.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase)
                    || typeName.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static LabTest CreateTest(Entry entry, out string error)
        {
            try
            {
                error = null;
                return (LabTest)Activator.CreateInstance(entry.Type);
            }
            catch (Exception exception)
            {
                error = "cannot create " + entry.Type.Name + ": " + exception.Message;
                return null;
            }
        }
    }
}
