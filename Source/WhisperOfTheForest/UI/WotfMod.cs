using UnityEngine;
using Verse;

namespace WhisperOfTheForest
{
    /// <summary>The single Mod entry point: it owns the settings object and its page.</summary>
    public class WotfMod : Mod
    {
        private static WotfMod instance;

        private WotfSettings settings;

        public WotfMod(ModContentPack content) : base(content)
        {
            instance = this;
            settings = GetSettings<WotfSettings>();
        }

        public static WotfSettings Settings => instance == null ? null : instance.settings;

        public override string SettingsCategory()
        {
            return Translator.Translate("WotF_UI.SettingsCategory");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (settings == null)
            {
                return;
            }

            Text.Font = GameFont.Small;
            bool debug = settings.logDebug;
            Widgets.CheckboxLabeled(new Rect(inRect.x, inRect.y, inRect.width, 24f),
                Translator.Translate("WotF_UI.DebugLog"), ref debug);
            if (debug != settings.logDebug)
            {
                settings.logDebug = debug;
                settings.Write();
            }

            Text.Font = GameFont.Tiny;
            Text.WordWrap = true;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(inRect.x, inRect.y + 30f, inRect.width, 40f),
                Translator.Translate("WotF_UI.SettingsHint"));
        }
    }
}
