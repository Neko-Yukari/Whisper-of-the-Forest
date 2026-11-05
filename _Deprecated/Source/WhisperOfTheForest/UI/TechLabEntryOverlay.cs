using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace WhisperOfTheForest.Experimental
{
    /// <summary>
    /// Draws the draggable floating entry that sits next to the vanilla play settings row.
    ///
    /// Rules implemented here:
    /// - click opens/toggles the lab window, drag only moves the entry (no click on release);
    /// - position and visibility are persisted in the mod settings, the window itself is not;
    /// - the entry is clamped into the screen every frame, so resolution changes cannot strand it;
    /// - the small corner button hides the entry; the native play settings row icon
    ///   (Patch_PlaySettingsRowEntry) always opens the window and restores the entry.
    /// </summary>
    public class TechLabEntryOverlay : GameComponent
    {
        private Vector2 cachedPosition;
        private bool hasCachedPosition;
        private bool cachedFromProbe;
        private int cachedScreenWidth = -1;
        private int cachedScreenHeight = -1;
        private int cachedLayoutVersion = -1;

        private bool pressActive;
        private bool pressOnHideButton;
        private bool dragMoved;
        private Vector2 pressMousePosition;
        private Vector2 pressEntryPosition;

        /// <summary>
        /// Verse.Game.FillComponents activates every GameComponent subclass with
        /// <c>Activator.CreateInstance(type, new object[] { game })</c>, so a (Game) ctor is mandatory.
        /// </summary>
        public TechLabEntryOverlay(Game game)
        {
        }

        public override void GameComponentOnGUI()
        {
            TechLabSettings settings = TechLabMod.Settings;
            if (settings == null || !settings.showFloatingEntry)
            {
                ResetPress();
                return;
            }

            if (Current.ProgramState != ProgramState.Playing)
            {
                return;
            }

            if (Find.CurrentMap == null || !WorldRendererUtility.DrawingMap)
            {
                return;
            }

            if (Find.WindowStack == null)
            {
                return;
            }

            Event currentEvent = Event.current;
            if (currentEvent == null)
            {
                return;
            }

            Rect entryRect = CurrentEntryRect(settings);
            bool mouseOverEntry = Mouse.IsOver(entryRect);
            bool windowUnderMouse = Find.WindowStack.GetWindowAt(currentEvent.mousePosition) != null;
            bool interactive = mouseOverEntry && !windowUnderMouse;

            HandleInput(currentEvent, entryRect, settings, interactive);
            Draw(entryRect, interactive);
        }

        private Rect CurrentEntryRect(TechLabSettings settings)
        {
            float size = TechLabStyle.EntrySize;
            Vector2 position;

            if (settings.hasCustomEntryPosition)
            {
                // Re-apply persistence load / resolution changes defensively.
                position = ClampToScreen(new Vector2(settings.entryX, settings.entryY), size);
            }
            else
            {
                bool probeMatchesScreen = Patch_PlaySettingsRowEntry.HasRow
                                          && Patch_PlaySettingsRowEntry.LastScreenWidth == UI.screenWidth
                                          && Patch_PlaySettingsRowEntry.LastScreenHeight == UI.screenHeight;
                bool needRecompute = !hasCachedPosition
                                     || cachedScreenWidth != UI.screenWidth
                                     || cachedScreenHeight != UI.screenHeight
                                     || cachedLayoutVersion != TechLabUi.EntryLayoutVersion
                                     || (!cachedFromProbe && probeMatchesScreen);

                if (needRecompute)
                {
                    cachedPosition = DefaultPosition(probeMatchesScreen);
                    cachedFromProbe = probeMatchesScreen;
                    hasCachedPosition = true;
                    cachedScreenWidth = UI.screenWidth;
                    cachedScreenHeight = UI.screenHeight;
                    cachedLayoutVersion = TechLabUi.EntryLayoutVersion;
                }

                position = ClampToScreen(cachedPosition, size);
            }

            return new Rect(position.x, position.y, size, size);
        }

        private static Vector2 DefaultPosition(bool useProbe)
        {
            float size = TechLabStyle.EntrySize;

            // The probe row is the vanilla play settings block (bottom right, just above the
            // bottom control bar). Put the entry just above and left of that block so neither
            // the vanilla toggles nor the time controls are covered.
            float rowLeft = useProbe ? Patch_PlaySettingsRowEntry.LastRowLeftX : UI.screenWidth - 165f;
            float rowTop = useProbe ? Patch_PlaySettingsRowEntry.LastRowTopY : UI.screenHeight - 63f;

            return new Vector2(rowLeft - size - 16f, rowTop - size - 6f);
        }

        private static Vector2 ClampToScreen(Vector2 position, float size)
        {
            float maxX = Mathf.Max(4f, UI.screenWidth - size - 4f);
            float maxY = Mathf.Max(4f, UI.screenHeight - size - 4f);
            return new Vector2(Mathf.Clamp(position.x, 4f, maxX), Mathf.Clamp(position.y, 4f, maxY));
        }

        private static Rect HideButtonRect(Rect entryRect)
        {
            const float buttonSize = TechLabStyle.EntryHideButtonSize;
            return new Rect(entryRect.xMax - buttonSize, entryRect.y, buttonSize, buttonSize);
        }

        private void HandleInput(Event currentEvent, Rect entryRect, TechLabSettings settings, bool interactive)
        {
            switch (currentEvent.type)
            {
                case EventType.MouseDown:
                    if (currentEvent.button != 0)
                    {
                        return;
                    }

                    if (!interactive)
                    {
                        ResetPress();
                        return;
                    }

                    pressActive = true;
                    dragMoved = false;
                    pressOnHideButton = HideButtonRect(entryRect).Contains(currentEvent.mousePosition);
                    pressMousePosition = currentEvent.mousePosition;
                    pressEntryPosition = entryRect.position;
                    currentEvent.Use();
                    break;

                case EventType.MouseDrag:
                    if (!pressActive || currentEvent.button != 0)
                    {
                        return;
                    }

                    if (!dragMoved
                        && (currentEvent.mousePosition - pressMousePosition).sqrMagnitude
                        >= TechLabStyle.EntryDragThreshold * TechLabStyle.EntryDragThreshold)
                    {
                        dragMoved = true;
                    }

                    if (!dragMoved)
                    {
                        return;
                    }

                    Vector2 moved = ClampToScreen(
                        pressEntryPosition + (currentEvent.mousePosition - pressMousePosition),
                        TechLabStyle.EntrySize);
                    settings.hasCustomEntryPosition = true;
                    settings.entryX = moved.x;
                    settings.entryY = moved.y;
                    cachedPosition = moved;
                    hasCachedPosition = true;
                    cachedScreenWidth = UI.screenWidth;
                    cachedScreenHeight = UI.screenHeight;
                    currentEvent.Use();
                    break;

                case EventType.MouseUp:
                    if (currentEvent.button != 0 || !pressActive)
                    {
                        return;
                    }

                    pressActive = false;
                    bool wasDrag = dragMoved;
                    dragMoved = false;
                    bool releasedOnHide = pressOnHideButton && HideButtonRect(entryRect).Contains(currentEvent.mousePosition);
                    bool releasedOnEntry = !pressOnHideButton && entryRect.Contains(currentEvent.mousePosition);
                    pressOnHideButton = false;

                    if (wasDrag)
                    {
                        // Persist once when the drag ends, not on every drag event.
                        settings.Write();
                        currentEvent.Use();
                        return;
                    }

                    if (releasedOnHide)
                    {
                        HideEntry(settings);
                        currentEvent.Use();
                        return;
                    }

                    if (releasedOnEntry)
                    {
                        TechLabUi.ToggleWindow();
                        currentEvent.Use();
                    }

                    break;
            }
        }

        private static void HideEntry(TechLabSettings settings)
        {
            settings.showFloatingEntry = false;
            settings.Write();
            SoundDefOf.Click.PlayOneShotOnCamera();
            Messages.Message(TechLabUi.T("WotF_UI.EntryHidden"), MessageTypeDefOf.SilentInput, false);
        }

        private static void Draw(Rect entryRect, bool interactive)
        {
            using (TechLabDrawScope.Begin())
            {
                Widgets.DrawBoxSolidWithOutline(entryRect,
                    interactive ? TechLabStyle.EntryFillHover : TechLabStyle.EntryFill,
                    TechLabStyle.AccentDim, 1);

                Rect iconRect = entryRect.ContractedBy(7f);
                Texture2D icon = TechLabStyle.Icon;
                if (icon != null)
                {
                    Widgets.DrawTextureFitted(iconRect, icon, 1f);
                }
                else
                {
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(iconRect, TechLabStyle.Glyph);
                }

                Rect hideRect = HideButtonRect(entryRect);
                bool hideHover = interactive && hideRect.Contains(Event.current.mousePosition);
                Widgets.DrawBoxSolidWithOutline(hideRect,
                    hideHover ? TechLabStyle.HideFillHover : TechLabStyle.HideFill,
                    TechLabStyle.AccentDim, 1);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(hideRect, "×");

                TooltipHandler.TipRegion(entryRect, TechLabUi.T("WotF_UI.EntryTooltip"));
                TooltipHandler.TipRegion(hideRect, TechLabUi.T("WotF_UI.EntryHideTooltip"));
            }
        }

        private void ResetPress()
        {
            pressActive = false;
            pressOnHideButton = false;
            dragMoved = false;
        }
    }
}
