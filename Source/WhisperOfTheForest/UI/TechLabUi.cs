using RimWorld;
using Verse;

namespace WhisperOfTheForest.Experimental
{
    /// <summary>Entry points shared by the floating entry and the native play settings icon.</summary>
    internal static class TechLabUi
    {
        /// <summary>Bumped when the floating entry must recompute its default position.</summary>
        public static int EntryLayoutVersion { get; private set; }

        public static string T(string key)
        {
            return Translator.Translate(key);
        }

        public static string TF(string key, params object[] args)
        {
            return string.Format(Translator.Translate(key), args);
        }

        public static void ResetEntryPosition(TechLabSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.hasCustomEntryPosition = false;
            settings.entryX = -1f;
            settings.entryY = -1f;
            settings.Write();
            EntryLayoutVersion++;
        }

        public static void ToggleWindow()
        {
            WindowStack stack = Find.WindowStack;
            if (stack == null)
            {
                return;
            }

            Window_TechLab open = stack.WindowOfType<Window_TechLab>();
            if (open != null)
            {
                open.Close(true);
                return;
            }

            stack.Add(new Window_TechLab());
        }

        /// <summary>Opens the lab window without toggling it closed.</summary>
        public static void OpenWindow()
        {
            WindowStack stack = Find.WindowStack;
            if (stack == null || stack.WindowOfType<Window_TechLab>() != null)
            {
                return;
            }

            stack.Add(new Window_TechLab());
        }

        /// <summary>
        /// Native play settings row entry: always opens the window and un-hides the floating entry,
        /// so a hidden floating entry can always be recovered from the vanilla control strip.
        /// </summary>
        public static void OpenWindowAndRestoreEntry()
        {
            TechLabSettings settings = TechLabMod.Settings;
            if (settings != null && !settings.showFloatingEntry)
            {
                settings.showFloatingEntry = true;
                settings.Write();
            }

            OpenWindow();
        }

        public static Pawn SelectedPawn()
        {
            Selector selector = Find.Selector;
            if (selector == null)
            {
                return null;
            }

            if (selector.SingleSelectedThing is Pawn single)
            {
                return single;
            }

            var pawns = selector.SelectedPawns;
            if (pawns != null && pawns.Count > 0)
            {
                return pawns[0];
            }

            return null;
        }
    }
}
