using System;
using UnityEngine;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest
{
    /// <summary>Rectangle helper that avoids the Verse extension methods (keeps layout Unity-only).</summary>
    internal static class SpellbookRect
    {
        public static Rect Inset(Rect rect, float amount)
        {
            return new Rect(rect.x + amount, rect.y + amount, rect.width - 2f * amount, rect.height - 2f * amount);
        }
    }

    /// <summary>
    /// Pure geometry for the book window: which rectangle each region gets for a given content
    /// size. No game state and no drawing, so every rectangle a click lands on is computed in one
    /// place and the animations can ask for the same rectangles the page used.
    ///
    /// The window has no margin of its own: the cover fills it, the brass frame sits
    /// <see cref="FrameInset"/> inside, and the two pages, the bookmark ribbons, the header and the
    /// status line are laid out inside that frame.
    /// </summary>
    internal readonly struct SpellbookLayout
    {
        public const float FrameInset = 12f;
        public const float HeaderHeight = 30f;
        public const float FooterHeight = 24f;
        public const float BodyGap = 4f;
        public const float SideInset = 10f;
        public const float MinBodyHeight = 140f;

        /// <summary>Visible cover between the pages: the spine.</summary>
        public const float PageGap = 10f;

        public const float LeftPageRatio = 0.44f;

        /// <summary>Content inset inside a page; keeps text off the vine frame's rules.</summary>
        public const float PageInset = 20f;

        public const float BookmarkWidth = 50f;

        /// <summary>Part of a ribbon that lies on the right page's margin, as if tucked into the book.</summary>
        public const float BookmarkOverlap = 10f;

        /// <summary>Ribbons stay clear of the page frame's corner ornaments.</summary>
        public const float BookmarkEndInset = 44f;

        public const float BeadWidth = 48f;
        public const float BeadHeight = 58f;
        public const float BeadGap = 4f;
        public const float BeadRowGap = 2f;
        public const float SocketSize = 44f;

        public const float PageTitleHeight = 26f;
        public const float PageDividerHeight = 12f;
        public const float PageHeadStep = 42f;

        public readonly Rect Header;
        public readonly Rect LeftPage;
        public readonly Rect RightPage;
        public readonly Rect Bookmarks;
        public readonly Rect Footer;

        private SpellbookLayout(Rect header, Rect leftPage, Rect rightPage, Rect bookmarks, Rect footer)
        {
            Header = header;
            LeftPage = leftPage;
            RightPage = rightPage;
            Bookmarks = bookmarks;
            Footer = footer;
        }

        public static SpellbookLayout Compute(Rect content)
        {
            Rect inner = SpellbookRect.Inset(content, FrameInset);

            Rect header = new Rect(inner.x + SideInset, inner.y, inner.width - 2f * SideInset, HeaderHeight);
            Rect footer = new Rect(inner.x + SideInset, inner.yMax - FooterHeight, inner.width - 2f * SideInset,
                FooterHeight);

            float bodyTop = header.yMax + BodyGap;
            float bodyHeight = footer.y - BodyGap - bodyTop;
            if (bodyHeight < MinBodyHeight)
            {
                bodyHeight = MinBodyHeight;
            }

            float bookWidth = inner.width - (BookmarkWidth - BookmarkOverlap);
            float leftWidth = Mathf.Round((bookWidth - PageGap) * LeftPageRatio);

            Rect leftPage = new Rect(inner.x, bodyTop, leftWidth, bodyHeight);
            Rect rightPage = new Rect(leftPage.xMax + PageGap, bodyTop, bookWidth - leftWidth - PageGap, bodyHeight);
            float ribbonInset = Mathf.Min(BookmarkEndInset, bodyHeight * 0.1f);
            Rect bookmarks = new Rect(rightPage.xMax - BookmarkOverlap, bodyTop + ribbonInset, BookmarkWidth,
                bodyHeight - 2f * ribbonInset);
            return new SpellbookLayout(header, leftPage, rightPage, bookmarks, footer);
        }

        /// <summary>Title and leaf divider at the top of a page's content area; returns the y below them.</summary>
        public static float PageHead(Rect inner, out Rect title, out Rect divider)
        {
            title = new Rect(inner.x, inner.y, inner.width, PageTitleHeight);
            divider = new Rect(inner.x + inner.width * 0.12f, title.yMax + 1f, inner.width * 0.76f, PageDividerHeight);
            return inner.y + PageHeadStep;
        }

        /// <summary>One bookmark ribbon's slot in the strip; tabs share the strip evenly.</summary>
        public static Rect BookmarkTab(Rect strip, int index, int count)
        {
            float tabHeight = strip.height / count;
            return new Rect(strip.x, strip.y + index * tabHeight, strip.width, Mathf.Max(18f, tabHeight - 3f));
        }
    }

    /// <summary>
    /// Vertical bands of the right page. The bar shows one socket per rune the tuning allows, empty
    /// ones included, and wraps row by row, so ten runes still fit when the window is narrow.
    /// </summary>
    internal readonly struct EditorLayout
    {
        public const float NameCaptionWidth = 84f;
        public const float NameRowHeight = 26f;
        public const float NameRowStep = 32f;
        public const float ChainCaptionHeight = 18f;
        public const float ChainCaptionStep = 20f;
        public const float ChainStepGap = 6f;
        public const float ActionRowHeight = 28f;
        public const float CastRowHeight = 32f;
        public const float ValidationHeight = 46f;
        public const float RowGap = 6f;

        public readonly Rect Title;
        public readonly Rect Divider;
        public readonly Rect NameCaption;
        public readonly Rect NameField;
        public readonly Rect ChainCaption;
        public readonly Rect Chain;
        public readonly Rect Detail;
        public readonly Rect Validation;
        public readonly Rect ActionRow;
        public readonly Rect CastRow;
        public readonly int BeadColumns;
        public readonly int BeadRows;

        private EditorLayout(Rect title, Rect divider, Rect nameCaption, Rect nameField, Rect chainCaption, Rect chain,
            Rect detail, Rect validation, Rect actionRow, Rect castRow, int beadColumns, int beadRows)
        {
            Title = title;
            Divider = divider;
            NameCaption = nameCaption;
            NameField = nameField;
            ChainCaption = chainCaption;
            Chain = chain;
            Detail = detail;
            Validation = validation;
            ActionRow = actionRow;
            CastRow = castRow;
            BeadColumns = beadColumns;
            BeadRows = beadRows;
        }

        /// <summary>Columns that fit one bead row at this page width; never below one.</summary>
        public static int ColumnsFor(float width)
        {
            int columns = (int)((width + SpellbookLayout.BeadGap) / (SpellbookLayout.BeadWidth + SpellbookLayout.BeadGap));
            return columns < 1 ? 1 : columns;
        }

        public static int RowsFor(int beadCount, int columns)
        {
            if (beadCount <= 0)
            {
                return 1;
            }

            int rows = (beadCount + columns - 1) / columns;
            return rows < 1 ? 1 : rows;
        }

        public static float ChainHeightFor(int rows)
        {
            return rows * SpellbookLayout.BeadHeight + (rows - 1) * SpellbookLayout.BeadRowGap;
        }

        /// <param name="socketCount">Sockets the bar shows: the tuning's rune limit, or more if a bar holds more.</param>
        public static EditorLayout Compute(Rect page, int socketCount)
        {
            Rect inner = SpellbookRect.Inset(page, SpellbookLayout.PageInset);
            float y = SpellbookLayout.PageHead(inner, out Rect title, out Rect divider);

            Rect nameCaption = new Rect(inner.x, y, NameCaptionWidth, NameRowHeight);
            Rect nameField = new Rect(inner.x + NameCaptionWidth + 4f, y, inner.width - NameCaptionWidth - 4f,
                NameRowHeight);
            y += NameRowStep;

            Rect chainCaption = new Rect(inner.x, y, inner.width, ChainCaptionHeight);
            y += ChainCaptionStep;

            int columns = ColumnsFor(inner.width);
            int rows = RowsFor(socketCount, columns);
            // Centre the socket rows on the page.
            float usedWidth = columns * SpellbookLayout.BeadWidth + (columns - 1) * SpellbookLayout.BeadGap;
            Rect chain = new Rect(inner.x + Mathf.Floor((inner.width - usedWidth) * 0.5f), y, usedWidth,
                ChainHeightFor(rows));
            y += chain.height + ChainStepGap;

            // Bottom-up, so the actions stay reachable on a short screen.
            Rect castRow = new Rect(inner.x, inner.yMax - CastRowHeight, inner.width, CastRowHeight);
            Rect actionRow = new Rect(inner.x, castRow.y - ActionRowHeight - RowGap, inner.width, ActionRowHeight);
            Rect validation = new Rect(inner.x, actionRow.y - ValidationHeight - RowGap, inner.width, ValidationHeight);

            float detailHeight = validation.y - RowGap - y;
            if (detailHeight < 0f)
            {
                detailHeight = 0f;
            }

            Rect detail = new Rect(inner.x, y, inner.width, detailHeight);
            return new EditorLayout(title, divider, nameCaption, nameField, chainCaption, chain, detail, validation,
                actionRow, castRow, columns, rows);
        }

        /// <summary>Rectangle of one bead inside <see cref="Chain"/>; beads wrap row by row.</summary>
        public Rect BeadRect(int index)
        {
            int column = index % BeadColumns;
            int row = index / BeadColumns;
            return new Rect(Chain.x + column * (SpellbookLayout.BeadWidth + SpellbookLayout.BeadGap),
                Chain.y + row * (SpellbookLayout.BeadHeight + SpellbookLayout.BeadRowGap),
                SpellbookLayout.BeadWidth, SpellbookLayout.BeadHeight);
        }

        /// <summary>The socket inside a bead; the rest of the bead holds its index.</summary>
        public static Rect SocketRect(Rect bead)
        {
            return new Rect(bead.x + (bead.width - SpellbookLayout.SocketSize) * 0.5f, bead.y,
                SpellbookLayout.SocketSize, SpellbookLayout.SocketSize);
        }
    }

    /// <summary>Vertical bands of the left page, and the card grid of the rune list.</summary>
    internal readonly struct CatalogLayout
    {
        public static readonly SpellGlyphKind[] Kinds = Enum.GetValues<SpellGlyphKind>();

        /// <summary>All plus each rune kind.</summary>
        public static int CategoryCount => 1 + Kinds.Length;

        public const float SearchHeight = 26f;
        public const float SearchStep = 32f;
        public const float CategoryHeight = 24f;
        public const float CategoryStep = 30f;
        public const float CategoryGap = 6f;

        public const float CardHeight = 44f;
        public const float CardGap = 4f;
        public const float MinCardWidth = 150f;
        public const float SectionHeight = 24f;

        /// <summary>Width the scroll view keeps free for its bar.</summary>
        public const float ScrollBarWidth = 16f;

        public readonly Rect Title;
        public readonly Rect Divider;
        public readonly Rect Search;
        public readonly Rect Categories;
        public readonly Rect List;

        private CatalogLayout(Rect title, Rect divider, Rect search, Rect categories, Rect list)
        {
            Title = title;
            Divider = divider;
            Search = search;
            Categories = categories;
            List = list;
        }

        /// <summary>Rectangle of one category tab; tabs share the row evenly.</summary>
        public static Rect CategoryTab(Rect row, int index)
        {
            float width = (row.width - (CategoryCount - 1) * CategoryGap) / CategoryCount;
            return new Rect(row.x + index * (width + CategoryGap), row.y, width, row.height);
        }

        /// <summary>Card columns for a list of this width: two when both cards stay readable.</summary>
        public static int CardColumns(float width)
        {
            return width >= 2f * MinCardWidth + CardGap ? 2 : 1;
        }

        public static CatalogLayout Compute(Rect page)
        {
            Rect inner = SpellbookRect.Inset(page, SpellbookLayout.PageInset);
            float y = SpellbookLayout.PageHead(inner, out Rect title, out Rect divider);

            Rect search = new Rect(inner.x, y, inner.width, SearchHeight);
            y += SearchStep;

            Rect categories = new Rect(inner.x, y, inner.width, CategoryHeight);
            y += CategoryStep;

            Rect list = new Rect(inner.x, y, inner.width, inner.yMax - y);
            return new CatalogLayout(title, divider, search, categories, list);
        }
    }
}
