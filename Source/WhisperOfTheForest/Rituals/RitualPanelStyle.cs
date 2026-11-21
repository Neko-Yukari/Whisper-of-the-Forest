using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Ritual-codex specific palette, textures, R4 ground preview materials, and composite widgets
    /// (cost/offering chips, chalk diagram helpers, portraits). All shared book-shell chrome,
    /// buttons, labels, and textures live in <see cref="BookUi"/> so the ritual book and spellbook
    /// share one component kit.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class RitualPanelStyle
    {
        private const string UiDir = "UI/Ritual/";

        // Shared palette aliases forwarded from BookUi
        public static Color CoverText => BookUi.CoverText;
        public static Color CoverTextDim => BookUi.CoverTextDim;
        public static Color CoverGold => BookUi.CoverGold;
        public static Color CoverBorder => BookUi.CoverBorder;
        public static Color CoverRim => BookUi.CoverRim;

        public static Color InkPrimary => BookUi.InkPrimary;
        public static Color InkSecondary => BookUi.InkSecondary;
        public static Color InkMuted => BookUi.InkMuted;
        public static Color RubricRed => BookUi.RubricRed;
        public static Color BotanicalJade => BookUi.BotanicalJade;
        public static Color CrystalBlue => BookUi.CrystalBlue;
        public static Color GoldAccent => BookUi.GoldAccent;
        public static Color AmberAccent => BookUi.AmberAccent;
        public static Color HairlineInk => BookUi.HairlineInk;
        public static Color SubtleRule => BookUi.SubtleRule;

        // Vellum card & ledger tints forwarded from BookUi
        public static Color CardVellumNormal => BookUi.CardVellumNormal;
        public static Color CardVellumHover => BookUi.CardVellumHover;
        public static Color CardVellumSel => BookUi.CardVellumSel;
        public static Color CardVellumRun => BookUi.CardVellumRun;
        public static Color CardVellumRunSel => BookUi.CardVellumRunSel;
        public static Color CardVellumLocked => BookUi.CardVellumLocked;
        public static Color CardVellumLockedSel => BookUi.CardVellumLockedSel;

        // Status seals on left-page vellum cards
        public static readonly Color SealRunningBg = new Color(0.122f, 0.420f, 0.271f, 0.92f);
        public static readonly Color SealCollectingBg = new Color(0.710f, 0.467f, 0.110f, 0.92f);
        public static readonly Color SealMissingBg = new Color(0.620f, 0.169f, 0.110f, 0.90f);
        public static readonly Color SealAutoBg = new Color(0.078f, 0.357f, 0.541f, 0.90f);
        public static Color SealText => BookUi.SealText;

        // Dark-stone calibration slate & chalk diagram (R3)
        public static readonly Color SlateBg = new Color(0.094f, 0.125f, 0.110f, 0.96f);
        public static readonly Color SlateBorder = new Color(0.784f, 0.561f, 0.157f, 0.85f);
        public static readonly Color SlateCellEmpty = new Color(0.137f, 0.176f, 0.157f, 0.85f);
        public static readonly Color SlateCellEmptyBorder = new Color(0.450f, 0.530f, 0.490f, 0.16f);
        public static readonly Color AltarCellBg = new Color(0.561f, 0.420f, 0.165f, 1f);
        public static readonly Color AltarCellBorder = new Color(0.988f, 0.886f, 0.612f, 0.95f);
        public static readonly Color AltarCellText = new Color(1f, 0.965f, 0.855f, 1f);

        public static readonly Color ChalkWhiteBg = new Color(0.949f, 0.961f, 0.961f, 0.94f);
        public static readonly Color ChalkWhiteBorder = new Color(1f, 1f, 1f, 0.95f);
        public static readonly Color ChalkGreenBg = new Color(0.463f, 0.831f, 0.498f, 0.94f);
        public static readonly Color ChalkGreenBorder = new Color(0.740f, 0.960f, 0.740f, 0.95f);
        public static readonly Color ChalkStarBg = new Color(0.455f, 0.729f, 0.980f, 0.95f);
        public static readonly Color ChalkStarBorder = new Color(0.784f, 0.902f, 1f, 0.95f);

        // Cost <-> Prep ledger & chips (R1) forwarded from BookUi
        public static Color CostLedgerBg => BookUi.LedgerBg;
        public static Color PrepLedgerBg => BookUi.LedgerHighlightBg;
        public static Color LedgerBorder => BookUi.LedgerBorder;
        public static Color LedgerPrepBorder => BookUi.LedgerHighlightBorder;

        public static Color ChipBg => BookUi.ChipBg;
        public static Color ChipBorder => BookUi.ChipBorder;
        public static Color ChipManaBg => BookUi.ChipManaBg;
        public static Color ChipManaBorder => BookUi.ChipManaBorder;
        public static Color ChipOkBg => BookUi.ChipOkBg;
        public static Color ChipOkBorder => BookUi.ChipOkBorder;
        public static Color ChipWarnBg => BookUi.ChipWarnBg;
        public static Color ChipWarnBorder => BookUi.ChipWarnBorder;
        public static Color ChipIconSocketBg => BookUi.ChipIconSocketBg;
        public static Color ChipIconSocketBorder => BookUi.ChipIconSocketBorder;

        // Portraits & Popover
        public static readonly Color PortraitBg = new Color(0.118f, 0.153f, 0.137f, 0.95f);
        public static readonly Color PortraitBorder = new Color(0.480f, 0.380f, 0.230f, 0.85f);
        public static readonly Color PortraitHoverBorder = new Color(0.898f, 0.663f, 0.231f, 1f);
        public static readonly Color PortraitEmptyBg = new Color(0.820f, 0.765f, 0.655f, 0.45f);
        public static readonly Color PortraitEmptyBorder = new Color(0.450f, 0.370f, 0.260f, 0.65f);
        public static readonly Color LevelTagBg = new Color(0.094f, 0.125f, 0.110f, 0.90f);
        public static readonly Color LevelTagText = new Color(0.608f, 0.898f, 0.635f, 1f);
        public static readonly Color LevelTagBorder = new Color(0.463f, 0.725f, 0.278f, 0.55f);
        public static readonly Color NameTagBg = new Color(0.094f, 0.125f, 0.110f, 0.88f);

        public static readonly Color PopoverRowBg = new Color(0.129f, 0.220f, 0.173f, 0.88f);
        public static readonly Color PopoverRowHoverBg = new Color(0.180f, 0.298f, 0.231f, 0.95f);
        public static readonly Color PopoverRowSelBg = new Color(0.220f, 0.341f, 0.255f, 0.96f);
        public static readonly Color PopoverRowOtherBg = new Color(0.133f, 0.188f, 0.235f, 0.84f);
        public static readonly Color PopoverRowOtherBorder = new Color(0.310f, 0.478f, 0.620f, 0.85f);
        public static readonly Color PopoverRowLockedBg = new Color(0.110f, 0.145f, 0.125f, 0.62f);
        public static readonly Color PopoverBorder = new Color(0.550f, 0.440f, 0.240f, 0.65f);
        public static readonly Color PopoverWarnText = new Color(1.000f, 0.600f, 0.549f, 1f);
        public static readonly Color PopoverWarnBg = new Color(0.320f, 0.125f, 0.110f, 0.88f);
        public static readonly Color PopoverOkGreen = new Color(0.463f, 0.831f, 0.498f, 1f);
        public static readonly Color RoleTagBg = new Color(0.125f, 0.306f, 0.467f, 0.45f);
        public static readonly Color RoleTagBorder = new Color(0.350f, 0.580f, 0.780f, 0.85f);
        public static readonly Color RoleTagText = new Color(0.780f, 0.910f, 1f, 1f);

        // Metrics & Progress
        public static readonly Color MetricsVellumBg = new Color(0.840f, 0.780f, 0.660f, 0.50f);
        public static readonly Color TrackBg = new Color(0.118f, 0.153f, 0.137f, 0.92f);
        public static readonly Color TrackActiveBorder = new Color(0.180f, 0.545f, 0.341f, 0.90f);
        public static readonly Color TrackFill = new Color(0.463f, 0.725f, 0.278f, 1f);

        // Shared divider alias & ritual-only textures
        public static Texture2D Divider => BookUi.Divider;
        public static readonly Texture2D FamilyCrafting = ContentFinder<Texture2D>.Get(UiDir + "FamilyCrafting", true);
        public static readonly Texture2D FamilyLife = ContentFinder<Texture2D>.Get(UiDir + "FamilyLife", true);
        public static readonly Texture2D FamilyDomain = ContentFinder<Texture2D>.Get(UiDir + "FamilyDomain", true);
        public static Texture2D ManaCrystal => BookUi.ManaCrystal;
        public static readonly Texture2D ChalkRuneCell = ContentFinder<Texture2D>.Get(UiDir + "ChalkRuneCell", true);
        public static readonly Texture2D RadioOn = ContentFinder<Texture2D>.Get(UiDir + "RadioOn", true);
        public static readonly Texture2D RadioOff = ContentFinder<Texture2D>.Get(UiDir + "RadioOff", true);
        public static readonly Texture2D Plus = ContentFinder<Texture2D>.Get(UiDir + "Plus", true);

        // R4: Cached semi-transparent map preview materials for missing chalk marks
        public static readonly Material ChalkPreviewWhite = MaterialPool.MatFrom(
            "Buildings/ChalkMark", ShaderDatabase.Transparent, new Color(0.95f, 0.96f, 0.94f, 0.60f));
        public static readonly Material ChalkPreviewGreen = MaterialPool.MatFrom(
            "Buildings/ChalkMark", ShaderDatabase.Transparent, new Color(0.46f, 0.88f, 0.50f, 0.62f));
        public static readonly Material ChalkPreviewStar = MaterialPool.MatFrom(
            "Buildings/ChalkMark", ShaderDatabase.Transparent, new Color(0.55f, 0.78f, 1.00f, 0.64f));

        public static Material ChalkPreviewMaterial(ChalkColor chalk)
        {
            switch (chalk)
            {
                case ChalkColor.Green:
                    return ChalkPreviewGreen;
                case ChalkColor.Star:
                    return ChalkPreviewStar;
                default:
                    return ChalkPreviewWhite;
            }
        }

        public static Texture2D FamilyIcon(RitualFamily family)
        {
            switch (family)
            {
                case RitualFamily.Crafting:
                    return FamilyCrafting;
                case RitualFamily.Life:
                    return FamilyLife;
                default:
                    return FamilyDomain;
            }
        }

        public static Color FamilyColor(RitualFamily family)
        {
            switch (family)
            {
                case RitualFamily.Crafting:
                    return new Color(0.620f, 0.400f, 0.080f, 1f);
                case RitualFamily.Life:
                    return BotanicalJade;
                default:
                    return CrystalBlue;
            }
        }

        public static string FamilyLabel(RitualFamily family)
        {
            switch (family)
            {
                case RitualFamily.Crafting:
                    return "WotF_RitualPanel_FamilyCrafting".Translate();
                case RitualFamily.Life:
                    return "WotF_RitualPanel_FamilyLife".Translate();
                default:
                    return "WotF_RitualPanel_FamilyDomain".Translate();
            }
        }

        // Thin forwards to shared BookUi primitives
        public static void DrawBox(Rect rect, Color fill, Color border, int thickness = 1) =>
            BookUi.DrawBox(rect, fill, border, thickness);

        public static void DrawHairline(float x, float y, float width) =>
            BookUi.DrawHairline(x, y, width);

        public static void DrawPanel(Rect rect, Color tint) =>
            BookUi.DrawPanel(rect, tint);

        public static void DrawIcon(Rect rect, Texture2D tex, Color tint) =>
            BookUi.DrawIcon(rect, tex, tint);

        public static void DrawLabel(Rect rect, string text, GameFont font, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft, bool ellipsis = true) =>
            BookUi.DrawLabel(rect, text, font, color, anchor, ellipsis);

        public static void DrawWrappedLabel(Rect rect, string text, GameFont font, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft) =>
            BookUi.DrawWrappedLabel(rect, text, font, color, anchor);

        public static float CalcTextWidth(string text, GameFont font) =>
            BookUi.CalcTextWidth(text, font);

        public static float CalcTextHeight(string text, GameFont font, float width) =>
            BookUi.CalcTextHeight(text, font, width);

        public static void DrawCoverBackdrop(Rect rect) =>
            BookUi.DrawCover(rect);

        public static bool DrawActionButton(Rect rect, string label, BookButtonRole role, bool enabled) =>
            BookUi.DrawButton(rect, label, role, enabled);

        public static void PlayClick() =>
            BookUi.PlayClick();

        public static Color Darken(Color color, float amount) =>
            BookUi.Darken(color, amount);

        public static void DrawBookSpread(Rect windowRect, float headerHeight, out Rect headerRect,
            out Rect leftPageRect, out Rect rightPageRect) =>
            BookUi.DrawBookSpread(windowRect, headerHeight, out headerRect, out leftPageRect, out rightPageRect);

        public static void DrawDashedRect(Rect rect, Color border, float dash = 6f, float gap = 4f) =>
            BookUi.DrawDashedRect(rect, border, dash, gap);

        public static void DrawCostChip(Rect rect, Texture2D icon, ThingDef thingDef, string text, string countText,
            Color bgColor, Color borderColor, Color textColor, Color countColor, string tooltip = null,
            Color? iconTint = null)
        {
            DrawChipChrome(rect, text, countText, bgColor, borderColor, textColor, countColor, tooltip,
                iconInner =>
                {
                    if (thingDef != null)
                    {
                        Widgets.ThingIcon(iconInner, thingDef);
                    }
                    else if (icon != null)
                    {
                        BookUi.DrawIcon(iconInner, icon, iconTint ?? CoverText);
                    }
                });
        }

        public static void DrawOfferingChip(Rect rect, SpellGlyphCost cost, string countText,
            Color bgColor, Color borderColor, Color textColor, Color countColor, string tooltip = null)
        {
            DrawChipChrome(rect, cost.Label, countText, bgColor, borderColor, textColor, countColor, tooltip,
                iconInner => DrawThingOrCategoryIcon(iconInner, cost));
        }

        private static void DrawChipChrome(Rect rect, string text, string countText, Color bgColor, Color borderColor,
            Color textColor, Color countColor, string tooltip, Action<Rect> drawIconContent)
        {
            BookUi.DrawBox(rect, bgColor, borderColor, 1);

            float iconSize = RitualLayoutMath.ChipIconSize;
            Rect iconBox = new Rect(rect.x + RitualLayoutMath.ChipPadLeft,
                rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize);
            BookUi.DrawBox(iconBox, ChipIconSocketBg, ChipIconSocketBorder, 1);
            drawIconContent?.Invoke(iconBox.ContractedBy(2f));

            bool hasLabel = !text.NullOrEmpty();
            bool hasCount = !countText.NullOrEmpty();
            float rawLabelW = hasLabel ? BookUi.CalcTextWidth(text, GameFont.Small) : 0f;
            float rawCountW = hasCount ? BookUi.CalcTextWidth(countText, GameFont.Small) : 0f;

            RitualLayoutMath.ComputeChipTextLayout(
                rect.x, rect.width, rawLabelW, rawCountW, hasLabel, hasCount,
                out float labelX, out float availLabelW, out bool needsEllipsis,
                out float countX, out float countBoxW);

            if (hasLabel && availLabelW > 0f)
            {
                BookUi.DrawLabel(
                    new Rect(labelX, rect.y, availLabelW, rect.height),
                    text,
                    GameFont.Small,
                    textColor,
                    TextAnchor.MiddleLeft,
                    ellipsis: needsEllipsis);
            }

            if (hasCount && countBoxW > 0f)
            {
                BookUi.DrawLabel(
                    new Rect(countX, rect.y, countBoxW, rect.height),
                    countText,
                    GameFont.Small,
                    countColor,
                    TextAnchor.MiddleRight,
                    ellipsis: false);
            }

            string resolvedTip = tooltip;
            if (needsEllipsis && hasLabel)
            {
                resolvedTip = resolvedTip.NullOrEmpty() ? text : text + "\n" + resolvedTip;
            }

            if (!resolvedTip.NullOrEmpty())
            {
                TooltipHandler.TipRegion(rect, resolvedTip);
            }
        }

        public static void DrawThingOrCategoryIcon(Rect rect, SpellGlyphCost cost)
        {
            if (cost.thingDef != null)
            {
                Widgets.ThingIcon(rect, cost.thingDef);
                return;
            }

            if (cost.category?.icon != null)
            {
                BookUi.DrawIcon(rect, cost.category.icon, Color.white);
                return;
            }

            List<ThingDef> children = cost.category?.childThingDefs;
            if (children != null && children.Count > 0)
            {
                Widgets.ThingIcon(rect, children[0]);
            }
        }

        public static void DrawPawnPortrait(Rect rect, Pawn pawn, Vector2 size)
        {
            RenderTexture portrait = PortraitsCache.Get(pawn, size, Rot4.South, new Vector3(0f, 0f, 0.20f), 1.38f);
            if (portrait != null)
            {
                GUI.DrawTexture(rect, portrait, ScaleMode.ScaleToFit);
            }
        }
    }
}
