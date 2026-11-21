using System.Collections.Generic;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Right page: header with chain-limit badge, 2×5 ten-slot grid (all 10 slots open, 4 states),
    /// spell name + form capsule, single-row 10-socket rune chain with sealed sockets beyond worn
    /// book capacity and augment-to-anchor highlighting, scrollable live tactical plan &amp; carrier
    /// compatibility ledger, and fixed bottom action row(s).
    /// </summary>
    public partial class Window_Spellbook
    {
        private EditorLayout EditorLayoutFor(Rect page)
        {
            int socketCount = Mathf.Max(SpellbookStyle.MaxGlyphs, chain.glyphs.Count);
            return EditorLayout.Compute(page, socketCount, IsStandalone);
        }

        private void DrawEditorPage(Rect page, out EditorLayout layout)
        {
            layout = EditorLayoutFor(page);

            DrawEditorHeader(layout.Title, layout.Divider);
            if (!IsStandalone)
            {
                DrawSlotGrid(layout);
            }

            DrawNameAndFormRow(layout);
            DrawChainCaption(layout.ChainCaption);
            DrawBar(layout);
            DrawTacticalLedger(layout);
            DrawActionRow(layout.ActionRow);
            if (!IsStandalone)
            {
                DrawCastRow(layout.CastRow);
            }
        }

        private void DrawEditorHeader(Rect titleRect, Rect dividerRect)
        {
            int limit = IsStandalone ? SpellbookStyle.MaxGlyphs : bookMaxGlyphs;
            string capText = TF("WotF_Spellbook.ChainCapBadge", limit);
            float capW = Mathf.Ceil(BookUi.CalcTextWidth(capText, GameFont.Tiny)) + 12f;
            Rect capRect = new Rect(titleRect.xMax - capW, titleRect.y + 3f, capW, 18f);

            bool overCap = !IsStandalone && chain.glyphs.Count > bookMaxGlyphs;
            BookUi.DrawLabel(titleRect, T("WotF_Spellbook.PageEditor"), GameFont.Medium,
                BookUi.InkPrimary, TextAnchor.MiddleCenter, ellipsis: true);
            BookUi.DrawPillBadge(capRect, capText,
                overCap ? BookUi.ChipWarnBg : BookUi.ChipBg,
                overCap ? BookUi.ChipWarnBorder : BookUi.ChipBorder,
                overCap ? BookUi.RubricRed : BookUi.InkSecondary);
            BookUi.DrawIcon(dividerRect, BookUi.Divider, Color.white);
        }

        // ---------------------------------------------------------------- 2×5 ten-slot grid

        private void DrawSlotGrid(EditorLayout layout)
        {
            for (int i = 0; i < GameComponent_Spellbook.SlotCount; i++)
            {
                Rect tab = layout.SlotTabRect(i);
                SpellRecipe saved = slotRecipes[i];
                bool selected = i == activeSlot;
                bool used = saved != null;
                string warnReason = null;
                bool exceedsBook = used && SlotExceedsBook(saved, out warnReason);
                bool over = Mouse.IsOver(tab);

                Color bg;
                Color border;
                if (exceedsBook)
                {
                    bg = selected ? BookUi.ChipWarnBg : BookUi.CardVellumWarn;
                    border = selected ? BookUi.GoldAccent : BookUi.ChipWarnBorder;
                }
                else if (selected)
                {
                    bg = BookUi.CardVellumSel;
                    border = BookUi.GoldAccent;
                }
                else if (used)
                {
                    bg = over ? BookUi.CardVellumHover : BookUi.CardVellumNormal;
                    border = BookUi.LedgerBorder;
                }
                else
                {
                    bg = over ? BookUi.CardVellumHover : BookUi.CardVellumLocked;
                    border = BookUi.SubtleRule;
                }

                BookUi.DrawBox(tab, bg, border, selected ? 2 : 1);

                // Semantic left indicator strip
                if (exceedsBook)
                {
                    Widgets.DrawBoxSolid(new Rect(tab.x + 1f, tab.y + 2f, 2.5f, tab.height - 4f), BookUi.RubricRed);
                }
                else if (selected)
                {
                    Widgets.DrawBoxSolid(new Rect(tab.x + 1f, tab.y + 2f, 2.5f, tab.height - 4f), BookUi.BotanicalJade);
                }
                else if (used)
                {
                    Widgets.DrawBoxSolid(new Rect(tab.x + 1f, tab.y + 2f, 2.5f, tab.height - 4f), BookUi.Fade(BookUi.BotanicalJade, 0.55f));
                }

                float x = tab.x + 5f;
                string numText = (i + 1).ToString();
                float numW = i >= 9 ? 14f : 10f;
                BookUi.DrawLabel(new Rect(x, tab.y, numW, tab.height), numText, GameFont.Tiny,
                    exceedsBook ? BookUi.RubricRed : (selected ? BookUi.BotanicalJade : BookUi.InkMuted),
                    TextAnchor.MiddleLeft, ellipsis: false);
                x += numW + 2f;

                if (exceedsBook)
                {
                    BookUi.DrawIcon(new Rect(x, tab.y + (tab.height - 12f) * 0.5f, 12f, 12f),
                        SpellbookStyle.IconWarn, BookUi.RubricRed);
                    x += 13f;
                }
                else if (used && slotIcons[i] != null && tab.width >= 66f)
                {
                    BookUi.DrawIcon(new Rect(x, tab.y + (tab.height - 14f) * 0.5f, 14f, 14f),
                        slotIcons[i], Color.white);
                    x += 15f;
                }

                string label = used ? saved.name : T("WotF_Spellbook.SlotEmptyShort");
                Color labelColor = exceedsBook ? BookUi.RubricRed
                    : used ? BookUi.InkPrimary
                    : BookUi.InkMuted;
                BookUi.DrawLabel(new Rect(x, tab.y, Mathf.Max(8f, tab.xMax - x - 3f), tab.height),
                    label, GameFont.Tiny, labelColor, TextAnchor.MiddleLeft, ellipsis: true);

                string tip;
                if (exceedsBook)
                {
                    tip = TF("WotF_Spellbook.SlotTitle", i + 1) + " · " + saved.name
                        + "\n" + warnReason
                        + "\n" + T("WotF_Spellbook.SlotExceedsBookHint");
                }
                else if (used)
                {
                    tip = TF("WotF_Spellbook.SlotTitle", i + 1) + " · " + TF("WotF_Spellbook.SlotTooltipUsed", saved.name)
                        + " (" + TF("WotF_Spellbook.SlotGlyphCount", saved.glyphs.Count) + ")"
                        + "\n" + T("WotF_Spellbook.SwitchBookmark");
                }
                else
                {
                    tip = TF("WotF_Spellbook.SlotTitle", i + 1) + " · " + T("WotF_Spellbook.SlotEmpty")
                        + "\n" + T("WotF_Spellbook.SwitchBookmark");
                }

                TooltipHandler.TipRegion(tab, tip);

                if (Widgets.ButtonInvisible(tab, true))
                {
                    SelectBookmark(i);
                }
            }
        }

        // ---------------------------------------------------------------- name + form capsule

        private void DrawNameAndFormRow(EditorLayout layout)
        {
            chain.name = SpellbookStyle.TextField(layout.NameField, chain.name,
                T("WotF_Spellbook.NamePlaceholder"), null);

            SpellGlyphDef first = GlyphDefAt(0);
            if (first != null && (first.kind == SpellGlyphKind.Form || first.kind == SpellGlyphKind.Super))
            {
                Color accent = SpellbookStyle.KindColor(first.kind);
                string formText = first.kind == SpellGlyphKind.Super
                    ? first.LabelCap.ToString()
                    : SpellbookStyle.FormLabel(SpellCompiler.FormOf(first));
                BookUi.DrawPillBadge(layout.FormCapsule, formText,
                    BookUi.Fade(accent, 0.16f), BookUi.Fade(accent, 0.78f), accent, GameFont.Small);
            }
            else
            {
                BookUi.DrawPillBadge(layout.FormCapsule, T("WotF_Spellbook.FormNone"),
                    BookUi.CardVellumLocked, BookUi.SubtleRule, BookUi.InkMuted, GameFont.Tiny);
            }
        }

        // ---------------------------------------------------------------- single-row 10-socket rune chain

        private SpellGlyphDef GlyphDefAt(int index)
        {
            if (index < 0 || index >= chain.glyphs.Count)
            {
                return null;
            }

            string defName = chain.glyphs[index];
            return defName.NullOrEmpty() ? null : SpellDefs.Glyph(defName);
        }

        private void DrawChainCaption(Rect rect)
        {
            int cap = IsStandalone ? SpellbookStyle.MaxGlyphs : bookMaxGlyphs;
            bool overCap = !IsStandalone && chain.glyphs.Count > cap;
            string count = TF("WotF_Spellbook.ChainHeader", chain.glyphs.Count, cap);
            float countW = Mathf.Ceil(BookUi.CalcTextWidth(count, GameFont.Tiny)) + 8f;
            BookUi.DrawLabel(new Rect(rect.x, rect.y, countW, rect.height), count, GameFont.Tiny,
                overCap ? BookUi.RubricRed : BookUi.InkSecondary, TextAnchor.MiddleLeft, ellipsis: false);

            BookUi.DrawLabel(new Rect(rect.x + countW, rect.y, Mathf.Max(0f, rect.width - countW), rect.height),
                T("WotF_Spellbook.ChainHintShort"), GameFont.Tiny, BookUi.InkMuted, TextAnchor.MiddleRight, ellipsis: true);
        }

        private void DrawBar(EditorLayout layout)
        {
            int openLimit = IsStandalone ? SpellbookStyle.MaxGlyphs : bookMaxGlyphs;

            // Pre-pass hover so hovering an augment highlights its bound anchor on the exact same frame.
            hoverGlyph = -1;
            for (int i = 0; i < chain.glyphs.Count && i < layout.SocketCount; i++)
            {
                if (!anim.IsAddingIndex(i) && Mouse.IsOver(layout.BeadRect(i)))
                {
                    hoverGlyph = i;
                    SpellGlyphDef hovered = GlyphDefAt(i);
                    if (hovered != null)
                    {
                        lastInspectedGlyph = hovered;
                    }
                    break;
                }
            }

            int anchorHighlightIdx = -1;
            int boundRangeStart = -1;
            int boundRangeEnd = -1;
            if (hoverGlyph >= 0)
            {
                SpellGlyphDef hovered = GlyphDefAt(hoverGlyph);
                if (hovered != null && hovered.kind == SpellGlyphKind.Augment)
                {
                    for (int k = hoverGlyph - 1; k >= 0; k--)
                    {
                        SpellGlyphDef candidate = GlyphDefAt(k);
                        if (candidate != null && candidate.kind != SpellGlyphKind.Augment)
                        {
                            anchorHighlightIdx = k;
                            break;
                        }
                    }
                }
                else if (hovered != null && (hovered.kind == SpellGlyphKind.Form || hovered.kind == SpellGlyphKind.Effect))
                {
                    int k = hoverGlyph + 1;
                    while (k < chain.glyphs.Count && GlyphDefAt(k)?.kind == SpellGlyphKind.Augment)
                    {
                        if (boundRangeStart < 0)
                        {
                            boundRangeStart = k;
                        }
                        boundRangeEnd = k;
                        k++;
                    }
                }
            }

            // Vine track running behind the single socket row (bright for open slots, dim for sealed).
            float centerY = layout.Chain.y + layout.SocketSize * 0.5f;
            if (openLimit > 0)
            {
                int lastOpen = Mathf.Min(openLimit, layout.SocketCount) - 1;
                Rect lastOpenRect = layout.BeadRect(lastOpen);
                float openW = lastOpenRect.xMax - layout.Chain.x + 10f;
                BookUi.DrawStretched(new Rect(layout.Chain.x - 5f, centerY - 8f, openW, 16f),
                    SpellbookStyle.VineTrack, new Color(1f, 1f, 1f, 0.85f));
            }
            if (openLimit < layout.SocketCount)
            {
                Rect firstSealed = layout.BeadRect(openLimit);
                float sealedW = layout.Chain.xMax - firstSealed.x + 5f;
                BookUi.DrawStretched(new Rect(firstSealed.x, centerY - 8f, sealedW, 16f),
                    SpellbookStyle.VineTrack, new Color(0.55f, 0.52f, 0.48f, 0.30f));
            }

            // Binding connector rail between hovered augment and its left anchor bead.
            if (anchorHighlightIdx >= 0 && hoverGlyph > anchorHighlightIdx)
            {
                Rect anchorSock = layout.SocketRect(layout.BeadRect(anchorHighlightIdx));
                Rect hoverSock = layout.SocketRect(layout.BeadRect(hoverGlyph));
                float railY = anchorSock.yMax + 1f;
                Widgets.DrawBoxSolid(
                    new Rect(anchorSock.center.x, railY, hoverSock.center.x - anchorSock.center.x, 2f),
                    SpellbookStyle.AnchorHighlight);
            }

            anim.TryNudge(out float nudgeX);

            for (int i = 0; i < layout.SocketCount; i++)
            {
                Rect bead = layout.BeadRect(i);
                bool sealedByTier = i >= openLimit;

                if (i >= chain.glyphs.Count)
                {
                    if (sealedByTier)
                    {
                        DrawSealedSocket(layout, bead, i);
                    }
                    else
                    {
                        DrawOpenEmptySocket(layout, bead, i, i == chain.glyphs.Count);
                    }
                    continue;
                }

                if (anim.IsAddingIndex(i))
                {
                    DrawOpenEmptySocket(layout, bead, i, false);
                    continue;
                }

                bead.x += nudgeX;
                bool isAnchor = i == anchorHighlightIdx;
                bool isBoundAugment = boundRangeStart >= 0 && i >= boundRangeStart && i <= boundRangeEnd;
                if (DrawBead(layout, bead, i, sealedByTier, isAnchor, isBoundAugment))
                {
                    break;
                }
            }
        }

        private bool DrawBead(EditorLayout layout, Rect bead, int index, bool exceedsBookCap,
            bool isAnchorHighlight, bool isBoundHighlight)
        {
            SpellGlyphDef glyph = GlyphDefAt(index);
            bool hovered = index == hoverGlyph;
            Rect socket = layout.SocketRect(bead);

            if (hovered)
            {
                Color glowColor = glyph != null ? SpellbookStyle.KindColor(glyph.kind) : BookUi.RubricRed;
                SpellbookStyle.DrawGlow(socket.center, socket.width * 1.65f, glowColor, 0.58f);
            }
            else if (isAnchorHighlight)
            {
                SpellbookStyle.DrawGlow(socket.center, socket.width * 1.70f, SpellbookStyle.AnchorHighlight, 0.72f);
            }
            else if (isBoundHighlight)
            {
                Color augColor = SpellbookStyle.KindColor(SpellGlyphKind.Augment);
                SpellbookStyle.DrawGlow(socket.center, socket.width * 1.50f, augColor, 0.45f);
            }

            Texture2D icon = SpellbookStyle.GlyphIcon(glyph);
            SpellbookStyle.DrawSocket(socket, icon, 1f);
            if (icon == null)
            {
                BookUi.DrawLabel(socket, "?", GameFont.Small, BookUi.CoverWarning, TextAnchor.MiddleCenter, ellipsis: false);
            }

            if (isAnchorHighlight)
            {
                BookUi.DrawBox(socket.ExpandedBy(1f), Color.clear, SpellbookStyle.AnchorHighlight, 2);
            }
            else if (exceedsBookCap)
            {
                BookUi.DrawBox(socket, BookUi.Fade(BookUi.RubricRed, 0.16f), BookUi.RubricRed, 1);
            }

            BookUi.DrawLabel(new Rect(bead.x, socket.yMax, bead.width, bead.yMax - socket.yMax),
                (index + 1).ToString(), GameFont.Tiny,
                exceedsBookCap ? BookUi.RubricRed : (isAnchorHighlight ? BookUi.AmberAccent : BookUi.InkSecondary),
                TextAnchor.MiddleCenter, ellipsis: false);

            if (hovered)
            {
                BookUi.DrawIcon(new Rect(socket.xMax - 11f, socket.y - 2f, 13f, 13f),
                    SpellbookStyle.IconRemove, BookUi.BtnRust);
            }

            string tip = TF("WotF_Spellbook.ClickToRemove",
                glyph != null ? glyph.LabelCap.ToString() : T("WotF_Spellbook.GlyphMissing"));
            if (exceedsBookCap)
            {
                tip += "\n" + SealedSocketTooltip(index);
            }
            else if (glyph != null && glyph.kind == SpellGlyphKind.Augment)
            {
                tip += "\n" + T("WotF_Spellbook.AugmentBindHint");
            }

            TooltipHandler.TipRegion(bead, tip);

            if (Widgets.ButtonInvisible(bead, true))
            {
                RemoveGlyph(index, socket);
                return true;
            }

            return false;
        }

        private static void DrawOpenEmptySocket(EditorLayout layout, Rect bead, int index, bool isNext)
        {
            Rect socket = layout.SocketRect(bead);
            if (isNext)
            {
                SpellbookStyle.DrawGlow(socket.center, socket.width * 1.45f, SpellbookStyle.JadeBright, 0.30f);
            }

            BookUi.DrawBox(socket, BookUi.Fade(BookUi.ChipBg, isNext ? 0.78f : 0.42f),
                isNext ? BookUi.BotanicalJade : BookUi.LedgerBorder, 1);
            SpellbookStyle.DrawSocket(socket, null, isNext ? 0.72f : 0.36f);
            BookUi.DrawLabel(new Rect(bead.x, socket.yMax, bead.width, bead.yMax - socket.yMax),
                (index + 1).ToString(), GameFont.Tiny, BookUi.InkMuted, TextAnchor.MiddleCenter, ellipsis: false);
        }

        private void DrawSealedSocket(EditorLayout layout, Rect bead, int index)
        {
            Rect socket = layout.SocketRect(bead);
            Widgets.DrawBoxSolid(socket, SpellbookStyle.SealedSocketBg);
            BookUi.DrawDashedRect(socket, SpellbookStyle.SealedSocketBorder, 4f, 3f);
            BookUi.DrawLabel(socket, T("WotF_Spellbook.SocketSealedMark"), GameFont.Tiny,
                BookUi.InkMuted, TextAnchor.MiddleCenter, ellipsis: false);
            BookUi.DrawLabel(new Rect(bead.x, socket.yMax, bead.width, bead.yMax - socket.yMax),
                (index + 1).ToString(), GameFont.Tiny, BookUi.Fade(BookUi.InkMuted, 0.65f),
                TextAnchor.MiddleCenter, ellipsis: false);

            TooltipHandler.TipRegion(bead, SealedSocketTooltip(index));
        }

        private void RemoveGlyph(int index, Rect socketRect)
        {
            string defName = index >= 0 && index < chain.glyphs.Count ? chain.glyphs[index] : null;
            chain.RemoveAt(index);
            cachedPlanKey = null;
            if (hoverGlyph >= chain.glyphs.Count)
            {
                hoverGlyph = -1;
            }

            anim.OnRemove(socketRect, defName);
            SpellGlyphDef glyph = defName.NullOrEmpty() ? null : SpellDefs.Glyph(defName);
            SetStatus(TF("WotF_Spellbook.GlyphRemoved", glyph != null ? glyph.LabelCap.ToString() : defName ?? ""), false);
        }

        // ---------------------------------------------------------------- live tactical plan & carrier ledger

        private void DrawTacticalLedger(EditorLayout layout)
        {
            BookUi.DrawBox(layout.LedgerOuter, BookUi.LedgerBg, BookUi.LedgerBorder, 1);
            DrawValidationBar(layout.ValidationBar);

            Rect outer = layout.LedgerScroll;
            if (outer.height <= 8f || outer.width <= 20f)
            {
                return;
            }

            bool needScrollbar = ledgerViewHeight > outer.height + 1f;
            float viewW = outer.width - (needScrollbar ? 16f : 0f);
            Rect viewRect = new Rect(0f, 0f, viewW, Mathf.Max(outer.height, ledgerViewHeight));

            Widgets.BeginScrollView(outer, ref ledgerScroll, viewRect, true);
            ledgerViewHeight = DrawLedgerContent(viewW) + 4f;
            Widgets.EndScrollView();
        }

        private void DrawValidationBar(Rect rect)
        {
            Color bg;
            Color border;
            Color textColor;
            Texture2D icon;
            string text;

            if (HasActiveToast)
            {
                bg = statusIsError ? BookUi.ChipWarnBg : BookUi.ChipOkBg;
                border = statusIsError ? BookUi.ChipWarnBorder : BookUi.ChipOkBorder;
                textColor = statusIsError ? BookUi.RubricRed : BookUi.BotanicalJade;
                icon = statusIsError ? SpellbookStyle.IconWarn : SpellbookStyle.IconOk;
                text = statusText;
            }
            else if (!chain.HasGlyphs)
            {
                bg = BookUi.CardVellumLocked;
                border = BookUi.LedgerBorder;
                textColor = BookUi.InkSecondary;
                icon = SpellbookStyle.IconOk;
                text = T("WotF_Spellbook.ChainEmpty");
            }
            else if (draftOk)
            {
                bg = BookUi.ChipOkBg;
                border = BookUi.ChipOkBorder;
                textColor = BookUi.BotanicalJade;
                icon = SpellbookStyle.IconOk;
                text = T("WotF_Spellbook.ValidationOk");
            }
            else
            {
                bg = BookUi.ChipWarnBg;
                border = BookUi.ChipWarnBorder;
                textColor = BookUi.RubricRed;
                icon = SpellbookStyle.IconWarn;
                text = TF("WotF_Spellbook.ValidationFailed", ReasonOrUnknown(draftReason));
            }

            BookUi.DrawBox(rect, bg, border, 1);
            const float iconSize = 14f;
            BookUi.DrawIcon(new Rect(rect.x + 6f, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize),
                icon, textColor);
            Rect labelRect = new Rect(rect.x + 24f, rect.y, rect.width - 30f, rect.height);
            BookUi.DrawLabel(labelRect, text, GameFont.Tiny, textColor, TextAnchor.MiddleLeft, ellipsis: true);
            if (!text.NullOrEmpty() && BookUi.CalcTextWidth(text, GameFont.Tiny) > labelRect.width - 2f)
            {
                TooltipHandler.TipRegion(rect, text);
            }
        }

        private float DrawLedgerContent(float width)
        {
            float y = 2f;

            // 1. Metric Chips (2×2 grid + optional held-staff material preview row)
            if (chain.HasGlyphs)
            {
                y = DrawMetricChips(width, y);
                y += 6f;
            }

            // 2. Compiled effect plan summary lines (single source of truth from SpellCastPlan)
            BookUi.DrawLabel(new Rect(0f, y, width, 16f), T("WotF_Spellbook.SectionPlan"),
                GameFont.Tiny, BookUi.InkMuted, TextAnchor.MiddleLeft, ellipsis: false);
            float headerW = BookUi.CalcTextWidth(T("WotF_Spellbook.SectionPlan"), GameFont.Tiny);
            if (width - headerW - 8f > 10f)
            {
                BookUi.DrawHairline(headerW + 6f, y + 8f, width - headerW - 8f);
            }
            y += 18f;

            if (previewPlan != null && previewSummaryLines.Count > 0)
            {
                for (int i = 0; i < previewSummaryLines.Count; i++)
                {
                    string line = previewSummaryLines[i];
                    bool nested = line.StartsWith("  ");
                    float indentX = nested ? 12f : 10f;
                    float textW = Mathf.Max(40f, width - indentX - 2f);
                    float lineH = Mathf.Max(16f, BookUi.CalcTextHeight(line.TrimStart(), GameFont.Tiny, textW) + 2f);

                    if (nested)
                    {
                        Widgets.DrawBoxSolid(new Rect(5f, y + 2f, 2f, lineH - 4f), BookUi.Fade(BookUi.GoldAccent, 0.65f));
                        BookUi.DrawWrappedLabel(new Rect(indentX, y, textW, lineH), line.TrimStart(), GameFont.Tiny, BookUi.InkSecondary);
                    }
                    else
                    {
                        Color dotColor = i == 0 ? BookUi.BotanicalJade : BookUi.AmberAccent;
                        Widgets.DrawBoxSolid(new Rect(2f, y + 6f, 4f, 4f), dotColor);
                        BookUi.DrawWrappedLabel(new Rect(indentX, y, textW, lineH), line, GameFont.Tiny, BookUi.InkPrimary);
                    }

                    y += lineH + 2f;
                }
            }
            else
            {
                string hint = T("WotF_Spellbook.PlanPending");
                float hintH = BookUi.CalcTextHeight(hint, GameFont.Tiny, width) + 2f;
                BookUi.DrawWrappedLabel(new Rect(0f, y, width, hintH), hint, GameFont.Tiny, BookUi.InkMuted);
                y += hintH + 4f;
            }

            // 3. Target classes & Carrier compatibility micro-badges
            if (previewPlan != null)
            {
                y += 4f;
                BookUi.DrawHairline(0f, y, width);
                y += 6f;
                y = DrawTargetsAndCarriers(width, y);
            }

            return y;
        }

        private float DrawMetricChips(float width, float y)
        {
            const float chipH = 24f;
            const float gap = 5f;
            float halfW = Mathf.Floor((width - gap) * 0.5f);
            float rightW = width - halfW - gap;

            // Row 1 Left: Mana
            string manaLabel = IsStandalone ? T("WotF_Spellbook.ChipTurretMana") : T("WotF_Spellbook.ChipMana");
            float displayedMana = IsStandalone
                ? previewMana(chain.ToRecipe())
                : (heldStaffMods != null ? barCost.mana * heldStaffMods.manaFactor : barCost.mana);
            string manaVal = displayedMana.ToString("0.#");
            string manaTip = IsStandalone
                ? TF("WotF_Turret_ManaPreview", manaVal)
                : (heldStaffMods != null
                    ? TF("WotF_Spellbook.ChipManaStaffTip", barCost.mana.ToString("0.#"),
                        heldStaffMods.manaFactor.ToStringPercent("0"), manaVal, barCost.experience.ToString("0.#"))
                    : TF("WotF_Spellbook.ChipManaTip", barCost.mana.ToString("0.#"), barCost.experience.ToString("0.#")));
            BookUi.DrawInfoChip(new Rect(0f, y, halfW, chipH), manaLabel, manaVal,
                BookUi.ChipManaBg, BookUi.ChipManaBorder, BookUi.InkSecondary, BookUi.CrystalBlue,
                BookUi.ManaCrystal, BookUi.CrystalBlue, manaTip);

            // Row 1 Right: Total Cast Time + phase breakdown tooltip
            SpellTiming displayTiming = previewPlan?.timing ?? barTiming;
            int totalTicks = displayTiming.windupTicks + displayTiming.channelTicks + displayTiming.recoveryTicks;
            string timeVal = TF("WotF_Spellbook.SecondsShort", SpellbookStyle.Seconds(totalTicks));
            string timeTip = TF("WotF_Spellbook.Timing",
                    SpellbookStyle.Seconds(displayTiming.windupTicks),
                    SpellbookStyle.Seconds(displayTiming.channelTicks),
                    SpellbookStyle.Seconds(displayTiming.recoveryTicks))
                + "\n" + TF("WotF_Spellbook.TimingTip", displayTiming.windupTicks, displayTiming.channelTicks, displayTiming.recoveryTicks);
            BookUi.DrawInfoChip(new Rect(halfW + gap, y, rightW, chipH),
                T("WotF_Spellbook.ChipTime"), timeVal,
                BookUi.ChipBg, BookUi.ChipBorder, BookUi.InkSecondary, BookUi.InkPrimary,
                SpellbookStyle.IconHourglass, BookUi.InkSecondary, timeTip);
            y += chipH + gap;

            // Row 2 Left: Range & Form
            string rangeText = BuildRangeChipValue();
            BookUi.DrawInfoChip(new Rect(0f, y, halfW, chipH),
                T("WotF_Spellbook.ChipRange"), rangeText,
                BookUi.ChipBg, BookUi.ChipBorder, BookUi.InkSecondary, BookUi.InkPrimary);

            // Row 2 Right: Neural Heat (Entropy)
            bool hasEntropy = barCost.entropy > 0f;
            BookUi.DrawInfoChip(new Rect(halfW + gap, y, rightW, chipH),
                T("WotF_Spellbook.ChipEntropy"), barCost.entropy.ToString("0.#"),
                hasEntropy ? BookUi.ChipWarnBg : BookUi.ChipBg,
                hasEntropy ? BookUi.ChipWarnBorder : BookUi.ChipBorder,
                BookUi.InkSecondary,
                hasEntropy ? BookUi.RubricRed : BookUi.InkMuted);
            y += chipH;

            // Optional Row 3: Held Staff Material Preview
            if (!IsStandalone && heldStaff != null && heldStaffMods != null)
            {
                y += gap;
                string staffVal = TF("WotF_Spellbook.StaffModSummary",
                    heldStaffMods.damageFactor.ToStringPercent("0"),
                    heldStaffMods.manaFactor.ToStringPercent("0"),
                    heldStaffMods.durationFactor.ToStringPercent("0"));
                string staffTip = TF("WotF_Spellbook.StaffModTip",
                    heldStaff.parent.LabelCap,
                    heldStaffMods.damageFactor.ToStringPercent("0"),
                    heldStaffMods.manaFactor.ToStringPercent("0"),
                    heldStaffMods.durationFactor.ToStringPercent("0"),
                    heldStaffMods.rangeFactor.ToStringPercent("0"),
                    heldStaffMods.windupFactor.ToStringPercent("0"));
                BookUi.DrawInfoChip(new Rect(0f, y, width, chipH),
                    heldStaff.parent.LabelCap, staffVal,
                    BookUi.LedgerHighlightBg, BookUi.LedgerHighlightBorder,
                    BookUi.InkPrimary, BookUi.BotanicalJade, null, null, staffTip);
                y += chipH;
            }

            return y;
        }

        private string BuildRangeChipValue()
        {
            if (previewPlan != null)
            {
                if (previewPlan.form == SpellFormKind.Self || previewPlan.form == SpellFormKind.Orbit)
                {
                    return SpellbookStyle.FormLabel(previewPlan.form);
                }

                if (previewPlan.form == SpellFormKind.Special)
                {
                    float r = previewPlan.Special?.Range ?? 0f;
                    return r <= 0f
                        ? T("WotF_SpellPlan.FullMap")
                        : TF("WotF_SpellPlan.RangeCells", r.ToString("0.#"));
                }

                return TF("WotF_SpellPlan.RangeCells", previewPlan.range.ToString("0.#"));
            }

            SpellGlyphDef first = GlyphDefAt(0);
            if (first == null)
            {
                return "-";
            }

            if (first.kind == SpellGlyphKind.Form)
            {
                return first.range > 0f
                    ? TF("WotF_SpellPlan.RangeCells", first.range.ToString("0.#"))
                    : SpellbookStyle.FormLabel(SpellCompiler.FormOf(first));
            }

            return first.kind == SpellGlyphKind.Super ? T("WotF_SpellPlan.FullMap") : "-";
        }

        private float DrawTargetsAndCarriers(float width, float y)
        {
            // Target classes
            string targetsText;
            if (previewPlan.form == SpellFormKind.Self || previewPlan.form == SpellFormKind.Orbit)
            {
                targetsText = T("WotF_Spellbook.TargetCasterOnly");
            }
            else if (previewPlan.targets != null && previewPlan.targets.Count > 0)
            {
                var labels = new List<string>(previewPlan.targets.Count);
                for (int i = 0; i < previewPlan.targets.Count; i++)
                {
                    labels.Add(SpellbookStyle.TargetClassLabel(previewPlan.targets[i]));
                }
                targetsText = string.Join(" · ", labels);
            }
            else
            {
                targetsText = "-";
            }

            string targetLine = TF("WotF_Spellbook.TargetsLine", targetsText);
            float targetH = Mathf.Max(16f, BookUi.CalcTextHeight(targetLine, GameFont.Tiny, width) + 2f);
            BookUi.DrawWrappedLabel(new Rect(0f, y, width, targetH), targetLine, GameFont.Tiny, BookUi.InkSecondary);
            y += targetH + 4f;

            // Carrier compatibility micro-badges (Staff / Turret / Equipment Enchant)
            const float pillH = 20f;
            const float gap = 5f;
            float pillW = Mathf.Floor((width - 2f * gap) / 3f);
            float thirdW = width - 2f * (pillW + gap);

            DrawCarrierBadge(new Rect(0f, y, pillW, pillH),
                T("WotF_Spellbook.CarrierStaff"), previewStaffOk,
                previewStaffOk ? T("WotF_Spellbook.CarrierStaffOk") : T("WotF_Spellbook.CarrierStaffNo"));
            DrawCarrierBadge(new Rect(pillW + gap, y, pillW, pillH),
                T("WotF_Spellbook.CarrierTurret"), previewTurretOk,
                previewTurretOk ? T("WotF_Spellbook.CarrierTurretOk") : previewTurretReason);
            DrawCarrierBadge(new Rect((pillW + gap) * 2f, y, thirdW, pillH),
                T("WotF_Spellbook.CarrierEnchant"), previewEnchantOk,
                previewEnchantOk ? T("WotF_Spellbook.CarrierEnchantOk") : previewEnchantReason);

            return y + pillH + 2f;
        }

        private static void DrawCarrierBadge(Rect rect, string label, bool ok, string tooltip)
        {
            string text = label + (ok ? " ✓" : " ✗");
            BookUi.DrawPillBadge(rect, text,
                ok ? BookUi.ChipOkBg : BookUi.CardVellumLocked,
                ok ? BookUi.ChipOkBorder : BookUi.SubtleRule,
                ok ? BookUi.BotanicalJade : BookUi.InkMuted,
                GameFont.Tiny, tooltip);
        }

        // ---------------------------------------------------------------- bottom action rows

        private void DrawActionRow(Rect rect)
        {
            if (IsStandalone)
            {
                float submitW = Mathf.Floor(rect.width * 0.62f - 3f);
                Rect submitRect = new Rect(rect.x, rect.y, submitW, rect.height);
                Rect clearRect = new Rect(submitRect.xMax + 6f, rect.y, rect.xMax - submitRect.xMax - 6f, rect.height);
                if (SpellbookStyle.Button(submitRect, submitLabel, BookButtonRole.Primary, draftOk))
                {
                    RequestCreate();
                }

                if (SpellbookStyle.Button(clearRect, T("WotF_Spellbook.ClearBar"), BookButtonRole.Secondary, chain.HasGlyphs))
                {
                    RequestClearBar();
                }

                return;
            }

            const float gap = 6f;
            float width = Mathf.Floor((rect.width - 2f * gap) / 3f);
            Rect saveRect = new Rect(rect.x, rect.y, width, rect.height);
            Rect clearBarRect = new Rect(saveRect.xMax + gap, rect.y, width, rect.height);
            Rect deleteRect = new Rect(clearBarRect.xMax + gap, rect.y, rect.xMax - clearBarRect.xMax - gap, rect.height);

            if (SpellbookStyle.Button(saveRect, TF("WotF_Spellbook.Create", slot + 1), BookButtonRole.Primary, draftOk))
            {
                RequestCreate();
            }

            TooltipHandler.TipRegion(saveRect, TF("WotF_Spellbook.CreateTip", slot + 1));

            if (SpellbookStyle.Button(clearBarRect, T("WotF_Spellbook.ClearBar"), BookButtonRole.Secondary, chain.HasGlyphs))
            {
                RequestClearBar();
            }

            TooltipHandler.TipRegion(clearBarRect, T("WotF_Spellbook.ClearBarTip"));

            if (SpellbookStyle.Button(deleteRect, T("WotF_Spellbook.DeleteSlot"), BookButtonRole.Danger,
                    slotRecipes[slot] != null))
            {
                RequestDeleteSlot();
            }

            TooltipHandler.TipRegion(deleteRect, TF("WotF_Spellbook.DeleteSlotTip", slot + 1));
        }

        private void DrawCastRow(Rect rect)
        {
            if (castable)
            {
                BookUi.DrawStretched(rect.ExpandedBy(8f, 6f), SpellbookStyle.Glow,
                    BookUi.Fade(SpellbookStyle.JadeBright, 0.32f));
            }

            SpellRecipe saved = slotRecipes[slot];
            string castLabel = saved == null || saved.name.NullOrEmpty()
                ? T("WotF_Spellbook.Cast")
                : TF("WotF_Spellbook.CastNamed", saved.name);
            if (SpellbookStyle.Button(rect, castLabel, BookButtonRole.Primary, castable))
            {
                RequestCast();
            }

            TooltipHandler.TipRegion(rect, castable
                ? TF("WotF_Spellbook.CastTip", slot + 1)
                : (castReason.NullOrEmpty() ? T("WotF_Spellbook.EmptySlotHint") : castReason));
        }

        // ---------------------------------------------------------------- animations

        private void DrawAnimations(Rect rightPage, EditorLayout editor)
        {
            DrawPageCrease(rightPage);
            DrawCreateRipple(editor);
            DrawAddedLanding(editor);
            DrawAddedGlyph(editor);
            DrawRemovedGlyph();
        }

        private void DrawAddedGlyph(EditorLayout editor)
        {
            if (!anim.TryAddFlight(out float progress, out Vector2 from, out int index)
                || index >= editor.SocketCount)
            {
                return;
            }

            SpellGlyphDef glyph = GlyphDefAt(index);
            Texture2D icon = SpellbookStyle.GlyphIcon(glyph);
            if (icon == null)
            {
                return;
            }

            Vector2 to = editor.SocketRect(editor.BeadRect(index)).center;
            float eased = SpellbookAnim.EaseOut(progress);
            float arcLift = Mathf.Sin(progress * Mathf.PI) * 26f;
            Vector2 position = new Vector2(
                Mathf.Lerp(from.x, to.x, eased),
                Mathf.Lerp(from.y, to.y, eased) - arcLift);
            float scale = 1.25f - 0.25f * eased;
            float size = editor.SocketSize * scale;
            Color kindColor = glyph != null ? SpellbookStyle.KindColor(glyph.kind) : SpellbookStyle.JadeBright;

            SpellbookStyle.DrawGlow(position, size * 1.75f, kindColor, 0.50f * (1f - 0.3f * progress));
            BookUi.DrawIcon(new Rect(position.x - size * 0.5f, position.y - size * 0.5f, size, size), icon, Color.white);
        }

        private void DrawAddedLanding(EditorLayout editor)
        {
            if (!anim.TryAddLanding(out float progress, out int index)
                || index >= editor.SocketCount)
            {
                return;
            }

            SpellGlyphDef glyph = GlyphDefAt(index);
            if (glyph == null)
            {
                return;
            }

            Rect socket = editor.SocketRect(editor.BeadRect(index));
            float eased = SpellbookAnim.EaseOut(progress);
            float haloSize = socket.width * Mathf.Lerp(1.1f, 2.35f, eased);
            Color kindColor = SpellbookStyle.KindColor(glyph.kind);
            SpellbookStyle.DrawGlow(socket.center, haloSize, kindColor, 0.65f * (1f - eased));
        }

        private void DrawRemovedGlyph()
        {
            if (!anim.TryRemove(out float progress, out Rect socket, out string defName))
            {
                return;
            }

            float eased = SpellbookAnim.EaseOut(progress);
            float size = socket.width * (1f - 0.40f * eased);
            Rect iconRect = new Rect(socket.center.x - size * 0.5f, socket.center.y - size * 0.5f, size, size);

            Texture2D icon = SpellbookStyle.GlyphIcon(defName.NullOrEmpty() ? null : SpellDefs.Glyph(defName));
            if (icon != null)
            {
                BookUi.DrawIcon(iconRect, icon, new Color(1f, 1f, 1f, 1f - eased));
            }
        }

        private void DrawCreateRipple(EditorLayout editor)
        {
            int count = Mathf.Min(chain.glyphs.Count, editor.SocketCount);
            for (int i = 0; i < count; i++)
            {
                if (!anim.TryBeadRipple(i, out float progress))
                {
                    continue;
                }

                SpellGlyphDef glyph = GlyphDefAt(i);
                Color color = glyph != null ? SpellbookStyle.KindColor(glyph.kind) : SpellbookStyle.JadeBright;
                Rect socket = editor.SocketRect(editor.BeadRect(i));
                float pulse = SpellbookAnim.Pulse(progress);
                float haloSize = socket.width * Mathf.Lerp(1.25f, 2.25f, SpellbookAnim.EaseOut(progress));
                SpellbookStyle.DrawGlow(socket.center, haloSize, color, 0.62f * pulse);
            }
        }

        private void DrawPageCrease(Rect rightPage)
        {
            if (!anim.TryPageCrease(out float progress))
            {
                return;
            }

            float eased = SpellbookAnim.EaseOut(progress);
            float width = Mathf.Lerp(54f, 14f, eased);
            float alpha = 0.22f * (1f - eased);
            BookUi.DrawStretched(
                new Rect(rightPage.x, rightPage.y, width, rightPage.height),
                BookUi.SpineShade,
                new Color(1f, 1f, 1f, alpha));
        }
    }
}
