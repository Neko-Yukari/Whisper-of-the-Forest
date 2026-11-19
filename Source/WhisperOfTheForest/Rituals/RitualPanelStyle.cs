using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Palette, textures and IMGUI primitives for the two-page book ritual host window.
    /// White-mask icons live under Textures/UI/Ritual/ and are tinted via GUI.color so every
    /// family colour, disabled state and badge stays crisp without font glyph fallbacks.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class RitualPanelStyle
    {
        private const string UiDir = "UI/Ritual/";

        // Book cover & spread
        public static readonly Color CoverBg = new Color(0.110f, 0.086f, 0.067f, 1f);
        public static readonly Color CoverBorder = new Color(0.431f, 0.361f, 0.271f, 1f);
        public static readonly Color CoverRim = new Color(0.831f, 0.722f, 0.525f, 0.20f);
        public static readonly Color PageEdgeLight = new Color(0.278f, 0.239f, 0.188f, 1f);
        public static readonly Color PageEdgeDark = new Color(0.169f, 0.141f, 0.110f, 1f);
        public static readonly Color SpreadBorder = new Color(0.388f, 0.325f, 0.251f, 1f);

        public static readonly Color PageBg = new Color(0.165f, 0.145f, 0.118f, 1f);
        public static readonly Color PageOuterEdge = new Color(0.122f, 0.106f, 0.086f, 1f);
        public static readonly Color PageInnerBorder = new Color(0.302f, 0.263f, 0.212f, 1f);
        public static readonly Color PanelSurface = new Color(0.071f, 0.059f, 0.047f, 0.56f);
        public static readonly Color PanelSurfaceSoft = new Color(0.071f, 0.063f, 0.051f, 0.45f);
        public static readonly Color SpineBg = new Color(0.063f, 0.051f, 0.039f, 1f);
        public static readonly Color SpineGroove = new Color(0.039f, 0.031f, 0.024f, 1f);
        public static readonly Color SpineStitch = new Color(0.690f, 0.588f, 0.424f, 0.36f);

        // Cards
        public static readonly Color CardNormalBg = new Color(0.196f, 0.180f, 0.157f, 1f);
        public static readonly Color CardHoverBg = new Color(0.243f, 0.220f, 0.188f, 1f);
        public static readonly Color CardBorder = new Color(0.361f, 0.325f, 0.278f, 1f);
        public static readonly Color CardHoverBorder = new Color(0.549f, 0.490f, 0.408f, 1f);
        public static readonly Color CardSelBg = new Color(0.322f, 0.271f, 0.196f, 1f);
        public static readonly Color CardSelBorder = new Color(0.659f, 0.565f, 0.400f, 1f);
        public static readonly Color CardRunBg = new Color(0.153f, 0.282f, 0.176f, 1f);
        public static readonly Color CardRunSelBg = new Color(0.212f, 0.345f, 0.204f, 1f);
        public static readonly Color CardRunBorder = new Color(0.463f, 0.784f, 0.510f, 1f);
        public static readonly Color CardRunSelBorder = new Color(0.608f, 0.910f, 0.651f, 1f);
        public static readonly Color CardLockedBg = new Color(0.137f, 0.125f, 0.114f, 0.85f);
        public static readonly Color CardLockedSelBg = new Color(0.208f, 0.188f, 0.165f, 0.90f);
        public static readonly Color CardLockedBorder = new Color(0.259f, 0.235f, 0.208f, 0.85f);
        public static readonly Color CardLockedSelBorder = new Color(0.486f, 0.439f, 0.365f, 0.92f);

        // Badges
        public static readonly Color BadgeRunningBg = new Color(0.290f, 0.580f, 0.337f, 0.92f);
        public static readonly Color BadgeRunningText = new Color(0.949f, 1f, 0.957f, 1f);
        public static readonly Color BadgeCollectingBg = new Color(0.706f, 0.510f, 0.165f, 0.92f);
        public static readonly Color BadgeCollectingText = new Color(1f, 0.973f, 0.902f, 1f);
        public static readonly Color BadgeMissingBg = new Color(0.612f, 0.243f, 0.220f, 0.92f);
        public static readonly Color BadgeMissingText = new Color(1f, 0.925f, 0.918f, 1f);
        public static readonly Color BadgeAutoBg = new Color(0.267f, 0.447f, 0.627f, 0.92f);
        public static readonly Color BadgeAutoText = new Color(0.922f, 0.961f, 1f, 1f);

        // Text & Accents
        public static readonly Color TitleCream = new Color(0.953f, 0.918f, 0.847f, 1f);
        public static readonly Color TextMain = new Color(0.937f, 0.918f, 0.878f, 1f);
        public static readonly Color TextSubtle = new Color(0.639f, 0.608f, 0.557f, 1f);
        public static readonly Color TextMuted = new Color(0.502f, 0.482f, 0.455f, 1f);
        public static readonly Color HeaderGreen = new Color(0.761f, 0.812f, 0.710f, 1f);
        public static readonly Color WarnRed = new Color(1.000f, 0.600f, 0.549f, 1f);
        public static readonly Color WarnBorder = new Color(0.651f, 0.341f, 0.314f, 1f);
        public static readonly Color OkGreen = new Color(0.561f, 0.831f, 0.596f, 1f);
        public static readonly Color OkText = new Color(0.737f, 0.902f, 0.761f, 1f);
        public static readonly Color OkBorder = new Color(0.365f, 0.541f, 0.392f, 1f);
        public static readonly Color ManaBlue = new Color(0.722f, 0.863f, 1.000f, 1f);
        public static readonly Color ManaBorder = new Color(0.302f, 0.439f, 0.588f, 1f);
        public static readonly Color SepColor = new Color(0.329f, 0.290f, 0.239f, 1f);

        // Hero & Chalk
        public static readonly Color HeroGlowTint = new Color(0.659f, 0.565f, 0.400f, 0.14f);
        public static readonly Color CalloutLine = new Color(0.659f, 0.588f, 0.467f, 1f);
        public static readonly Color CalloutText = new Color(0.937f, 0.906f, 0.839f, 1f);
        public static readonly Color BoardBg = new Color(0.075f, 0.071f, 0.063f, 1f);
        public static readonly Color BoardBorder = new Color(0.388f, 0.341f, 0.278f, 1f);
        public static readonly Color BoardCellEmpty = new Color(0.118f, 0.110f, 0.098f, 1f);
        public static readonly Color BoardCellBorder = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color AltarCellBg = new Color(0.561f, 0.447f, 0.212f, 1f);
        public static readonly Color AltarCellBorder = new Color(0.902f, 0.776f, 0.478f, 1f);
        public static readonly Color AltarCellText = new Color(1f, 0.961f, 0.839f, 1f);

        public static readonly Color ChalkWhiteBg = new Color(0.839f, 0.871f, 0.855f, 0.86f);
        public static readonly Color ChalkWhiteBorder = new Color(1f, 1f, 1f, 0.55f);
        public static readonly Color ChalkGreenBg = new Color(0.369f, 0.722f, 0.439f, 0.88f);
        public static readonly Color ChalkGreenBorder = new Color(0.659f, 0.941f, 0.706f, 0.60f);
        public static readonly Color ChalkStarBg = new Color(0.494f, 0.682f, 0.933f, 0.90f);
        public static readonly Color ChalkStarBorder = new Color(0.784f, 0.886f, 1f, 0.65f);

        // Cost <-> Prep Progress
        public static readonly Color CostBoxBg = new Color(0.071f, 0.063f, 0.051f, 0.52f);
        public static readonly Color CostBoxBorder = new Color(0.369f, 0.325f, 0.271f, 1f);
        public static readonly Color PrepBoxBg = new Color(0.290f, 0.235f, 0.133f, 0.46f);
        public static readonly Color PrepBoxBorder = new Color(0.659f, 0.545f, 0.290f, 1f);
        public static readonly Color PrepGoldText = new Color(0.941f, 0.812f, 0.522f, 1f);
        public static readonly Color ChipBg = new Color(0.114f, 0.102f, 0.086f, 1f);
        public static readonly Color ChipBorder = new Color(0.349f, 0.310f, 0.259f, 1f);
        public static readonly Color ChipIconBg = new Color(0.071f, 0.063f, 0.055f, 1f);
        public static readonly Color ChipIconBorder = new Color(0.271f, 0.239f, 0.200f, 1f);

        // Portraits & Popover
        public static readonly Color PortraitBg = new Color(0.157f, 0.141f, 0.122f, 1f);
        public static readonly Color PortraitBorder = new Color(0.522f, 0.471f, 0.400f, 1f);
        public static readonly Color PortraitHoverBorder = new Color(0.839f, 0.765f, 0.553f, 1f);
        public static readonly Color PortraitEmptyBg = new Color(0.110f, 0.098f, 0.082f, 0.65f);
        public static readonly Color PortraitEmptyBorder = new Color(0.420f, 0.380f, 0.329f, 1f);
        public static readonly Color LevelTagBg = new Color(0.059f, 0.055f, 0.047f, 0.88f);
        public static readonly Color LevelTagText = new Color(0.722f, 0.890f, 0.690f, 1f);
        public static readonly Color LevelTagBorder = new Color(0.561f, 0.831f, 0.596f, 0.35f);
        public static readonly Color NameTagBg = new Color(0.055f, 0.047f, 0.039f, 0.85f);

        public static readonly Color PopoverBg = new Color(0.110f, 0.098f, 0.082f, 0.98f);
        public static readonly Color PopoverRowBg = new Color(0.153f, 0.137f, 0.118f, 1f);
        public static readonly Color PopoverRowHoverBg = new Color(0.208f, 0.184f, 0.157f, 1f);
        public static readonly Color PopoverRowOtherBg = new Color(0.153f, 0.173f, 0.204f, 0.78f);
        public static readonly Color PopoverRowOtherBorder = new Color(0.310f, 0.373f, 0.451f, 1f);
        public static readonly Color RoleTagBg = new Color(0.255f, 0.376f, 0.518f, 0.32f);
        public static readonly Color RoleTagBorder = new Color(0.369f, 0.455f, 0.569f, 1f);
        public static readonly Color RoleTagText = new Color(0.722f, 0.847f, 1f, 1f);

        // Progress & Buttons
        public static readonly Color TrackBg = new Color(0.078f, 0.071f, 0.059f, 1f);
        public static readonly Color TrackActiveBorder = new Color(0.420f, 0.620f, 0.455f, 1f);
        public static readonly Color TrackFill = new Color(0.420f, 0.745f, 0.482f, 1f);
        public static readonly Color BtnBg = new Color(0.220f, 0.196f, 0.165f, 1f);
        public static readonly Color BtnHoverBg = new Color(0.290f, 0.259f, 0.220f, 1f);
        public static readonly Color BtnActiveBg = new Color(0.165f, 0.145f, 0.122f, 1f);
        public static readonly Color BtnBorder = new Color(0.525f, 0.467f, 0.384f, 1f);
        public static readonly Color BtnPrimaryBorder = new Color(0.549f, 0.659f, 0.490f, 1f);
        public static readonly Color BtnDangerBorder = new Color(0.651f, 0.427f, 0.408f, 1f);
        public static readonly Color BtnHoverBorder = new Color(0.710f, 0.639f, 0.537f, 1f);

        // Textures
        public static readonly Texture2D FamilyCrafting = ContentFinder<Texture2D>.Get(UiDir + "FamilyCrafting", true);
        public static readonly Texture2D FamilyLife = ContentFinder<Texture2D>.Get(UiDir + "FamilyLife", true);
        public static readonly Texture2D FamilyDomain = ContentFinder<Texture2D>.Get(UiDir + "FamilyDomain", true);
        public static readonly Texture2D ManaCrystal = ContentFinder<Texture2D>.Get(UiDir + "ManaCrystal", true);
        public static readonly Texture2D LightningBolt = ContentFinder<Texture2D>.Get(UiDir + "LightningBolt", true);
        public static readonly Texture2D SpineShade = ContentFinder<Texture2D>.Get(UiDir + "SpineShade", true);
        public static readonly Texture2D HeroGlow = ContentFinder<Texture2D>.Get(UiDir + "HeroGlow", true);
        public static readonly Texture2D Dot = ContentFinder<Texture2D>.Get(UiDir + "Dot", true);
        public static readonly Texture2D RadioOn = ContentFinder<Texture2D>.Get(UiDir + "RadioOn", true);
        public static readonly Texture2D RadioOff = ContentFinder<Texture2D>.Get(UiDir + "RadioOff", true);
        public static readonly Texture2D Plus = ContentFinder<Texture2D>.Get(UiDir + "Plus", true);

        static RitualPanelStyle()
        {
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
                    return new Color(0.886f, 0.745f, 0.471f, 1f);
                case RitualFamily.Life:
                    return new Color(0.549f, 0.831f, 0.588f, 1f);
                default:
                    return new Color(0.557f, 0.757f, 0.949f, 1f);
            }
        }

        public static Color FamilyFrameColor(RitualFamily family, float alpha = 0.45f)
        {
            Color c = FamilyColor(family);
            return new Color(c.r, c.g, c.b, alpha);
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

        public static void DrawLabel(Rect rect, string text, GameFont font, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft, bool ellipsis = true)
        {
            using (TechLabDrawScope.Begin())
            {
                Text.Font = font;
                Text.Anchor = anchor;
                Text.WordWrap = !ellipsis;
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
            // Multi-layer paper stack edges below and beside the pages.
            Rect stackAnchor = new Rect(12f, 43f, windowRect.width - 24f, windowRect.height - 54f);
            Widgets.DrawBoxSolid(new Rect(stackAnchor.x - 5f, stackAnchor.y + 2f, stackAnchor.width + 10f, stackAnchor.height + 5f), PageEdgeDark);
            Widgets.DrawBoxSolid(new Rect(stackAnchor.x - 3f, stackAnchor.y + 1f, stackAnchor.width + 6f, stackAnchor.height + 4f), PageEdgeLight);
            Widgets.DrawBoxSolid(new Rect(stackAnchor.x, stackAnchor.y, stackAnchor.width, stackAnchor.height + 3f), PageEdgeLight);

            // Outer book cover
            DrawBox(windowRect, CoverBg, CoverBorder, 2);
            DrawBox(windowRect.ContractedBy(2f), Color.clear, CoverRim, 1);

            const float padX = 18f;
            const float padTop = 12f;
            const float padBottom = 16f;

            headerRect = new Rect(padX, padTop, windowRect.width - padX * 2f, headerHeight);
            float bodyTop = headerRect.yMax + 8f;
            float bodyHeight = windowRect.height - bodyTop - padBottom;
            Rect bodyRect = new Rect(padX, bodyTop, windowRect.width - padX * 2f, bodyHeight);

            const float spineWidth = 22f;
            float pageWidth = Mathf.Floor((bodyRect.width - spineWidth) * 0.5f);
            float rightWidth = bodyRect.width - spineWidth - pageWidth;

            leftPageRect = new Rect(bodyRect.x, bodyRect.y, pageWidth, bodyRect.height);
            Rect spineRect = new Rect(leftPageRect.xMax, bodyRect.y, spineWidth, bodyRect.height);
            rightPageRect = new Rect(spineRect.xMax, bodyRect.y, rightWidth, bodyRect.height);

            // Left page + outer shading + right-edge spine fold shadow
            Widgets.DrawBoxSolid(leftPageRect, PageBg);
            Widgets.DrawBoxSolid(new Rect(leftPageRect.x, leftPageRect.y, 8f, leftPageRect.height), PageOuterEdge);
            DrawSpineFoldShadow(leftPageRect, onRightEdge: true);

            // Right page + outer shading + left-edge spine fold shadow
            Widgets.DrawBoxSolid(rightPageRect, PageBg);
            Widgets.DrawBoxSolid(new Rect(rightPageRect.xMax - 8f, rightPageRect.y, 8f, rightPageRect.height), PageOuterEdge);
            DrawSpineFoldShadow(rightPageRect, onRightEdge: false);

            // Center book spine groove & binding stitches
            Widgets.DrawBoxSolid(spineRect, SpineBg);
            Widgets.DrawBoxSolid(new Rect(spineRect.center.x - 3f, spineRect.y, 6f, spineRect.height), SpineGroove);
            Color prev = GUI.color;
            GUI.color = SpineStitch;
            float stitchX = Mathf.Round(spineRect.center.x) - 1f;
            for (float y = spineRect.y + 10f; y < spineRect.yMax - 10f; y += 15f)
            {
                float h = Mathf.Min(7f, spineRect.yMax - 10f - y);
                GUI.DrawTexture(new Rect(stitchX, y, 2f, h), BaseContent.WhiteTex);
            }

            GUI.color = prev;

            // Outer spread border
            DrawBox(bodyRect, Color.clear, SpreadBorder, 1);
        }

        private static void DrawSpineFoldShadow(Rect pageRect, bool onRightEdge)
        {
            if (SpineShade == null)
            {
                return;
            }

            const float shadeWidth = 42f;
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
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

        public static bool DrawActionButton(Rect rect, string label, Color roleBorder, bool enabled)
        {
            bool over = enabled && Mouse.IsOver(rect);
            bool down = over && Input.GetMouseButton(0);

            Color fill = !enabled ? new Color(BtnBg.r, BtnBg.g, BtnBg.b, 0.42f)
                : down ? BtnActiveBg
                : over ? BtnHoverBg
                : BtnBg;
            Color border = !enabled ? new Color(roleBorder.r, roleBorder.g, roleBorder.b, 0.35f)
                : over ? BtnHoverBorder
                : roleBorder;
            Color textColor = enabled ? Color.white : new Color(TextSubtle.r, TextSubtle.g, TextSubtle.b, 0.55f);

            DrawBox(rect, fill, border, 1);
            if (enabled)
            {
                Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 1f),
                    new Color(1f, 1f, 1f, 0.08f));
            }

            DrawLabel(rect.ContractedBy(6f, 0f), label, GameFont.Small, textColor, TextAnchor.MiddleCenter);
            return enabled && Widgets.ButtonInvisible(rect, doMouseoverSound: true);
        }

        public static void DrawCostChip(Rect rect, Texture2D icon, ThingDef thingDef, string text, string countText,
            Color bgColor, Color borderColor, Color textColor, Color countColor, string tooltip = null)
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
                        DrawIcon(iconInner, icon, textColor);
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
            Color textColor, Color countColor, string tooltip, System.Action<Rect> drawIconContent)
        {
            DrawBox(rect, bgColor, borderColor, 1);

            Rect iconBox = new Rect(rect.x + 5f, rect.y + (rect.height - 24f) * 0.5f, 24f, 24f);
            DrawBox(iconBox, ChipIconBg, ChipIconBorder, 1);
            drawIconContent?.Invoke(iconBox.ContractedBy(2f));

            float x = iconBox.xMax + 6f;
            if (!text.NullOrEmpty())
            {
                float labelW = CalcTextWidth(text, GameFont.Small);
                float maxLabelW = countText.NullOrEmpty()
                    ? Mathf.Max(0f, rect.xMax - x - 6f)
                    : Mathf.Min(labelW + 2f, Mathf.Max(0f, rect.xMax - x - 44f));
                DrawLabel(new Rect(x, rect.y, maxLabelW, rect.height), text, GameFont.Small, textColor,
                    TextAnchor.MiddleLeft, ellipsis: true);
                x += maxLabelW + 5f;
            }

            if (!countText.NullOrEmpty())
            {
                float countW = Mathf.Max(0f, rect.xMax - x - 6f);
                DrawLabel(new Rect(x, rect.y, countW, rect.height), countText, GameFont.Small, countColor,
                    TextAnchor.MiddleLeft, ellipsis: false);
            }

            if (!tooltip.NullOrEmpty())
            {
                TooltipHandler.TipRegion(rect, tooltip);
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

        public static void PlayClick()
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
        }
    }
}
