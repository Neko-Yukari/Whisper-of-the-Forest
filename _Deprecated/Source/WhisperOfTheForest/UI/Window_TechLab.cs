using System;
using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Experimental.Spells;

namespace WhisperOfTheForest.Experimental
{
    /// <summary>
    /// Compact lab bench window: shows the selected pawn, grants the missile psycast,
    /// equips/removes the two movement experiment items and controls the floating entry.
    /// It never pauses the game, never blocks camera dragging outside its own rect and is
    /// intentionally not restored when a game is loaded.
    /// </summary>
    public class Window_TechLab : Window
    {
        private string statusText;
        private bool statusIsError;

        public Window_TechLab()
        {
            optionalTitle = TechLabUi.T("WotF_UI.WindowTitle");
            doCloseX = true;
            doCloseButton = false;
            draggable = true;
            forcePause = false;
            preventCameraMotion = false;
            absorbInputAroundWindow = false;
            onlyOneOfTypeAllowed = true;
            closeOnAccept = false;
            closeOnCancel = true;
            drawShadow = true;
        }

        public override Vector2 InitialSize => new Vector2(520f, 536f);

        protected override void SetInitialSizeAndPosition()
        {
            Vector2 size = InitialSize;
            float x = Mathf.Clamp((UI.screenWidth - size.x) * 0.5f + 60f, 0f, Mathf.Max(0f, UI.screenWidth - size.x));
            float y = Mathf.Clamp((UI.screenHeight - size.y) * 0.5f - 30f, 0f, Mathf.Max(0f, UI.screenHeight - size.y));
            windowRect = GenUI.Rounded(new Rect(x, y, size.x, size.y));
        }

        public override void DoWindowContents(Rect inRect)
        {
            // All widget state changes are local to this scope: Verse.Text.StartOfOnGUI()
            // errors out if WordWrap/Anchor leak into the next frame.
            using (TechLabDrawScope.Begin())
            {
                TechLabSettings settings = TechLabMod.Settings;
                Pawn pawn = TechLabUi.SelectedPawn();
                float width = inRect.width;
                float y = 0f;

                DrawPawnCard(new Rect(0f, y, width, 58f), pawn);
                y += 62f;

                DrawSectionHeader(new Rect(0f, y, width, 18f), "WotF_UI.SectionPsycast");
                y += 22f;

                ActionButton(new Rect(0f, y, width, 32f), "WotF_UI.GrantPsycast", "WotF_UI.GrantPsycastTip", pawn, LabActions.GrantPsycast);
                y += 36f;

                DrawHint(new Rect(0f, y, width, 30f), "WotF_UI.PsycastHint");
                y += 32f;

                DrawSectionHeader(new Rect(0f, y, width, 18f), "WotF_UI.SectionMovement");
                y += 22f;

                float half = (width - 8f) * 0.5f;
                ActionButton(new Rect(0f, y, half, 32f), "WotF_UI.EquipTerrainStride", "WotF_UI.EquipTerrainStrideTip", pawn,
                    LabActions.EquipTerrainStride);
                ActionButton(new Rect(half + 8f, y, half, 32f), "WotF_UI.EquipBlinkStride", "WotF_UI.EquipBlinkStrideTip", pawn,
                    LabActions.EquipBlinkStride);
                y += 36f;

                ActionButton(new Rect(0f, y, width, 32f), "WotF_UI.RemoveApparel", "WotF_UI.RemoveApparelTip", pawn,
                    LabActions.RemoveExperimentApparel);
                y += 36f;

                DrawHint(new Rect(0f, y, width, 30f), "WotF_UI.MovementHint");
                y += 32f;

                DrawSectionHeader(new Rect(0f, y, width, 18f), "WotF_UI.SectionEntry");
                y += 24f;

                if (settings != null)
                {
                    bool show = settings.showFloatingEntry;
                    Widgets.CheckboxLabeled(new Rect(0f, y, width - 126f, 24f),
                        TechLabUi.T("WotF_UI.ShowFloatingEntry"), ref show);
                    if (show != settings.showFloatingEntry)
                    {
                        settings.showFloatingEntry = show;
                        settings.Write();
                    }

                    Rect resetRect = new Rect(width - 118f, y, 118f, 24f);
                    if (Widgets.ButtonText(resetRect, TechLabUi.T("WotF_UI.ResetEntryPosition"), true, true,
                            settings.hasCustomEntryPosition, null))
                    {
                        TechLabUi.ResetEntryPosition(settings);
                    }

                    TooltipHandler.TipRegion(resetRect, TechLabUi.T("WotF_UI.ResetEntryPositionTip"));
                }

                y += 28f;
                DrawHint(new Rect(0f, y, width, 34f), "WotF_UI.EntryHint");
                y += 36f;

                DrawSectionHeader(new Rect(0f, y, width, 18f), "WotF_Spellbook.LabSection");
                y += 22f;

                // Entry point for the new magic book. Only the button lives here; the window
                // itself is a separate class so this file keeps its former responsibilities.
                Rect spellbookRect = new Rect(0f, y, width, 32f);
                bool canOpenSpellbook = pawn != null;
                if (Widgets.ButtonText(spellbookRect, TechLabUi.T("WotF_Spellbook.LabOpen"), true, true, canOpenSpellbook, null)
                    && canOpenSpellbook)
                {
                    // Shared entry point: the book itself refuses a second instance.
                    SpellService.OpenSpellbook(pawn);
                    statusText = TechLabUi.TF("WotF_Spellbook.LabOpened", pawn.LabelShort);
                    statusIsError = false;
                }

                TooltipHandler.TipRegion(spellbookRect, canOpenSpellbook
                    ? TechLabUi.T("WotF_Spellbook.LabOpenTip")
                    : TechLabUi.T("WotF_UI.NeedPawnTip"));
                y += 36f;

                DrawStatus(inRect, y);
            }
        }

        private void ActionButton(Rect rect, string labelKey, string tipKey, Pawn pawn, Action<Pawn> action)
        {
            string label = TechLabUi.T(labelKey);
            bool clicked = Widgets.ButtonText(rect, label, true, true, pawn != null, null);
            TooltipHandler.TipRegion(rect, TechLabUi.T(pawn != null ? tipKey : "WotF_UI.NeedPawnTip"));

            if (clicked && pawn != null)
            {
                RunAction(label, pawn, action);
            }
        }

        private void RunAction(string label, Pawn pawn, Action<Pawn> action)
        {
            try
            {
                action(pawn);
                statusText = TechLabUi.TF("WotF_UI.ActionDone", label, pawn.LabelShort);
                statusIsError = false;
            }
            catch (Exception exception)
            {
                WotfLog.Error(WotfLog.ModuleUI, label + " on " + pawn.LabelShort + " failed: " + exception);
                statusText = TechLabUi.TF("WotF_UI.ActionFailed", label, exception.Message);
                statusIsError = true;
            }
        }

        private static void DrawPawnCard(Rect rect, Pawn pawn)
        {
            Widgets.DrawBoxSolid(rect, TechLabStyle.CardFill);
            Widgets.DrawBoxSolidWithOutline(rect, new Color(0f, 0f, 0f, 0f), TechLabStyle.AccentDim, 1);

            Text.Font = GameFont.Small;
            GUI.color = pawn != null ? Color.white : TechLabStyle.Hint;
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 5f, rect.width - 16f, 22f),
                pawn != null ? pawn.LabelShort : TechLabUi.T("WotF_UI.NoPawn"));
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            Text.WordWrap = true;
            GUI.color = TechLabStyle.Hint;
            string raceLabel = pawn == null ? null : (pawn.def.label ?? pawn.def.defName);
            string sub = pawn != null
                ? TechLabUi.TF("WotF_UI.PawnInfo", pawn.KindLabel, raceLabel)
                : TechLabUi.T("WotF_UI.NoPawnHint");
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 27f, rect.width - 16f, 28f), sub);
            // Font/WordWrap/colour are restored by the enclosing TechLabDrawScope.
            GUI.color = Color.white;
        }

        private static void DrawSectionHeader(Rect rect, string labelKey)
        {
            Text.Font = GameFont.Small;
            GUI.color = TechLabStyle.Accent;
            Widgets.Label(rect, TechLabUi.T(labelKey));
            GUI.color = Color.white;
            Widgets.DrawLineHorizontal(rect.x, rect.yMax + 1f, rect.width, TechLabStyle.AccentDim);
        }

        private static void DrawHint(Rect rect, string labelKey)
        {
            Text.Font = GameFont.Tiny;
            Text.WordWrap = true;
            GUI.color = TechLabStyle.Hint;
            Widgets.Label(rect, TechLabUi.T(labelKey));
            GUI.color = Color.white;
        }

        private void DrawStatus(Rect inRect, float hintBottom)
        {
            float top = Mathf.Max(hintBottom + 4f, inRect.height - 30f);
            Widgets.DrawLineHorizontal(0f, top - 4f, inRect.width, TechLabStyle.AccentDim);

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = statusIsError ? TechLabStyle.ErrorText : TechLabStyle.Hint;
            Widgets.LabelEllipses(new Rect(0f, top, inRect.width, 22f),
                statusText ?? TechLabUi.T("WotF_UI.StatusReady"));
            GUI.color = Color.white;
        }
    }
}
