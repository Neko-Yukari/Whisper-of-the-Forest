using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Experimental
{
    /// <summary>The single Mod entry point for the experimental lab UI.</summary>
    public class TechLabMod : Mod
    {
        private static TechLabMod instance;

        private TechLabSettings settings;

        public TechLabMod(ModContentPack content) : base(content)
        {
            instance = this;
            settings = GetSettings<TechLabSettings>();
        }

        public static TechLabSettings Settings => instance == null ? null : instance.settings;

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

            using (TechLabDrawScope.Begin())
            {
                Text.Font = GameFont.Small;
                bool show = settings.showFloatingEntry;
                Widgets.CheckboxLabeled(new Rect(inRect.x, inRect.y, inRect.width, 24f),
                    Translator.Translate("WotF_UI.ShowFloatingEntry"), ref show);
                if (show != settings.showFloatingEntry)
                {
                    settings.showFloatingEntry = show;
                    settings.Write();
                }

                Text.Font = GameFont.Tiny;
                Text.WordWrap = true;
                GUI.color = TechLabStyle.Hint;
                Widgets.Label(new Rect(inRect.x, inRect.y + 28f, inRect.width, 40f),
                    Translator.Translate("WotF_UI.SettingsHint"));
            }
        }
    }
}
