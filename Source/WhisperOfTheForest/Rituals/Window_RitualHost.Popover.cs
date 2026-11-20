using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Race;

namespace WhisperOfTheForest.Rituals
{
    public sealed partial class Window_RitualHost
    {
        private void OpenPawnPopover(bool forHost, Rect anchorScreenRect)
        {
            if (activePopover != null && activePopover.IsOpen && activePopover.ForHost == forHost)
            {
                ClosePopover();
                return;
            }

            ClosePopover();
            activePopover = new PopoverWindow(this, forHost, anchorScreenRect);
            Find.WindowStack.Add(activePopover);
        }

        private void ClosePopover()
        {
            if (activePopover != null && activePopover.IsOpen)
            {
                activePopover.Close(doCloseSound: false);
            }

            activePopover = null;
        }

        /// <summary>
        /// Floating pawn selection popover above the portrait slots. Renders on WindowLayer.Super
        /// so its rows receive clicks before the host spread and closes cleanly on outside clicks.
        /// Drafted colonists are listed and selectable identically to undrafted colonists.
        /// </summary>
        private sealed class PopoverWindow : Window
        {
            private const float Width = 286f;
            private const float HeaderHeight = 28f;
            private const float RowHeight = 44f;
            private const float RowGap = 4f;
            private const float Pad = 8f;

            private readonly Window_RitualHost parentWindow;
            private readonly Rect anchorScreenRect;
            private Vector2 scrollPos;

            public bool ForHost { get; }

            public PopoverWindow(Window_RitualHost parentWindow, bool forHost, Rect anchorScreenRect)
            {
                this.parentWindow = parentWindow;
                ForHost = forHost;
                this.anchorScreenRect = anchorScreenRect;

                layer = WindowLayer.Super;
                doWindowBackground = false;
                doCloseX = false;
                doCloseButton = false;
                draggable = false;
                forcePause = false;
                preventCameraMotion = false;
                absorbInputAroundWindow = false;
                closeOnClickedOutside = true;
                closeOnCancel = true;
                closeOnAccept = false;
                drawShadow = true;
            }

            protected override float Margin => 0f;

            public override Vector2 InitialSize
            {
                get
                {
                    int count = parentWindow.BloodlinePawns().Count;
                    float listH = count == 0
                        ? 48f
                        : count * RowHeight + Mathf.Max(0, count - 1) * RowGap;
                    float height = Mathf.Clamp(Pad * 2f + HeaderHeight + 6f + listH, 100f, 340f);
                    return new Vector2(Width, height);
                }
            }

            protected override void SetInitialSizeAndPosition()
            {
                Vector2 size = InitialSize;
                float x = Mathf.Clamp(anchorScreenRect.x, 8f, Mathf.Max(8f, UI.screenWidth - size.x - 8f));
                float yAbove = anchorScreenRect.y - size.y - 6f;
                float yBelow = anchorScreenRect.yMax + 6f;
                float y = yAbove >= 8f
                    ? yAbove
                    : Mathf.Clamp(yBelow, 8f, Mathf.Max(8f, UI.screenHeight - size.y - 8f));
                windowRect = GenUI.Rounded(new Rect(x, y, size.x, size.y));
            }

            public override void DoWindowContents(Rect inRect)
            {
                if (parentWindow == null || !parentWindow.IsOpen || parentWindow.altar == null
                    || !parentWindow.altar.parent.Spawned)
                {
                    Close(doCloseSound: false);
                    return;
                }

                using (TechLabDrawScope.Begin())
                {
                    RitualPanelStyle.DrawCoverBackdrop(inRect);

                    Rect headerRect = new Rect(Pad + 2f, Pad, inRect.width - (Pad + 2f) * 2f, HeaderHeight);
                    string title = (ForHost
                        ? "WotF_RitualPanel_PickHostTitle"
                        : "WotF_RitualPanel_PickHelperTitle").Translate();
                    RitualPanelStyle.DrawLabel(new Rect(headerRect.x, headerRect.y, headerRect.width - 24f, HeaderHeight),
                        title, GameFont.Small, RitualPanelStyle.CoverGold, TextAnchor.MiddleLeft);

                    Rect closeRect = new Rect(headerRect.xMax - 20f, headerRect.y + 4f, 20f, 20f);
                    RitualPanelStyle.DrawIcon(closeRect.ContractedBy(3f), TexButton.CloseXSmall,
                        Mouse.IsOver(closeRect) ? RitualPanelStyle.CoverGold : RitualPanelStyle.CoverTextDim);
                    if (Widgets.ButtonInvisible(closeRect, doMouseoverSound: true))
                    {
                        Close(doCloseSound: false);
                        return;
                    }

                    Widgets.DrawBoxSolid(
                        new Rect(Pad + 2f, headerRect.yMax, inRect.width - (Pad + 2f) * 2f, 1f),
                        RitualPanelStyle.CoverRim);

                    Rect listOuter = new Rect(Pad + 2f, headerRect.yMax + 6f, inRect.width - (Pad + 2f) * 2f,
                        inRect.height - (headerRect.yMax + 6f) - Pad - 2f);

                    List<Pawn> candidates = parentWindow.BloodlinePawns();
                    if (candidates.Count == 0)
                    {
                        RitualPanelStyle.DrawWrappedLabel(listOuter, "WotF_Ritual_NoCaster".Translate(),
                            GameFont.Small, RitualPanelStyle.CoverTextDim, TextAnchor.MiddleCenter);
                        return;
                    }

                    float viewH = candidates.Count * RowHeight + Mathf.Max(0, candidates.Count - 1) * RowGap;
                    bool needScroll = viewH > listOuter.height;
                    float viewW = listOuter.width - (needScroll ? 16f : 0f);
                    Rect viewRect = new Rect(0f, 0f, viewW, Mathf.Max(listOuter.height, viewH));

                    Widgets.BeginScrollView(listOuter, ref scrollPos, viewRect);
                    float y = 0f;
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        DrawCandidateRow(new Rect(0f, y, viewW, RowHeight), candidates[i]);
                        y += RowHeight + RowGap;
                    }

                    Widgets.EndScrollView();
                }
            }

            private void DrawCandidateRow(Rect rowRect, Pawn pawn)
            {
                bool isCurrent = ForHost
                    ? parentWindow.selectedHost == pawn
                    : parentWindow.helpers.Contains(pawn);
                bool inOtherRole = ForHost
                    ? parentWindow.helpers.Contains(pawn)
                    : parentWindow.selectedHost == pawn;
                bool eligible = CompRitualAltar.IsEligibleRitualCaster(pawn, out string ineligibleReason);
                bool over = eligible && Mouse.IsOver(rowRect);

                Color fill;
                Color border;
                if (isCurrent)
                {
                    fill = RitualPanelStyle.PopoverRowSelBg;
                    border = RitualPanelStyle.CoverBorder;
                }
                else if (inOtherRole)
                {
                    fill = over ? RitualPanelStyle.PopoverRowHoverBg : RitualPanelStyle.PopoverRowOtherBg;
                    border = RitualPanelStyle.PopoverRowOtherBorder;
                }
                else if (!eligible)
                {
                    fill = RitualPanelStyle.PopoverRowLockedBg;
                    border = RitualPanelStyle.PopoverBorder;
                }
                else
                {
                    fill = over ? RitualPanelStyle.PopoverRowHoverBg : RitualPanelStyle.PopoverRowBg;
                    border = over ? RitualPanelStyle.CoverBorder : RitualPanelStyle.PopoverBorder;
                }

                RitualPanelStyle.DrawPanel(rowRect, fill);
                RitualPanelStyle.DrawBox(rowRect, Color.clear, border, 1);

                Rect checkRect = new Rect(rowRect.x + 8f, rowRect.center.y - 8f, 16f, 16f);
                Texture2D checkTex = ForHost
                    ? (isCurrent ? RitualPanelStyle.RadioOn : RitualPanelStyle.RadioOff)
                    : (isCurrent ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
                Color checkTint = !eligible ? RitualPanelStyle.CoverTextDim
                    : isCurrent ? RitualPanelStyle.PopoverOkGreen
                    : RitualPanelStyle.CoverTextDim;
                RitualPanelStyle.DrawIcon(checkRect, checkTex, checkTint);

                Rect portraitRect = new Rect(checkRect.xMax + 8f, rowRect.y + 4f, 36f, 36f);
                RitualPanelStyle.DrawBox(portraitRect, RitualPanelStyle.PortraitBg,
                    RitualPanelStyle.PopoverBorder, 1);
                RitualPanelStyle.DrawPawnPortrait(portraitRect.ContractedBy(1f), pawn, new Vector2(34f, 34f));

                string tagText = null;
                Color tagBg = Color.clear;
                Color tagBorder = Color.clear;
                Color tagColor = Color.white;
                if (!eligible)
                {
                    tagText = ineligibleReason;
                    tagBg = RitualPanelStyle.PopoverWarnBg;
                    tagBorder = RitualPanelStyle.RubricRed;
                    tagColor = RitualPanelStyle.PopoverWarnText;
                }
                else if (inOtherRole)
                {
                    tagText = (ForHost
                        ? "WotF_RitualPanel_TagIsHelper"
                        : "WotF_RitualPanel_TagIsHost").Translate();
                    tagBg = RitualPanelStyle.RoleTagBg;
                    tagBorder = RitualPanelStyle.RoleTagBorder;
                    tagColor = RitualPanelStyle.RoleTagText;
                }

                float tagW = 0f;
                if (!tagText.NullOrEmpty())
                {
                    tagW = Mathf.Min(108f, RitualPanelStyle.CalcTextWidth(tagText, GameFont.Tiny) + 12f);
                    Rect tagRect = new Rect(rowRect.xMax - tagW - 8f, rowRect.center.y - 10f, tagW, 20f);
                    RitualPanelStyle.DrawBox(tagRect, tagBg, tagBorder, 1);
                    RitualPanelStyle.DrawLabel(tagRect.ContractedBy(4f, 0f), tagText, GameFont.Tiny,
                        tagColor, TextAnchor.MiddleCenter);
                }

                float infoX = portraitRect.xMax + 8f;
                float infoW = Mathf.Max(40f, rowRect.xMax - infoX - (tagW > 0f ? tagW + 14f : 8f));
                RitualPanelStyle.DrawLabel(new Rect(infoX, rowRect.y + 4f, infoW, 18f),
                    pawn.LabelShortCap, GameFont.Small,
                    eligible ? RitualPanelStyle.CoverText : RitualPanelStyle.CoverTextDim,
                    TextAnchor.MiddleLeft);

                float level = Gene_ForestBloodline.Get(pawn)?.Level ?? 0f;
                string chantStr = string.Format("WotF_RitualPanel_ChantLevelShort".Translate(),
                    level.ToString("0.#"));
                RitualPanelStyle.DrawLabel(new Rect(infoX, rowRect.y + 22f, infoW, 18f),
                    chantStr, GameFont.Tiny,
                    eligible ? RitualPanelStyle.LevelTagText : RitualPanelStyle.CoverTextDim,
                    TextAnchor.MiddleLeft);

                if (!eligible && !ineligibleReason.NullOrEmpty())
                {
                    TooltipHandler.TipRegion(rowRect, ineligibleReason);
                    return;
                }

                if (Widgets.ButtonInvisible(rowRect, doMouseoverSound: true))
                {
                    TogglePawn(pawn, isCurrent);
                }
            }

            private void TogglePawn(Pawn pawn, bool isCurrent)
            {
                if (ForHost)
                {
                    if (isCurrent)
                    {
                        parentWindow.selectedHost = null;
                    }
                    else
                    {
                        parentWindow.helpers.Remove(pawn);
                        parentWindow.selectedHost = pawn;
                    }

                    parentWindow.selectedBookSlot = -1;
                    parentWindow.dirty = true;
                    Close(doCloseSound: false);
                }
                else
                {
                    if (isCurrent)
                    {
                        parentWindow.helpers.Remove(pawn);
                    }
                    else
                    {
                        if (parentWindow.selectedHost == pawn)
                        {
                            parentWindow.selectedHost = null;
                            parentWindow.selectedBookSlot = -1;
                        }

                        parentWindow.helpers.Add(pawn);
                    }

                    parentWindow.dirty = true;
                }
            }
        }
    }
}
