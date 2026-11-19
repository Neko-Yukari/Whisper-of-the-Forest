using Verse;

namespace WhisperOfTheForest
{
    /// <summary>
    /// The one logging entry point of this mod. Every line carries a module tag, and the Debug tier is
    /// off unless the player turns it on in the mod settings.
    ///
    /// Error and Warning go to <see cref="Log"/>'s own error/warning channels; Info and Debug are
    /// ordinary log messages. A module never formats its own prefix.
    /// </summary>
    public static class WotfLog
    {
        public const string ModuleSpell = "Spell";
        public const string ModuleVfx = "Vfx";
        public const string ModuleMissile = "Missile";
        public const string ModuleMovement = "Movement";
        public const string ModuleRitual = "Ritual";
        public const string ModuleShield = "Shield";
        public const string ModuleRuin = "Ruin";
        public const string ModuleTeleport = "Teleport";
        public const string ModuleCourt = "Court";
        public const string ModuleUI = "UI";
        public const string ModuleBoot = "Boot";

        /// <summary>
        /// Per-object detail. Off by default: a normal session logs no Debug line. Before the Mod
        /// instance exists there are no settings yet, and Debug stays off.
        /// </summary>
        public static bool DebugEnabled
        {
            get
            {
                WotfSettings settings = WotfMod.Settings;
                return settings != null && settings.logDebug;
            }
        }

        /// <summary>The mod itself is broken.</summary>
        public static void Error(string module, string message)
        {
            Log.Error(Prefix(module) + message);
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
