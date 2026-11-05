using System;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Snapshots the global IMGUI text state and restores it on dispose.
    ///
    /// Verse.Text.StartOfOnGUI() asserts that WordWrap is true and Anchor is UpperLeft at the start
    /// of every frame ("Word wrap was false at end of frame." / "Alignment was ... at end of frame."),
    /// and Font is reset to Small. Any UI drawing entry therefore has to save and restore whatever it
    /// changes instead of hardcoding a default; GUI.color is restored for the same reason.
    /// </summary>
    internal readonly struct TechLabDrawScope : IDisposable
    {
        private readonly GameFont font;
        private readonly TextAnchor anchor;
        private readonly bool wordWrap;
        private readonly Color color;

        private TechLabDrawScope(GameFont font, TextAnchor anchor, bool wordWrap, Color color)
        {
            this.font = font;
            this.anchor = anchor;
            this.wordWrap = wordWrap;
            this.color = color;
        }

        public static TechLabDrawScope Begin()
        {
            return new TechLabDrawScope(Text.Font, Text.Anchor, Text.WordWrap, GUI.color);
        }

        public void Dispose()
        {
            Text.Font = font;
            Text.Anchor = anchor;
            Text.WordWrap = wordWrap;
            GUI.color = color;
        }
    }
}
