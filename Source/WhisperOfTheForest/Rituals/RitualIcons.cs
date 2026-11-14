using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// The three altar button icons. Loaded in the static constructor so the asset is read on the main
    /// thread (StaticConstructorOnStartupUtility.cs:44-51); ContentFinder reports a missing file at
    /// startup and returns the pink error texture, so a missing placeholder shows up immediately
    /// instead of silently drawing nothing.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class RitualIcons
    {
        public static readonly Texture2D Codex = ContentFinder<Texture2D>.Get("UI/Ritual/Codex", true);
        public static readonly Texture2D Host = ContentFinder<Texture2D>.Get("UI/Ritual/Host", true);
        public static readonly Texture2D Auto = ContentFinder<Texture2D>.Get("UI/Ritual/Auto", true);

        static RitualIcons()
        {
        }
    }
}
