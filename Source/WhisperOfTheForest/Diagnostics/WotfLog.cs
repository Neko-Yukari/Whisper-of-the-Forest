using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Experimental
{
    /// <summary>
    /// The one logging entry point of this mod. Every line carries a module tag, and the Debug tier is
    /// off unless the player turns it on in the mod settings or the lab sets it for a session.
    ///
    /// Error and Warning go to <see cref="Log"/>'s own error/warning channels so the L1 load check can
    /// count them; Info and Debug are ordinary log messages. A module never formats its own prefix.
    /// </summary>
    public static class WotfLog
    {
        public const string ModuleSpell = "Spell";
        public const string ModuleVfx = "Vfx";
        public const string ModuleMissile = "Missile";
        public const string ModuleMovement = "Movement";
        public const string ModuleUI = "UI";
        public const string ModuleBoot = "Boot";

        private static readonly HashSet<string> reported = new HashSet<string>();

        // Used before the Mod instance exists (or in a game that never built one).
        private static bool debugBeforeSettings;

        /// <summary>Per-object detail. Off by default: a normal session logs no Debug line.</summary>
        public static bool DebugEnabled
        {
            get
            {
                WotfSettings settings = WotfMod.Settings;
                return settings != null ? settings.logDebug : debugBeforeSettings;
            }
            set
            {
                debugBeforeSettings = value;
                WotfSettings settings = WotfMod.Settings;
                if (settings == null)
                {
                    return;
                }

                settings.logDebug = value;
                settings.Write();
            }
        }

        public static string LevelName => DebugEnabled ? "debug" : "info";

        /// <summary>The mod itself is broken; the L1 check requires zero of these.</summary>
        public static void Error(string module, string message)
        {
            Log.Error(Prefix(module) + message);
        }

        /// <summary>Error for a condition that would otherwise repeat every tick or every cast.</summary>
        public static void ErrorOnce(string module, string message)
        {
            if (reported.Add(module + "|" + message))
            {
                Log.Error(Prefix(module) + message);
            }
        }

        /// <summary>Recoverable trouble: the feature keeps running with less than it asked for.</summary>
        public static void Warning(string module, string message)
        {
            Log.Warning(Prefix(module) + message);
        }

        /// <summary>One summary line per key action, with the counts and the outcome.</summary>
        public static void Info(string module, string message)
        {
            Log.Message(Prefix(module) + message);
        }

        /// <summary>Per-object detail, written only while <see cref="DebugEnabled"/> is on.</summary>
        public static void Debug(string module, string message)
        {
            if (DebugEnabled)
            {
                Log.Message(Prefix(module) + message);
            }
        }

        private static string Prefix(string module)
        {
            return "[WotF/" + module + "] ";
        }
    }
}
