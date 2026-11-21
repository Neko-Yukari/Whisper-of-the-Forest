using System.Collections.Generic;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Left page: searchable, category-filtered rune codex with 2-column Birch-Vellum cards
    /// (semantic color strip, icon, name, mana/locked badge, numeric micro-summary) and a fixed
    /// bottom rune inspector card synchronized with hovers on either page.
    /// </summary>
    public partial class Window_Spellbook
    {
        private readonly List<SpellGlyphDef> visibleGlyphs = new List<SpellGlyphDef>();
        private readonly int[] visibleKindCounts = new int[CatalogLayout.Kinds.Length];

        private SpellGlyphDef catalogHover;
        private string blockedTip;
        private Vector2 blockedTipAt;

        private void DrawCatalogPage(Rect page, out CatalogLayout layout)
        {
            layout = CatalogLayout.Compute(page);

            SpellbookStyle.DrawPageTitle(layout.Title, layout.Divider, T("WotF_Spellbook.PageCatalog"));
            search = SpellbookStyle.TextField(layout.Search, search, T("WotF_Spellbook.SearchPlaceholder"),
                SpellbookStyle.IconSearch);

            blockedTip = null;
            catalogHover = null;
            DrawCategoryRow(layout.Categories);
            DrawCatalogList(layout.List);
        }

        private void DrawCategoryRow(Rect rect)
        {
            CategoryButton(CatalogLayout.CategoryTab(rect, 0), null, "WotF_Spellbook.CategoryAll");
            for (int i = 0; i < CatalogLayout.Kinds.Length; i++)
            {
                SpellGlyphKind kind = CatalogLayout.Kinds[i];
                CategoryButton(CatalogLayout.CategoryTab(rect, i + 1), kind, "WotF_Spellbook.Category" + kind);
            }
        }

        private void CategoryButton(Rect rect, SpellGlyphKind? kind, string labelKey)
        {
            bool selected = category == kind;
            bool over = Mouse.IsOver(rect);
            Color kindColor = kind.HasValue ? SpellbookStyle.KindColor(kind.Value) : SpellbookStyle.Jade;

            if (selected)
            {
                BookUi.DrawBox(rect, kindColor, BookUi.Darken(kindColor, 0.25f), 1);
            }
            else
            {
                Color bg = over ? BookUi.CardVellumHover : BookUi.CardVellumNormal;
                BookUi.DrawBox(rect, bg, BookUi.Fade(kindColor, over ? 0.85f : 0.48f), 1);
            }

            BookUi.DrawLabel(rect.ContractedBy(2f, 0f), T(labelKey), GameFont.Tiny,
                selected ? BookUi.SealText : kindColor, TextAnchor.MiddleCenter, ellipsis: true);

            if (Widgets.ButtonInvisible(rect, true) && category != kind)
            {
                category = kind;
                catalogScroll = Vector2.zero;
            }
        }

        private void DrawCatalogList(Rect rect)
        {
            CollectVisibleGlyphs();

            if (visibleGlyphs.Count == 0)
            {
                string message = SpellGlyphDefsAvailable()
                    ? T("WotF_Spellbook.CatalogEmpty")
                    : T("WotF_Spellbook.CatalogMissingDefs");
                SpellbookStyle.DrawParagraph(rect, message, GameFont.Small, SpellbookStyle.InkDim);
                return;
            }

            float width = rect.width - CatalogLayout.ScrollBarWidth;
            float viewHeight = LayoutCatalog(width, false, Vector2.zero);
            Rect viewRect = new Rect(0f, 0f, width, Mathf.Max(viewHeight, rect.height));
            Vector2 listOrigin = new Vector2(rect.x - catalogScroll.x, rect.y - catalogScroll.y);
            Widgets.BeginScrollView(rect, ref catalogScroll, viewRect, true);
            LayoutCatalog(width, true, listOrigin);
            Widgets.EndScrollView();
        }

        private float LayoutCatalog(float width, bool draw, Vector2 listOrigin)
        {
            int columns = CatalogLayout.CardColumns(width);
            float cardWidth = Mathf.Floor((width - (columns - 1) * CatalogLayout.CardGap) / columns);
            float rowStep = CatalogLayout.CardHeight + CatalogLayout.CardGap;
            bool grouped = category == null;

            float y = 0f;
            int column = 0;
            SpellGlyphKind? group = null;
            for (int i = 0; i < visibleGlyphs.Count; i++)
            {
                SpellGlyphDef glyph = visibleGlyphs[i];
                if (grouped && group != glyph.kind)
                {
                    if (column != 0)
                    {
                        y += rowStep;
                        column = 0;
                    }

                    if (draw)
                    {
                        DrawGroupHeading(new Rect(0f, y, width, CatalogLayout.SectionHeight), glyph.kind);
                    }

                    y += CatalogLayout.SectionHeight;
                    group = glyph.kind;
                }

                if (draw)
                {
                    float x = column * (cardWidth + CatalogLayout.CardGap);
                    float w = column == columns - 1 ? width - x : cardWidth;
                    DrawCatalogCard(new Rect(x, y, w, CatalogLayout.CardHeight), glyph, listOrigin);
                }

                column++;
                if (column == columns)
                {
                    column = 0;
                    y += rowStep;
                }
            }

            if (column != 0)
            {
                y += rowStep;
            }

            return y;
        }

        private void DrawGroupHeading(Rect rect, SpellGlyphKind kind)
        {
            Color color = SpellbookStyle.KindColor(kind);
            string text = SpellbookStyle.KindLabel(kind) + " · " + visibleKindCounts[(int)kind];
            float textWidth = BookUi.CalcTextWidth(text, GameFont.Tiny);
            BookUi.DrawLabel(new Rect(rect.x + 2f, rect.y, textWidth + 6f, rect.height - 2f),
                text, GameFont.Tiny, color, TextAnchor.LowerLeft, ellipsis: false);
            float lineX = rect.x + textWidth + 10f;
            if (rect.xMax - lineX > 8f)
            {
                BookUi.DrawHairline(lineX, rect.yMax - 8f, rect.xMax - lineX - 2f);
            }
        }

        private void DrawCatalogCard(Rect card, SpellGlyphDef glyph, Vector2 listOrigin)
        {
            bool unlocked = GameComponent_Spellbook.IsUnlocked(glyph);
            bool appendable = ValidateCatalogGlyph(glyph, out string appendReason);
            bool over = Mouse.IsOver(card);
            float alpha = appendable ? 1f : SpellbookStyle.BlockedAlpha;
            Color kindColor = SpellbookStyle.KindColor(glyph.kind);

            // Birch-Vellum card surface matching Ritual codex cards
            Color bg = !appendable
                ? (over ? BookUi.CardVellumLockedSel : BookUi.CardVellumLocked)
                : (over ? BookUi.CardVellumHover : BookUi.CardVellumNormal);
            Color border = over && appendable
                ? kindColor
                : (over ? BookUi.LedgerBorder : BookUi.SubtleRule);
            BookUi.DrawBox(card, bg, border, 1);

            // Semantic left color bar
            Widgets.DrawBoxSolid(
                new Rect(card.x + 1f, card.y + 3f, 3f, card.height - 6f),
                BookUi.Fade(kindColor, appendable ? 0.92f : 0.40f));

            const float socketSize = 34f;
            Rect socket = new Rect(card.x + 7f, card.y + (card.height - socketSize) * 0.5f, socketSize, socketSize);
            if (over && appendable)
            {
                SpellbookStyle.DrawGlow(socket.center, socketSize * 1.65f, kindColor, 0.45f);
            }

            Texture2D icon = SpellbookStyle.GlyphIcon(glyph);
            SpellbookStyle.DrawSocket(socket, icon, alpha);
            if (icon == null)
            {
                BookUi.DrawLabel(socket, "?", GameFont.Small, BookUi.Fade(BookUi.RubricRed, alpha),
                    TextAnchor.MiddleCenter, ellipsis: false);
            }

            float textX = socket.xMax + 5f;
            float rightPad = 5f;
            float topY = card.y + 4f;

            // Right-aligned mana / locked pill on row 1
            float badgeW = 0f;
            if (!unlocked)
            {
                string lockText = T("WotF_Spellbook.BadgeLocked");
                badgeW = Mathf.Min(48f, Mathf.Ceil(BookUi.CalcTextWidth(lockText, GameFont.Tiny)) + 8f);
                Rect badgeRect = new Rect(card.xMax - rightPad - badgeW, topY + 2f, badgeW, 16f);
                BookUi.DrawPillBadge(badgeRect, lockText,
                    new Color(0.28f, 0.24f, 0.20f, 0.82f), BookUi.SubtleRule, BookUi.SealText);
            }
            else if (glyph.manaCost > 0f)
            {
                string manaText = glyph.manaCost.ToString("0");
                badgeW = Mathf.Max(22f, Mathf.Ceil(BookUi.CalcTextWidth(manaText, GameFont.Tiny)) + 8f);
                Rect badgeRect = new Rect(card.xMax - rightPad - badgeW, topY + 2f, badgeW, 16f);
                BookUi.DrawPillBadge(badgeRect, manaText,
                    BookUi.Fade(BookUi.ChipManaBg, appendable ? 1f : 0.55f),
                    BookUi.Fade(BookUi.ChipManaBorder, alpha),
                    BookUi.Fade(BookUi.CrystalBlue, alpha));
            }

            float nameW = Mathf.Max(20f, card.xMax - rightPad - textX - (badgeW > 0f ? badgeW + 3f : 0f));
            BookUi.DrawLabel(new Rect(textX, topY, nameW, 20f), glyph.LabelCap, GameFont.Small,
                BookUi.Fade(BookUi.InkPrimary, alpha), TextAnchor.MiddleLeft, ellipsis: true);

            // Row 2: key numeric micro-summary
            string micro = SpellbookStyle.GlyphMicroSummary(glyph);
            BookUi.DrawLabel(new Rect(textX, card.y + 25f, card.xMax - rightPad - textX, 18f),
                micro, GameFont.Tiny, BookUi.Fade(BookUi.InkSecondary, alpha), TextAnchor.MiddleLeft, ellipsis: true);

            if (over)
            {
                catalogHover = glyph;
                lastInspectedGlyph = glyph;
                if (!appendable)
                {
                    blockedTip = appendReason;
                    blockedTipAt = listOrigin + (Event.current != null ? Event.current.mousePosition : card.center);
                }
            }

            if (Widgets.ButtonInvisible(card, true))
            {
                if (!appendable)
                {
                    anim.Nudge();
                    SetStatus(appendReason, true);
                    return;
                }

                Vector2 mouse = Event.current != null ? Event.current.mousePosition : card.center;
                AddGlyphFromCatalog(glyph, listOrigin + mouse);
            }
        }

        /// <summary>
        /// Fixed bottom rune inspector card on the left page. Refreshes live on hovering either a
        /// left-page catalogue card or a right-page rune-chain bead, and holds the last inspected
        /// glyph when the cursor moves away so its description and compatibility stay readable.
        /// </summary>
        private void DrawInspectorCard(Rect rect)
        {
            if (rect.height < 40f)
            {
                return;
            }

            BookUi.DrawBox(rect, BookUi.LedgerBg, BookUi.LedgerBorder, 1);
            Rect inner = rect.ContractedBy(8f, 6f);

            SpellGlyphDef glyph = catalogHover ?? GlyphDefAt(hoverGlyph) ?? lastInspectedGlyph;
            if (glyph == null)
            {
                BookUi.DrawLabel(new Rect(inner.x, inner.y, inner.width, 18f),
                    T("WotF_Spellbook.InspectorTitle"), GameFont.Tiny, BookUi.InkMuted, TextAnchor.MiddleLeft);
                BookUi.DrawHairline(inner.x, inner.y + 19f, inner.width);
                BookUi.DrawWrappedLabel(new Rect(inner.x, inner.y + 24f, inner.width, Mathf.Max(0f, inner.height - 24f)),
                    T("WotF_Spellbook.InspectorEmpty"), GameFont.Tiny, BookUi.InkSecondary);
                return;
            }

            Color kindColor = SpellbookStyle.KindColor(glyph.kind);
            const float socketSize = 32f;
            Rect socket = new Rect(inner.x, inner.y, socketSize, socketSize);
            SpellbookStyle.DrawSocket(socket, SpellbookStyle.GlyphIcon(glyph), 1f);

            float textX = socket.xMax + 7f;
            float rightX = inner.xMax;

            // Right-aligned cost + timing readout on top row
            int totalTicks = glyph.windupTicks + glyph.channelTicks + glyph.recoveryTicks;
            string meta = BuildGlyphMetaString(glyph, totalTicks);
            float metaW = meta.NullOrEmpty() ? 0f : Mathf.Min(inner.width * 0.48f, Mathf.Ceil(BookUi.CalcTextWidth(meta, GameFont.Tiny)) + 4f);
            if (metaW > 0f)
            {
                Rect metaRect = new Rect(rightX - metaW, inner.y, metaW, 18f);
                BookUi.DrawLabel(metaRect, meta, GameFont.Tiny, BookUi.CrystalBlue, TextAnchor.MiddleRight, ellipsis: true);
                TooltipHandler.TipRegion(metaRect, TF("WotF_Spellbook.GlyphTiming",
                    SpellbookStyle.Seconds(glyph.windupTicks),
                    SpellbookStyle.Seconds(glyph.channelTicks),
                    SpellbookStyle.Seconds(glyph.recoveryTicks)));
            }

            // Glyph name + kind pill
            string kindLabel = SpellbookStyle.KindLabel(glyph.kind);
            float kindW = Mathf.Ceil(BookUi.CalcTextWidth(kindLabel, GameFont.Tiny)) + 10f;
            float nameMaxW = Mathf.Max(30f, rightX - metaW - (metaW > 0f ? 6f : 0f) - textX - kindW - 6f);
            float nameW = Mathf.Min(nameMaxW, Mathf.Ceil(BookUi.CalcTextWidth(glyph.LabelCap, GameFont.Small)) + 4f);
            BookUi.DrawLabel(new Rect(textX, inner.y - 1f, nameW, 19f), glyph.LabelCap, GameFont.Small,
                BookUi.InkPrimary, TextAnchor.MiddleLeft, ellipsis: true);
            BookUi.DrawPillBadge(new Rect(textX + nameW + 5f, inner.y + 1f, kindW, 15f),
                kindLabel, BookUi.Fade(kindColor, 0.16f), BookUi.Fade(kindColor, 0.72f), kindColor);

            // Second sub-line beside icon: raw numeric summary
            string micro = SpellbookStyle.GlyphMicroSummary(glyph);
            BookUi.DrawLabel(new Rect(textX, inner.y + 18f, rightX - textX, 15f),
                micro, GameFont.Tiny, BookUi.InkSecondary, TextAnchor.MiddleLeft, ellipsis: true);

            float y = socket.yMax + 4f;

            // Compatibility / exclusion line
            string compatLine = BuildCompatibilityLine(glyph);
            if (!compatLine.NullOrEmpty())
            {
                Rect compatRect = new Rect(inner.x, y, inner.width, 16f);
                BookUi.DrawLabel(compatRect, compatLine, GameFont.Tiny, BookUi.InkMuted, TextAnchor.MiddleLeft, ellipsis: true);
                TooltipHandler.TipRegion(compatRect, compatLine);
                y += 17f;
            }

            // Description body
            float descH = Mathf.Max(14f, inner.yMax - y);
            Rect descRect = new Rect(inner.x, y, inner.width, descH);
            string desc = glyph.description ?? string.Empty;
            BookUi.DrawWrappedLabel(descRect, desc, GameFont.Tiny, BookUi.InkSecondary);
            if (!desc.NullOrEmpty() && BookUi.CalcTextHeight(desc, GameFont.Tiny, inner.width) > descH + 2f)
            {
                TooltipHandler.TipRegion(descRect, desc);
            }
        }

        private static string BuildGlyphMetaString(SpellGlyphDef glyph, int totalTicks)
        {
            var parts = new List<string>(3);
            if (glyph.manaCost > 0f)
            {
                parts.Add(TF("WotF_Spellbook.MetaMana", glyph.manaCost.ToString("0.#")));
            }

            if (glyph.entropyCost > 0f)
            {
                parts.Add(TF("WotF_Spellbook.MetaEntropy", glyph.entropyCost.ToString("0.#")));
            }

            if (totalTicks > 0)
            {
                parts.Add(TF("WotF_Spellbook.MetaTime", SpellbookStyle.Seconds(totalTicks)));
            }

            return string.Join(" · ", parts);
        }

        private static string BuildCompatibilityLine(SpellGlyphDef glyph)
        {
            if (glyph.compatibleAugments != null && glyph.compatibleAugments.Count > 0)
            {
                var names = new List<string>(glyph.compatibleAugments.Count);
                for (int i = 0; i < glyph.compatibleAugments.Count; i++)
                {
                    SpellGlyphDef aug = SpellDefs.Glyph(glyph.compatibleAugments[i]);
                    names.Add(aug != null ? aug.LabelCap.ToString() : glyph.compatibleAugments[i]);
                }

                return TF("WotF_Spellbook.CompatAugments", string.Join(" / ", names));
            }

            if (glyph.excludesAugments != null && glyph.excludesAugments.Count > 0)
            {
                var names = new List<string>(glyph.excludesAugments.Count);
                for (int i = 0; i < glyph.excludesAugments.Count; i++)
                {
                    SpellGlyphDef aug = SpellDefs.Glyph(glyph.excludesAugments[i]);
                    names.Add(aug != null ? aug.LabelCap.ToString() : glyph.excludesAugments[i]);
                }

                return TF("WotF_Spellbook.ExcludesAugments", string.Join(" / ", names));
            }

            return glyph.kind == SpellGlyphKind.Form || glyph.kind == SpellGlyphKind.Effect
                ? T("WotF_Spellbook.NoCompatibleAugments")
                : string.Empty;
        }

        private void DrawBlockedTooltip(Rect bounds)
        {
            if (blockedTip.NullOrEmpty() || bounds.width <= 0f)
            {
                return;
            }

            float boxWidth = Mathf.Min(272f, bounds.width);
            float textWidth = boxWidth - 12f;
            float height = BookUi.CalcTextHeight(blockedTip, GameFont.Tiny, textWidth) + 14f;

            float x = Mathf.Clamp(blockedTipAt.x + 16f, bounds.x, Mathf.Max(bounds.x, bounds.xMax - boxWidth));
            float y = Mathf.Clamp(blockedTipAt.y + 16f, bounds.y, Mathf.Max(bounds.y, bounds.yMax - height));
            Rect box = new Rect(x, y, boxWidth, height);
            Widgets.DrawBoxSolid(box, new Color(0.10f, 0.08f, 0.06f, 0.96f));
            Widgets.DrawBoxSolidWithOutline(box, Color.clear, BookUi.CoverWarning, 1);
            SpellbookStyle.DrawParagraph(box.ContractedBy(6f), blockedTip, GameFont.Tiny, BookUi.CoverWarning);
        }

        private void AddGlyphFromCatalog(SpellGlyphDef glyph, Vector2 fromWindowPoint)
        {
            if (glyph == null)
            {
                return;
            }

            chain.Add(glyph.defName);
            cachedPlanKey = null;
            anim.OnAdd(fromWindowPoint, chain.glyphs.Count - 1);
            SetStatus(TF("WotF_Spellbook.GlyphAdded", glyph.LabelCap), false);
        }

        private void CollectVisibleGlyphs()
        {
            visibleGlyphs.Clear();
            for (int i = 0; i < visibleKindCounts.Length; i++)
            {
                visibleKindCounts[i] = 0;
            }

            List<SpellGlyphDef> all = DefDatabase<SpellGlyphDef>.AllDefsListForReading;
            if (all == null)
            {
                return;
            }

            string filter = search == null ? "" : search.Trim();
            for (int i = 0; i < all.Count; i++)
            {
                SpellGlyphDef glyph = all[i];
                if (glyph == null)
                {
                    continue;
                }

                if (category != null && glyph.kind != category.Value)
                {
                    continue;
                }

                if (!filter.NullOrEmpty() && !Matches(glyph, filter))
                {
                    continue;
                }

                visibleGlyphs.Add(glyph);
                visibleKindCounts[(int)glyph.kind]++;
            }

            visibleGlyphs.Sort(CompareGlyphs);
        }

        private static bool Matches(SpellGlyphDef glyph, string filter)
        {
            if (glyph.defName != null && glyph.defName.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            string label = glyph.LabelCap;
            return label != null && label.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CompareGlyphs(SpellGlyphDef left, SpellGlyphDef right)
        {
            int byKind = left.kind.CompareTo(right.kind);
            if (byKind != 0)
            {
                return byKind;
            }

            return string.Compare(left.LabelCap, right.LabelCap, System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool SpellGlyphDefsAvailable()
        {
            List<SpellGlyphDef> all = DefDatabase<SpellGlyphDef>.AllDefsListForReading;
            return all != null && all.Count > 0;
        }
    }
}
