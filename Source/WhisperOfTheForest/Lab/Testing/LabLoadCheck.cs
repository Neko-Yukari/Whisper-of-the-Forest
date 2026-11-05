using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;
using Verse;

namespace WhisperOfTheForest.Experimental.Testing
{
    /// <summary>
    /// Reads the game's own log queue (the disk Player.log can stay empty under -quicktest) and
    /// counts what appeared since a snapshot. The queue keeps the last 1000 entries and merges
    /// consecutive duplicates into a repeat counter, so a snapshot also stores the anchor entry's
    /// repeat count and the count is reported as truncated when the anchor was already evicted.
    /// </summary>
    internal sealed class TestLogWatch
    {
        private LogMessage anchor;
        private int anchorRepeats;
        private int queueCount;

        internal int Errors { get; private set; }

        /// <summary>Errors that name this mod; the L1 gate is zero of these.</summary>
        internal int OwnErrors { get; private set; }

        internal bool Truncated { get; private set; }

        internal List<string> Samples { get; } = new List<string>();

        internal static TestLogWatch Now()
        {
            var watch = new TestLogWatch();
            var messages = new List<LogMessage>(Log.Messages);
            watch.queueCount = messages.Count;
            if (messages.Count > 0)
            {
                watch.anchor = messages[messages.Count - 1];
                watch.anchorRepeats = watch.anchor.repeats;
            }

            return watch;
        }

        /// <summary>Errors (and exception text logged as errors) added after the snapshot.</summary>
        internal void Capture()
        {
            int errors = 0;
            int ownErrors = 0;
            bool sawAnchor = anchor == null;
            Samples.Clear();
            foreach (LogMessage message in Log.Messages)
            {
                if (!sawAnchor)
                {
                    if (ReferenceEquals(message, anchor))
                    {
                        sawAnchor = true;
                        int repeated = Math.Max(0, message.repeats - anchorRepeats);
                        errors += repeated;
                        if (message.type == LogMessageType.Error)
                        {
                            if (IsOwn(message.text))
                            {
                                ownErrors += repeated;
                            }

                            if (Samples.Count < 8)
                            {
                                Samples.Add(Sample(message.text));
                            }
                        }
                    }

                    continue;
                }

                if (message.type != LogMessageType.Error)
                {
                    continue;
                }

                errors += message.repeats;
                if (IsOwn(message.text))
                {
                    ownErrors += message.repeats;
                }

                if (Samples.Count < 8)
                {
                    Samples.Add(Sample(message.text));
                }
            }

            Errors = errors;
            OwnErrors = ownErrors;
            Truncated = !sawAnchor;

            // A rare but real failure case: the queue wrapped past the anchor, so the exact number is
            // unknown. Every remaining error is still counted and the report says so.
            if (Truncated)
            {
                int now = 0;
                int ownNow = 0;
                foreach (LogMessage message in Log.Messages)
                {
                    if (message.type != LogMessageType.Error)
                    {
                        continue;
                    }

                    now += message.repeats;
                    if (IsOwn(message.text))
                    {
                        ownNow += message.repeats;
                    }
                }

                Errors = Math.Max(Errors, now);
                OwnErrors = Math.Max(OwnErrors, ownNow);
            }
        }

        /// <summary>True when a log line names this mod, by tag, def or assembly name.</summary>
        internal static bool IsOwn(string text)
        {
            return text != null
                   && (text.Contains("WotF") || text.Contains("WhisperOfTheForest"));
        }

        private static string Sample(string text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            // Keep enough of the message to name the frames that matter, without the whole trace.
            string[] lines = text.Split('\n');
            int count = Math.Min(lines.Length, 12);
            var builder = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    builder.Append(" | ");
                }

                builder.Append(lines[i].Trim());
            }

            return builder.Length > 1200 ? builder.ToString(0, 1200) : builder.ToString();
        }
    }

    /// <summary>
    /// L1 checks that need the real game: every Def declared by this mod's XML really is in the
    /// database, every Harmony patch class of this assembly is applied under our Harmony id, and the
    /// log queue carries no error from this assembly. This replaces the source-text checks that used
    /// to stand in for loading.
    /// </summary>
    internal static class LabLoadCheck
    {
        internal sealed class Result
        {
            internal readonly List<string> Failures = new List<string>();
            internal readonly List<string> Notes = new List<string>();
            internal int DefsChecked;

            internal bool Passed => Failures.Count == 0;
        }

        internal static Result Run(string defsRoot, string assemblyName)
        {
            var result = new Result();
            CheckDefs(defsRoot, result);
            CheckHarmony(assemblyName, result);
            return result;
        }

        private static void CheckDefs(string defsRoot, Result result)
        {
            if (string.IsNullOrEmpty(defsRoot) || !System.IO.Directory.Exists(defsRoot))
            {
                result.Failures.Add("defs root not found: " + defsRoot);
                return;
            }

            var missing = new List<string>();
            var badXml = new List<string>();
            foreach (string file in System.IO.Directory.GetFiles(defsRoot, "*.xml", System.IO.SearchOption.AllDirectories))
            {
                XDocument document;
                try
                {
                    document = XDocument.Load(file);
                }
                catch (Exception exception)
                {
                    badXml.Add(System.IO.Path.GetFileName(file) + ": " + exception.Message);
                    continue;
                }

                if (document.Root == null || document.Root.Name.LocalName != "Defs")
                {
                    continue;
                }

                foreach (XElement element in document.Root.Elements())
                {
                    string defName = (string)element.Element("defName");
                    if (string.IsNullOrEmpty(defName))
                    {
                        continue;
                    }

                    string typeName = element.Name.LocalName;
                    Type type = GenTypes.GetTypeInAnyAssembly(typeName);
                    if (type == null || !typeof(Def).IsAssignableFrom(type))
                    {
                        missing.Add(defName + " (" + System.IO.Path.GetFileName(file) + ", unknown type " + typeName + ")");
                        continue;
                    }

                    Def def = GenDefDatabase.GetDefSilentFail(type, defName, false);
                    if (def == null)
                    {
                        missing.Add(defName + " (" + typeName + ")");
                        continue;
                    }

                    result.DefsChecked++;
                }
            }

            foreach (string error in badXml)
            {
                result.Failures.Add("XML does not parse: " + error);
            }

            if (missing.Count > 0)
            {
                result.Failures.Add("Defs declared but not loaded: " + string.Join("; ", missing.ToArray()));
            }

            result.Notes.Add("defs loaded=" + result.DefsChecked);
        }

        private static void CheckHarmony(string assemblyName, Result result)
        {
            Assembly assembly = typeof(LabLoadCheck).Assembly;
            int checkedCount = 0;
            var missing = new List<string>();

            foreach (Type type in assembly.GetTypes())
            {
                var attribute = type.GetCustomAttribute<HarmonyPatch>();
                if (attribute == null)
                {
                    continue;
                }

                MethodBase target = ResolveTarget(attribute);

                if (target == null)
                {
                    missing.Add(type.Name + " -> " + attribute.info.declaringType?.Name + "." + attribute.info.methodName
                                + " (target method not found)");
                    continue;
                }

                checkedCount++;
                Patches applied = Harmony.GetPatchInfo(target);
                bool found = false;
                if (applied != null)
                {
                    found = HasOwner(applied.Prefixes, assemblyName)
                            || HasOwner(applied.Postfixes, assemblyName)
                            || HasOwner(applied.Transpilers, assemblyName)
                            || HasOwner(applied.Finalizers, assemblyName);
                }

                if (!found)
                {
                    missing.Add(type.Name + " -> " + target.DeclaringType?.Name + "." + target.Name + " (not applied)");
                }
            }

            if (missing.Count > 0)
            {
                result.Failures.Add("Harmony patches not applied: " + string.Join("; ", missing.ToArray()));
            }

            result.Notes.Add("harmony patches applied=" + checkedCount);
        }

        private static MethodBase ResolveTarget(HarmonyPatch attribute)
        {
            HarmonyMethod info = attribute.info;
            if (info == null)
            {
                return null;
            }

            if (info.method != null)
            {
                return info.method;
            }

            if (info.declaringType == null || string.IsNullOrEmpty(info.methodName))
            {
                return null;
            }

            return info.argumentTypes != null && info.argumentTypes.Length > 0
                ? AccessTools.Method(info.declaringType, info.methodName, info.argumentTypes)
                : AccessTools.Method(info.declaringType, info.methodName);
        }

        private static bool HasOwner(IEnumerable<Patch> patches, string assemblyName)
        {
            foreach (Patch patch in patches)
            {
                if (patch.owner == Bootstrap.HarmonyId)
                {
                    return true;
                }

                // Patches applied by another mod to the same method must not count as ours.
                if (patch.PatchMethod != null && patch.PatchMethod.DeclaringType?.Assembly.GetName().Name == assemblyName)
                {
                    return true;
                }
            }

            return false;
        }

        internal static string Describe(Result result)
        {
            var builder = new StringBuilder();
            foreach (string note in result.Notes)
            {
                builder.Append(note).Append(';');
            }

            foreach (string failure in result.Failures)
            {
                builder.Append(" FAIL: ").Append(failure);
            }

            return builder.ToString();
        }
    }
}
