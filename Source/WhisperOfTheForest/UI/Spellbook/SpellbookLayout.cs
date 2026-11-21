using System;
using UnityEngine;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Pure geometry for the symmetric two-page spellbook built on <see cref="BookUi.DrawBookSpread"/>.
    /// No game state and no drawing, so clicks, tooltips and animations always agree on every rect.
    /// </summary>
    internal readonly struct SpellbookLayout
    {
        public const float HeaderHeight = 28f;
        public const float PageInsetX = 18f;
        public const float PageInsetY = 20f;
        public const float PageTitleHeight = 24f;
        public const float PageDividerHeight = 10f;
        public const float PageHeadStep = 38f;

        public readonly Rect Header;
        public readonly Rect LeftPage;
        public readonly Rect Spine;
        public readonly Rect RightPage;

        private SpellbookLayout(Rect header, Rect leftPage, Rect spine, Rect rightPage)
        {
            Header = header;
            LeftPage = leftPage;
            Spine = spine;
            RightPage = rightPage;
        }

        public static SpellbookLayout Compute(Rect content)
        {
            BookUi.ComputeBookSpread(content, HeaderHeight, out Rect header, out Rect leftPage,
                out Rect spine, out Rect rightPage);
            return new SpellbookLayout(header, leftPage, spine, rightPage);
        }

        public static Rect PageInner(Rect page)
        {
            return new Rect(
                page.x + PageInsetX,
                page.y + PageInsetY,
                Mathf.Max(0f, page.width - PageInsetX * 2f),
                Mathf.Max(0f, page.height - PageInsetY * 2f));
        }

        public static float PageHead(Rect inner, out Rect title, out Rect divider)
        {
            title = new Rect(inner.x, inner.y, inner.width, PageTitleHeight);
            divider = new Rect(inner.x + inner.width * 0.10f, title.yMax + 1f, inner.width * 0.80f, PageDividerHeight);
            return inner.y + PageHeadStep;
        }
    }

    /// <summary>
    /// Left page: title + divider, search box, 5 category tabs, scrollable 2-column Birch-Vellum
    /// rune card grid (stays 2 columns even at 820px width), and fixed bottom rune inspector card.
    /// </summary>
    internal readonly struct CatalogLayout
    {
        public static readonly SpellGlyphKind[] Kinds = Enum.GetValues<SpellGlyphKind>();
        public static int CategoryCount => 1 + Kinds.Length;

        public const float SearchHeight = 26f;
        public const float SearchStep = 31f;
        public const float CategoryHeight = 22f;
        public const float CategoryStep = 28f;
        public const float CategoryGap = 4f;

        public const float CardHeight = 50f;
        public const float CardGap = 6f;
        public const float MinCardWidth = 148f;
        public const float SectionHeight = 22f;
        public const float ScrollBarWidth = 16f;
        public const float InspectorGap = 6f;

        public readonly Rect Title;
        public readonly Rect Divider;
        public readonly Rect Search;
        public readonly Rect Categories;
        public readonly Rect List;
        public readonly Rect Inspector;

        private CatalogLayout(Rect title, Rect divider, Rect search, Rect categories, Rect list, Rect inspector)
        {
            Title = title;
            Divider = divider;
            Search = search;
            Categories = categories;
            List = list;
            Inspector = inspector;
        }

        public static Rect CategoryTab(Rect row, int index)
        {
            float width = (row.width - (CategoryCount - 1) * CategoryGap) / CategoryCount;
            return new Rect(row.x + index * (width + CategoryGap), row.y, width, row.height);
        }

        public static int CardColumns(float width)
        {
            return width >= 2f * MinCardWidth + CardGap ? 2 : 1;
        }

        public static CatalogLayout Compute(Rect page)
        {
            Rect inner = SpellbookLayout.PageInner(page);
            float y = SpellbookLayout.PageHead(inner, out Rect title, out Rect divider);

            Rect search = new Rect(inner.x, y, inner.width, SearchHeight);
            y += SearchStep;

            Rect categories = new Rect(inner.x, y, inner.width, CategoryHeight);
            y += CategoryStep;

            float inspectorHeight = inner.height < 500f ? 116f : 124f;
            Rect inspector = new Rect(inner.x, inner.yMax - inspectorHeight, inner.width, inspectorHeight);

            float listHeight = Mathf.Max(40f, inspector.y - InspectorGap - y);
            Rect list = new Rect(inner.x, y, inner.width, listHeight);
            return new CatalogLayout(title, divider, search, categories, list, inspector);
        }
    }

    /// <summary>
    /// Right page: header + book-tier cap badge, 2×5 ten-slot grid (omitted in standalone turret mode),
    /// spell name + form capsule, single-row 10-socket rune chain (never wraps even at 820px),
    /// scrollable live tactical plan &amp; carrier compatibility ledger, and fixed bottom action row(s).
    /// </summary>
    internal readonly struct EditorLayout
    {
        public const float SlotRowHeight = 24f;
        public const float SlotGap = 4f;
        public const float SlotGridHeight = SlotRowHeight * 2f + SlotGap;
        public const float NameRowHeight = 26f;
        public const float ChainCaptionHeight = 18f;
        public const float SectionGap = 6f;
        public const float ValidationBarHeight = 26f;
        public const float ActionRowHeight = 26f;
        public const float CastRowHeight = 30f;
        public const float ActionGap = 5f;
        public const float StandaloneActionHeight = 30f;

        public readonly Rect Title;
        public readonly Rect Divider;
        public readonly Rect SlotGrid;
        public readonly Rect NameField;
        public readonly Rect FormCapsule;
        public readonly Rect ChainCaption;
        public readonly Rect Chain;
        public readonly Rect LedgerOuter;
        public readonly Rect ValidationBar;
        public readonly Rect LedgerScroll;
        public readonly Rect ActionRow;
        public readonly Rect CastRow;
        public readonly int SocketCount;
        public readonly float BeadWidth;
        public readonly float BeadHeight;
        public readonly float BeadGap;
        public readonly float SocketSize;

        private EditorLayout(Rect title, Rect divider, Rect slotGrid, Rect nameField, Rect formCapsule,
            Rect chainCaption, Rect chain, Rect ledgerOuter, Rect validationBar, Rect ledgerScroll,
            Rect actionRow, Rect castRow, int socketCount, float beadWidth, float beadHeight,
            float beadGap, float socketSize)
        {
            Title = title;
            Divider = divider;
            SlotGrid = slotGrid;
            NameField = nameField;
            FormCapsule = formCapsule;
            ChainCaption = chainCaption;
            Chain = chain;
            LedgerOuter = ledgerOuter;
            ValidationBar = validationBar;
            LedgerScroll = ledgerScroll;
            ActionRow = actionRow;
            CastRow = castRow;
            SocketCount = socketCount;
            BeadWidth = beadWidth;
            BeadHeight = beadHeight;
            BeadGap = beadGap;
            SocketSize = socketSize;
        }

        public static EditorLayout Compute(Rect page, int socketCount, bool isStandalone)
        {
            int count = Mathf.Max(1, socketCount);
            Rect inner = SpellbookLayout.PageInner(page);
            float y = SpellbookLayout.PageHead(inner, out Rect title, out Rect divider);

            Rect slotGrid = Rect.zero;
            if (!isStandalone)
            {
                slotGrid = new Rect(inner.x, y, inner.width, SlotGridHeight);
                y = slotGrid.yMax + SectionGap;
            }

            float capsuleWidth = Mathf.Clamp(inner.width * 0.27f, 88f, 118f);
            Rect nameField = new Rect(inner.x, y, inner.width - capsuleWidth - 6f, NameRowHeight);
            Rect formCapsule = new Rect(nameField.xMax + 6f, y, capsuleWidth, NameRowHeight);
            y = nameField.yMax + SectionGap;

            Rect chainCaption = new Rect(inner.x, y, inner.width, ChainCaptionHeight);
            y = chainCaption.yMax + 2f;

            // Single row: always fits all sockets (10 even at 820px width) without wrapping.
            float beadGap = inner.width >= 420f ? 4f : 3f;
            float beadWidth = Mathf.Min(44f, Mathf.Floor((inner.width - (count - 1) * beadGap) / count));
            float socketSize = Mathf.Clamp(beadWidth - 2f, 24f, 40f);
            float beadHeight = socketSize + 14f;
            float usedWidth = count * beadWidth + (count - 1) * beadGap;
            Rect chain = new Rect(inner.x + Mathf.Floor((inner.width - usedWidth) * 0.5f), y, usedWidth, beadHeight);
            y = chain.yMax + SectionGap;

            Rect castRow = Rect.zero;
            Rect actionRow;
            float ledgerBottom;
            if (isStandalone)
            {
                actionRow = new Rect(inner.x, inner.yMax - StandaloneActionHeight, inner.width, StandaloneActionHeight);
                ledgerBottom = actionRow.y - SectionGap;
            }
            else
            {
                castRow = new Rect(inner.x, inner.yMax - CastRowHeight, inner.width, CastRowHeight);
                actionRow = new Rect(inner.x, castRow.y - ActionRowHeight - ActionGap, inner.width, ActionRowHeight);
                ledgerBottom = actionRow.y - SectionGap;
            }

            float ledgerHeight = Mathf.Max(48f, ledgerBottom - y);
            Rect ledgerOuter = new Rect(inner.x, y, inner.width, ledgerHeight);
            Rect validationBar = new Rect(ledgerOuter.x, ledgerOuter.y, ledgerOuter.width,
                Mathf.Min(ValidationBarHeight, ledgerOuter.height));
            Rect ledgerScroll = new Rect(
                ledgerOuter.x + 6f,
                validationBar.yMax + 4f,
                Mathf.Max(0f, ledgerOuter.width - 12f),
                Mathf.Max(0f, ledgerOuter.yMax - validationBar.yMax - 8f));

            return new EditorLayout(title, divider, slotGrid, nameField, formCapsule, chainCaption, chain,
                ledgerOuter, validationBar, ledgerScroll, actionRow, castRow, count,
                beadWidth, beadHeight, beadGap, socketSize);
        }

        public Rect SlotTabRect(int index)
        {
            const int cols = 5;
            int col = index % cols;
            int row = index / cols;
            float tabWidth = Mathf.Floor((SlotGrid.width - (cols - 1) * SlotGap) / cols);
            float x = SlotGrid.x + col * (tabWidth + SlotGap);
            float w = col == cols - 1 ? SlotGrid.xMax - x : tabWidth;
            return new Rect(x, SlotGrid.y + row * (SlotRowHeight + SlotGap), w, SlotRowHeight);
        }

        public Rect BeadRect(int index)
        {
            return new Rect(Chain.x + index * (BeadWidth + BeadGap), Chain.y, BeadWidth, BeadHeight);
        }

        public Rect SocketRect(Rect bead)
        {
            return new Rect(bead.x + (bead.width - SocketSize) * 0.5f, bead.y, SocketSize, SocketSize);
        }
    }
}
