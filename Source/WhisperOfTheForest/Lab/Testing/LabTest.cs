using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Verse;

namespace WhisperOfTheForest.Experimental.Testing
{
    /// <summary>
    /// Marks a lab test class. Discovery is by type, so adding a test means adding one class in
    /// <c>LabTests/</c> - no registration list to keep in sync.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class LabTestAttribute : Attribute
    {
        /// <summary>
        /// The test is known to fail on the current build and the failure is itself the result
        /// being tracked. It is reported as "expectedFailure" and does not fail the run, unless it
        /// starts passing (that is an "unexpectedPass" and does fail the run, because the note below
        /// became stale).
        /// </summary>
        public bool ExpectedFailure { get; set; }

        /// <summary>Why the test is expected to fail; stored in the report.</summary>
        public string ExpectedFailureReason { get; set; }

        /// <summary>
        /// The test needs a real save/load in the middle: the runner saves, loads the same save and
        /// then calls <see cref="LabTest.AfterReload"/>. Only these tests pay the reload cost.
        /// </summary>
        public bool Reload { get; set; }
    }

    /// <summary>
    /// One scenario test. A test builds its own scene inside the arena it is given, acts through
    /// public product APIs and asserts observable results only (hit points, injuries, positions,
    /// defs). It must not depend on another test having run first.
    /// </summary>
    public abstract class LabTest
    {
        /// <summary>Short, unique, prefixed name; the filter matches it as a prefix.</summary>
        public abstract string Name { get; }

        /// <summary>
        /// Optional: places the scene and acts. Every test gets a fresh arena here; throw or report
        /// through <see cref="TestContext"/> to fail.
        /// </summary>
        protected virtual void Run(TestContext context)
        {
        }

        /// <summary>Only for <see cref="LabTestAttribute.Reload"/> tests: runs after the reload.</summary>
        protected virtual void AfterReload(TestContext context)
        {
        }

        /// <summary>Runner entry for a normal test.</summary>
        internal void Execute(TestContext context)
        {
            Run(context);
        }

        /// <summary>Runner entry for the first half of a reloading test.</summary>
        internal void ExecuteFirstPhase(TestContext context)
        {
            Run(context);
        }

        /// <summary>Runner entry for the second half of a reloading test.</summary>
        internal void ExecuteAfterReload(TestContext context)
        {
            AfterReload(context);
        }
    }

    /// <summary>Outcome of one test, as written to the report file.</summary>
    internal sealed class TestOutcome
    {
        internal string Name;
        internal int Pass = 1;
        internal string Status;
        internal string Message;
        internal long ElapsedMilliseconds;
        internal long TickStart;
        internal long TickEnd;
        internal int LogErrors;
        internal int LogOwnErrors;
        internal bool LogTruncated;
        internal string Phase;
        internal readonly List<string> Measurements = new List<string>();
        internal readonly List<string> ErrorTexts = new List<string>();

        internal bool Failed => Status == "failed" || Status == "unexpectedPass";
    }

    /// <summary>
    /// A named number recorded by a test. Measurements are free-form on purpose: a test that fails
    /// can be judged from the report without re-running the game.
    /// </summary>
    internal readonly struct Measurement
    {
        internal readonly string Name;
        internal readonly string Value;

        internal Measurement(string name, string value)
        {
            Name = name;
            Value = value;
        }

        internal static Measurement Of(string name, bool value)
        {
            return new Measurement(name, value ? "true" : "false");
        }

        internal static Measurement Of(string name, int value)
        {
            return new Measurement(name, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        internal static Measurement Of(string name, long value)
        {
            return new Measurement(name, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        internal static Measurement Of(string name, float value)
        {
            return new Measurement(name, value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
