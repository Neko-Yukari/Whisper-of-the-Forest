using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Left page: searchable, category-filtered rune catalogue. Runes are cards in one or two
    /// columns; under "All" they are grouped by kind with a small heading per group.
    /// </summary>
    public partial class Window_Spellbook
    {
        private readonly List<SpellGlyphDef> visibleGlyphs = new List<SpellGlyphDef>();

        /// <summary>Visible runes per <see cref="SpellGlyphKind"/>, for the group headings.</summary>
        private readonly int[] visibleKindCounts = new int[CatalogLayout.Kinds.Length];

        /// <summary>Catalogue rune under the mouse this frame; the detail panel describes it.</summary>
        private SpellGlyphDef catalogHover;

        // The hovered rune the bar cannot take, and where the mouse was (window space). Drawn as a
        // custom tooltip because the vanilla one has a fixed text colour.
        private string blockedTip;
        private Vector2 blockedTipAt;

        private void DrawCatalogPage(Rect page)
        {
            SpellbookStyle.DrawPage(page, true);
            CatalogLayout layout = CatalogLayout.Compute(page);

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
                SpellbookStyle.DrawAtlas(rect, SpellbookStyle.ButtonAtlas, kindColor);
            }
            else
            {
                SpellbookStyle.DrawPanel(rect, SpellbookStyle.Fade(kindColor, over ? 0.9f : 0.5f));
            }

            SpellbookStyle.DrawLabel(rect, T(labelKey), GameFont.Small,
                selected ? SpellbookStyle.CoverText : kindColor, TextAnchor.MiddleCenter);

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
            // Scroll-view coordinates are offset by the list's origin and its scroll position; a
            // fly-in animation and the refusal tooltip are drawn in window coordinates, so hand the
            // cards the delta.
            Vector2 listOrigin = new Vector2(rect.x - catalogScroll.x, rect.y - catalogScroll.y);
            Widgets.BeginScrollView(rect, ref catalogScroll, viewRect, true);
            LayoutCatalog(width, true, listOrigin);
            Widgets.EndScrollView();
        }

        /// <summary>
        /// Walks the visible runes in display order, placing group headings (under "All") and cards;
        /// draws them when <paramref name="draw"/> is set. Returns the total height, so measuring
        /// and drawing can never disagree.
        /// </summary>
        private float LayoutCatalog(float width, bool draw, Vector2 listOrigin)
        {
            int columns = CatalogLayout.CardColumns(width);
            float cardWidth = (width - (columns - 1) * CatalogLayout.CardGap) / columns;
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
                    DrawCatalogCard(new Rect(column * (cardWidth + CatalogLayout.CardGap), y, cardWidth,
                        CatalogLayout.CardHeight), glyph, listOrigin);
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
            float textWidth = SpellbookStyle.TextWidth(text, GameFont.Tiny);
            SpellbookStyle.DrawLabel(new Rect(rect.x + 2f, rect.y, rect.width - 2f, rect.height - 4f), text, GameFont.Tiny,
                color, TextAnchor.LowerLeft);
            float lineX = rect.x + textWidth + 10f;
            if (rect.xMax - lineX > 8f)
            {
                Widgets.DrawLineHorizontal(lineX, rect.yMax - 11f, rect.xMax - lineX - 2f, SpellbookStyle.Fade(color, 0.45f));
            }
        }

        private void DrawCatalogCard(Rect card, SpellGlyphDef glyph, Vector2 listOrigin)
        {
            // The shared compiler is the only place that decides whether a rune may follow this bar;
            // the page never re-implements the grammar.
            bool appendable = SpellCompiler.CanAppend(chain.glyphs, glyph, out string appendReason);
            bool over = Mouse.IsOver(card);
            float alpha = appendable ? 1f : SpellbookStyle.BlockedAlpha;
            Color kindColor = SpellbookStyle.KindColor(glyph.kind);

            SpellbookStyle.DrawPanel(card, over && appendable
                ? SpellbookStyle.Fade(kindColor, 0.95f)
                : SpellbookStyle.Fade(SpellbookStyle.Ink, 0.30f * alpha));

            const float socketSize = 36f;
            Rect socket = new Rect(card.x + 4f, card.y + (card.height - socketSize) * 0.5f, socketSize, socketSize);
            if (over && appendable)
            {
                SpellbookStyle.DrawGlow(socket.center, socketSize * 1.8f, kindColor, 0.6f);
            }

            Texture2D icon = SpellbookStyle.GlyphIcon(glyph);
            SpellbookStyle.DrawSocket(socket, icon, alpha);
            if (icon == null)
            {
                SpellbookStyle.DrawLabel(socket, "?", GameFont.Small, SpellbookStyle.Fade(SpellbookStyle.Warning, alpha),
                    TextAnchor.MiddleCenter);
            }

            float textX = socket.xMax + 6f;
            float textWidth = card.xMax - textX - 4f;
            SpellbookStyle.DrawLabel(new Rect(textX, card.y + 4f, textWidth, 20f), glyph.LabelCap, GameFont.Small,
                SpellbookStyle.Fade(SpellbookStyle.Ink, alpha));
            SpellbookStyle.DrawKindTag(new Rect(textX, card.y + 24f, textWidth, 14f), glyph.kind, alpha);

            if (over)
            {
                catalogHover = glyph;
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
                    // Refused rune: reuse the bar's shake/flash, append nothing.
                    anim.Shake();
                    SetStatus(appendReason, true);
                    return;
                }

                Vector2 mouse = Event.current != null ? Event.current.mousePosition : card.center;
                AddGlyphFromCatalog(glyph, listOrigin + mouse);
            }
        }

        /// <summary>
        /// Red reason for the rune under the mouse. The vanilla tooltip draws its text in one fixed
        /// colour, so the refusal gets its own small box, drawn last and clamped inside the window.
        /// </summary>
        private void DrawBlockedTooltip(Rect bounds)
        {
            if (blockedTip.NullOrEmpty() || bounds.width <= 0f)
            {
                return;
            }

            float boxWidth = Mathf.Min(272f, bounds.width);
            float textWidth = boxWidth - 12f;
            float height;
            using (TechLabDrawScope.Begin())
            {
                Text.Font = GameFont.Tiny;
                Text.WordWrap = true;
                height = Text.CalcHeight(blockedTip, textWidth) + 14f;
            }

            float x = Mathf.Clamp(blockedTipAt.x + 16f, bounds.x, Mathf.Max(bounds.x, bounds.xMax - boxWidth));
            float y = Mathf.Clamp(blockedTipAt.y + 16f, bounds.y, Mathf.Max(bounds.y, bounds.yMax - height));
            Rect box = new Rect(x, y, boxWidth, height);
            Widgets.DrawBoxSolid(box, new Color(0.10f, 0.08f, 0.06f, 0.96f));
            Widgets.DrawBoxSolidWithOutline(box, Color.clear, SpellbookStyle.CoverWarning, 1);
            SpellbookStyle.DrawParagraph(box.ContractedBy(6f), blockedTip, GameFont.Tiny, SpellbookStyle.CoverWarning);
        }

        /// <summary>
        /// One click appends the glyph to the bar; the caller has already asked
        /// <see cref="SpellCompiler.CanAppend"/> whether that is allowed.
        /// </summary>
        private void AddGlyphFromCatalog(SpellGlyphDef glyph, Vector2 fromWindowPoint)
        {
            if (glyph == null)
            {
                return;
            }

            // The flight starts where the click happened, so the eye follows it into the bar.
            chain.Add(glyph.defName);
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
