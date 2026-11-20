using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Rituals
{
    internal enum RitualButtonRole
    {
        Primary,
        Secondary,
        Danger
    }

    /// <summary>
    /// Livingwood &amp; Birch-Vellum Codex palette, UI texture bindings, R4 ground preview
    /// materials, and IMGUI primitives for <see cref="Window_RitualHost"/>.
    /// Every static Texture2D and Material is loaded in the static constructor under
    /// [StaticConstructorOnStartup], and every PNG in Textures/UI/Ritual/ is actively drawn.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class RitualPanelStyle
    {
        private const string UiDir = "UI/Ritual/";

        // Cover & dark-stone slate surfaces
        public static readonly Color CoverText = new Color(0.918f, 0.894f, 0.843f, 1f);         // #EAE4D7
        public static readonly Color CoverTextDim = new Color(0.737f, 0.706f, 0.620f, 1f);
        public static readonly Color CoverGold = new Color(0.988f, 0.886f, 0.612f, 1f);         // #FCE29C
        public static readonly Color CoverBorder = new Color(0.898f, 0.663f, 0.231f, 0.80f);    // #E5A93B
        public static readonly Color CoverRim = new Color(0.898f, 0.663f, 0.231f, 0.25f);
        public static readonly Color SpineBg = new Color(0.082f, 0.145f, 0.110f, 1f);
        public static readonly Color SpineGroove = new Color(0.047f, 0.086f, 0.067f, 1f);
        public static readonly Color SpineStitch = new Color(0.898f, 0.663f, 0.231f, 0.42f);

        // Two-Ink + Gold/Jade/Crystal hierarchy on Birch-Vellum pages (#EAE4D7)
        public static readonly Color InkPrimary = new Color(0.169f, 0.129f, 0.078f, 1f);        // #2B2114
        public static readonly Color InkSecondary = new Color(0.349f, 0.294f, 0.220f, 1f);      // #594B38
        public static readonly Color InkMuted = new Color(0.510f, 0.451f, 0.369f, 1f);
        public static readonly Color RubricRed = new Color(0.620f, 0.169f, 0.110f, 1f);         // #9E2B1C
        public static readonly Color BotanicalJade = new Color(0.122f, 0.420f, 0.271f, 1f);     // #1F6B45
        public static readonly Color CrystalBlue = new Color(0.078f, 0.357f, 0.541f, 1f);       // #145B8A
        public static readonly Color GoldAccent = new Color(0.784f, 0.561f, 0.157f, 1f);        // #C88F28
        public static readonly Color AmberAccent = new Color(0.710f, 0.467f, 0.110f, 1f);
        public static readonly Color HairlineInk = new Color(0.320f, 0.250f, 0.160f, 0.26f);
        public static readonly Color SubtleRule = new Color(0.420f, 0.340f, 0.230f, 0.20f);

        // Vellum card & ledger tints (applied to RitualPanelAtlas)
        public static readonly Color CardVellumNormal = new Color(0.840f, 0.780f, 0.655f, 0.42f);
        public static readonly Color CardVellumHover = new Color(0.910f, 0.855f, 0.735f, 0.68f);
        public static readonly Color CardVellumSel = new Color(0.965f, 0.915f, 0.790f, 0.88f);
        public static readonly Color CardVellumRun = new Color(0.760f, 0.885f, 0.790f, 0.66f);
        public static readonly Color CardVellumRunSel = new Color(0.820f, 0.930f, 0.845f, 0.86f);
        public static readonly Color CardVellumLocked = new Color(0.760f, 0.710f, 0.620f, 0.24f);
        public static readonly Color CardVellumLockedSel = new Color(0.835f, 0.785f, 0.690f, 0.52f);

        // Status seals on left-page vellum cards
        public static readonly Color SealRunningBg = new Color(0.122f, 0.420f, 0.271f, 0.92f);
        public static readonly Color SealCollectingBg = new Color(0.710f, 0.467f, 0.110f, 0.92f);
        public static readonly Color SealMissingBg = new Color(0.620f, 0.169f, 0.110f, 0.90f);
        public static readonly Color SealAutoBg = new Color(0.078f, 0.357f, 0.541f, 0.90f);
        public static readonly Color SealText = new Color(0.980f, 0.965f, 0.930f, 1f);

        // Dark-stone calibration slate & chalk diagram (R3)
        public static readonly Color SlateBg = new Color(0.094f, 0.125f, 0.110f, 0.96f);          // #18201C
        public static readonly Color SlateBorder = new Color(0.784f, 0.561f, 0.157f, 0.85f);
        public static readonly Color SlateCellEmpty = new Color(0.137f, 0.176f, 0.157f, 0.85f);
        public static readonly Color SlateCellEmptyBorder = new Color(0.450f, 0.530f, 0.490f, 0.16f);
        public static readonly Color AltarCellBg = new Color(0.561f, 0.420f, 0.165f, 1f);
        public static readonly Color AltarCellBorder = new Color(0.988f, 0.886f, 0.612f, 0.95f);
        public static readonly Color AltarCellText = new Color(1f, 0.965f, 0.855f, 1f);

        public static readonly Color ChalkWhiteBg = new Color(0.949f, 0.961f, 0.961f, 0.94f);     // #F2F5F5
        public static readonly Color ChalkWhiteBorder = new Color(1f, 1f, 1f, 0.95f);
        public static readonly Color ChalkGreenBg = new Color(0.463f, 0.831f, 0.498f, 0.94f);     // #76B947 / emerald
        public static readonly Color ChalkGreenBorder = new Color(0.740f, 0.960f, 0.740f, 0.95f);
        public static readonly Color ChalkStarBg = new Color(0.455f, 0.729f, 0.980f, 0.95f);      // #74BAFA
        public static readonly Color ChalkStarBorder = new Color(0.784f, 0.902f, 1f, 0.95f);

        // Cost <-> Prep ledger & chips (R1)
        public static readonly Color CostLedgerBg = new Color(0.835f, 0.775f, 0.655f, 0.44f);
        public static readonly Color PrepLedgerBg = new Color(0.925f, 0.835f, 0.610f, 0.60f);
        public static readonly Color LedgerBorder = new Color(0.440f, 0.350f, 0.230f, 0.50f);
        public static readonly Color LedgerPrepBorder = new Color(0.784f, 0.561f, 0.157f, 0.85f);

        public static readonly Color ChipBg = new Color(0.935f, 0.900f, 0.825f, 0.92f);
        public static readonly Color ChipBorder = new Color(0.460f, 0.380f, 0.260f, 0.58f);
        public static readonly Color ChipManaBg = new Color(0.865f, 0.920f, 0.965f, 0.94f);
        public static readonly Color ChipManaBorder = new Color(0.078f, 0.357f, 0.541f, 0.68f);
        public static readonly Color ChipOkBg = new Color(0.855f, 0.930f, 0.870f, 0.94f);
        public static readonly Color ChipOkBorder = new Color(0.122f, 0.420f, 0.271f, 0.68f);
        public static readonly Color ChipWarnBg = new Color(0.955f, 0.855f, 0.840f, 0.95f);
        public static readonly Color ChipWarnBorder = new Color(0.620f, 0.169f, 0.110f, 0.75f);
        public static readonly Color ChipIconSocketBg = new Color(0.118f, 0.153f, 0.137f, 0.94f);
        public static readonly Color ChipIconSocketBorder = new Color(0.450f, 0.360f, 0.220f, 0.70f);

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

        // Metrics, Progress & Buttons
        public static readonly Color MetricsVellumBg = new Color(0.840f, 0.780f, 0.660f, 0.50f);
        public static readonly Color TrackBg = new Color(0.118f, 0.153f, 0.137f, 0.92f);
        public static readonly Color TrackActiveBorder = new Color(0.180f, 0.545f, 0.341f, 0.90f);
        public static readonly Color TrackFill = new Color(0.463f, 0.725f, 0.278f, 1f);

        public static readonly Color BtnJade = new Color(0.180f, 0.545f, 0.341f, 1f);           // #2E8B57
        public static readonly Color BtnWalnut = new Color(0.470f, 0.355f, 0.220f, 1f);
        public static readonly Color BtnRust = new Color(0.620f, 0.169f, 0.110f, 1f);           // #9E2B1C

        // UI Textures (every texture in Textures/UI/Ritual/ is loaded here and actively drawn)
        public static readonly Texture2D Cover = ContentFinder<Texture2D>.Get(UiDir + "RitualCover", true);
        public static readonly Texture2D CoverFrameAtlas = ContentFinder<Texture2D>.Get(UiDir + "RitualCoverFrameAtlas", true);
        public static readonly Texture2D Page = ContentFinder<Texture2D>.Get(UiDir + "RitualPage", true);
        public static readonly Texture2D PageFrameAtlas = ContentFinder<Texture2D>.Get(UiDir + "RitualPageFrameAtlas", true);
        public static readonly Texture2D PanelAtlas = ContentFinder<Texture2D>.Get(UiDir + "RitualPanelAtlas", true);
        public static readonly Texture2D ButtonAtlas = ContentFinder<Texture2D>.Get(UiDir + "RitualButtonAtlas", true);
        public static readonly Texture2D Divider = ContentFinder<Texture2D>.Get(UiDir + "RitualDivider", true);
        public static readonly Texture2D SpineShade = ContentFinder<Texture2D>.Get(UiDir + "SpineShade", true);

        public static readonly Texture2D FamilyCrafting = ContentFinder<Texture2D>.Get(UiDir + "FamilyCrafting", true);
        public static readonly Texture2D FamilyLife = ContentFinder<Texture2D>.Get(UiDir + "FamilyLife", true);
        public static readonly Texture2D FamilyDomain = ContentFinder<Texture2D>.Get(UiDir + "FamilyDomain", true);
        public static readonly Texture2D ManaCrystal = ContentFinder<Texture2D>.Get(UiDir + "ManaCrystal", true);
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

        static RitualPanelStyle()
        {
        }

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

        public static void DrawBox(Rect rect, Color fill, Color border, int thickness = 1)
        {
            Widgets.DrawBoxSolidWithOutline(rect, fill, border, thickness);
        }

        public static void DrawHairline(float x, float y, float width)
        {
            Widgets.DrawBoxSolid(new Rect(x, y, width, 1f), HairlineInk);
        }

        public static void DrawAtlas(Rect rect, Texture2D atlas, Color tint)
        {
            if (atlas == null)
            {
                return;
            }

            Color prev = GUI.color;
            GUI.color = tint;
            Widgets.DrawAtlas(rect, atlas);
            GUI.color = prev;
        }

        public static void DrawPanel(Rect rect, Color tint)
        {
            DrawAtlas(rect, PanelAtlas, tint);
        }

        public static void DrawDashedRect(Rect rect, Color border, float dash = 6f, float gap = 4f)
        {
            Color prev = GUI.color;
            GUI.color = border;
            for (float x = rect.x; x < rect.xMax; x += dash + gap)
            {
                float w = Mathf.Min(dash, rect.xMax - x);
                GUI.DrawTexture(new Rect(x, rect.y, w, 1f), BaseContent.WhiteTex);
                GUI.DrawTexture(new Rect(x, rect.yMax - 1f, w, 1f), BaseContent.WhiteTex);
            }

            for (float y = rect.y; y < rect.yMax; y += dash + gap)
            {
                float h = Mathf.Min(dash, rect.yMax - y);
                GUI.DrawTexture(new Rect(rect.x, y, 1f, h), BaseContent.WhiteTex);
                GUI.DrawTexture(new Rect(rect.xMax - 1f, y, 1f, h), BaseContent.WhiteTex);
            }

            GUI.color = prev;
        }

        public static void DrawIcon(Rect rect, Texture2D tex, Color tint)
        {
            if (tex == null)
            {
                return;
            }

            Color prev = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
            GUI.color = prev;
        }

        public static void DrawStretched(Rect rect, Texture2D tex, Color tint)
        {
            if (tex == null)
            {
                return;
            }

            Color prev = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill);
            GUI.color = prev;
        }

        /// <summary>
        /// Draws a single-line label with <see cref="Text.WordWrap"/> kept strictly false.
        /// Callers wishing multi-line wrapping must use <see cref="DrawWrappedLabel"/>.
        /// </summary>
        public static void DrawLabel(Rect rect, string text, GameFont font, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft, bool ellipsis = true)
        {
            using (TechLabDrawScope.Begin())
            {
                Text.Font = font;
                Text.Anchor = anchor;
                Text.WordWrap = false;
                GUI.color = color;
                if (ellipsis)
                {
                    Widgets.LabelEllipses(rect, text ?? string.Empty);
                }
                else
                {
                    Widgets.Label(rect, text ?? string.Empty);
                }
            }
        }

        /// <summary>Draws a multi-line word-wrapped label.</summary>
        public static void DrawWrappedLabel(Rect rect, string text, GameFont font, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft)
        {
            using (TechLabDrawScope.Begin())
            {
                Text.Font = font;
                Text.Anchor = anchor;
                Text.WordWrap = true;
                GUI.color = color;
                Widgets.Label(rect, text ?? string.Empty);
            }
        }

        public static float CalcTextWidth(string text, GameFont font)
        {
            using (TechLabDrawScope.Begin())
            {
                Text.Font = font;
                Text.WordWrap = false;
                return Text.CalcSize(text ?? string.Empty).x;
            }
        }

        public static float CalcTextHeight(string text, GameFont font, float width)
        {
            using (TechLabDrawScope.Begin())
            {
                Text.Font = font;
                Text.WordWrap = true;
                return Text.CalcHeight(text ?? string.Empty, width);
            }
        }

        public static void DrawBookSpread(Rect windowRect, float headerHeight, out Rect headerRect,
            out Rect leftPageRect, out Rect rightPageRect)
        {
            // Ancient moss-leather cover + superconducting-gold corner hardware
            DrawStretched(windowRect, Cover, Color.white);
            DrawAtlas(windowRect, CoverFrameAtlas, Color.white);

            const float frameInset = 14f;
            const float headerPadX = 10f;
            const float spineWidth = 14f;

            headerRect = new Rect(
                windowRect.x + frameInset + headerPadX,
                windowRect.y + frameInset - 2f,
                windowRect.width - (frameInset + headerPadX) * 2f,
                headerHeight);

            float bodyTop = headerRect.yMax + 6f;
            float bodyHeight = windowRect.height - bodyTop - frameInset;
            Rect bodyRect = new Rect(windowRect.x + frameInset, bodyTop, windowRect.width - frameInset * 2f, bodyHeight);

            float pageWidth = Mathf.Floor((bodyRect.width - spineWidth) * 0.5f);
            float rightWidth = bodyRect.width - spineWidth - pageWidth;

            leftPageRect = new Rect(bodyRect.x, bodyRect.y, pageWidth, bodyRect.height);
            Rect spineRect = new Rect(leftPageRect.xMax, bodyRect.y, spineWidth, bodyRect.height);
            rightPageRect = new Rect(spineRect.xMax, bodyRect.y, rightWidth, bodyRect.height);

            // Warm birch-vellum pages with inner spine fold shadow and botanical border atlas
            DrawPageSurface(leftPageRect, spineOnRight: true);
            DrawPageSurface(rightPageRect, spineOnRight: false);

            // Recessed leather spine groove + gold binding stitches
            Widgets.DrawBoxSolid(spineRect, SpineBg);
            Widgets.DrawBoxSolid(new Rect(spineRect.center.x - 2f, spineRect.y, 4f, spineRect.height), SpineGroove);
            Color prev = GUI.color;
            GUI.color = SpineStitch;
            float stitchX = Mathf.Round(spineRect.center.x) - 1f;
            for (float y = spineRect.y + 12f; y < spineRect.yMax - 12f; y += 16f)
            {
                float h = Mathf.Min(7f, spineRect.yMax - 12f - y);
                GUI.DrawTexture(new Rect(stitchX, y, 2f, h), BaseContent.WhiteTex);
            }

            GUI.color = prev;
        }

        private static void DrawPageSurface(Rect pageRect, bool spineOnRight)
        {
            DrawStretched(pageRect, Page, Color.white);
            DrawSpineFoldShadow(pageRect, spineOnRight);
            DrawAtlas(pageRect, PageFrameAtlas, Color.white);
        }

        private static void DrawSpineFoldShadow(Rect pageRect, bool onRightEdge)
        {
            if (SpineShade == null)
            {
                return;
            }

            const float shadeWidth = 34f;
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            if (onRightEdge)
            {
                GUI.DrawTextureWithTexCoords(
                    new Rect(pageRect.xMax - shadeWidth, pageRect.y, shadeWidth, pageRect.height),
                    SpineShade,
                    new Rect(1f, 0f, -1f, 1f));
            }
            else
            {
                GUI.DrawTexture(
                    new Rect(pageRect.x, pageRect.y, shadeWidth, pageRect.height),
                    SpineShade,
                    ScaleMode.StretchToFill);
            }

            GUI.color = prev;
        }

        public static void DrawCoverBackdrop(Rect rect)
        {
            DrawStretched(rect, Cover, Color.white);
            DrawAtlas(rect, CoverFrameAtlas, Color.white);
        }

        public static bool DrawActionButton(Rect rect, string label, RitualButtonRole role, bool enabled)
        {
            Color baseColor = role == RitualButtonRole.Primary ? BtnJade
                : role == RitualButtonRole.Danger ? BtnRust
                : BtnWalnut;

            bool over = enabled && Mouse.IsOver(rect);
            Color tint = baseColor;
            if (!enabled)
            {
                float grey = baseColor.grayscale * 0.85f;
                tint = new Color(grey, grey, grey, 0.45f);
            }
            else if (over)
            {
                tint = Input.GetMouseButton(0) ? Darken(baseColor, 0.16f) : Lighten(baseColor, 0.16f);
            }

            DrawAtlas(rect, ButtonAtlas, tint);
            Color textColor = enabled ? CoverText : new Color(CoverText.r, CoverText.g, CoverText.b, 0.52f);
            DrawLabel(rect.ContractedBy(8f, 0f), label, GameFont.Small, textColor,
                TextAnchor.MiddleCenter, ellipsis: true);
            return enabled && Widgets.ButtonInvisible(rect, doMouseoverSound: true);
        }

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
                        DrawIcon(iconInner, icon, iconTint ?? CoverText);
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
            DrawBox(rect, bgColor, borderColor, 1);

            float iconSize = RitualLayoutMath.ChipIconSize;
            Rect iconBox = new Rect(rect.x + RitualLayoutMath.ChipPadLeft,
                rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize);
            DrawBox(iconBox, ChipIconSocketBg, ChipIconSocketBorder, 1);
            drawIconContent?.Invoke(iconBox.ContractedBy(2f));

            bool hasLabel = !text.NullOrEmpty();
            bool hasCount = !countText.NullOrEmpty();
            float rawLabelW = hasLabel ? CalcTextWidth(text, GameFont.Small) : 0f;
            float rawCountW = hasCount ? CalcTextWidth(countText, GameFont.Small) : 0f;

            RitualLayoutMath.ComputeChipTextLayout(
                rect.x, rect.width, rawLabelW, rawCountW, hasLabel, hasCount,
                out float labelX, out float availLabelW, out bool needsEllipsis,
                out float countX, out float countBoxW);

            if (hasLabel && availLabelW > 0f)
            {
                DrawLabel(
                    new Rect(labelX, rect.y, availLabelW, rect.height),
                    text,
                    GameFont.Small,
                    textColor,
                    TextAnchor.MiddleLeft,
                    ellipsis: needsEllipsis);
            }

            if (hasCount && countBoxW > 0f)
            {
                DrawLabel(
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
                DrawIcon(rect, cost.category.icon, Color.white);
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

        public static Color Lighten(Color color, float amount)
        {
            return new Color(Mathf.Lerp(color.r, 1f, amount), Mathf.Lerp(color.g, 1f, amount),
                Mathf.Lerp(color.b, 1f, amount), color.a);
        }

        public static Color Darken(Color color, float amount)
        {
            return new Color(color.r * (1f - amount), color.g * (1f - amount), color.b * (1f - amount), color.a);
        }

        public static void PlayClick()
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
        }
    }
}
