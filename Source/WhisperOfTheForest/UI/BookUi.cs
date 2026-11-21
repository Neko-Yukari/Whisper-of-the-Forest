using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace WhisperOfTheForest
{
    /// <summary>Colour role shared by buttons in both the spellbook and the ritual codex.</summary>
    internal enum BookButtonRole
    {
        Primary,
        Secondary,
        Danger
    }

    /// <summary>
    /// Shared Livingwood &amp; Birch-Vellum codex UI kit used by both <see cref="Window_Spellbook"/>
    /// and <see cref="WhisperOfTheForest.Rituals.Window_RitualHost"/>. Owns the common palette,
    /// nine-slice cover/page/panel/button textures, stitched leather spine, page surfaces, buttons,
    /// labels, close button, and frameless book window setup so both books stay visually and
    /// structurally unified.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class BookUi
    {
        private const string SharedDir = "UI/Ritual/";

        // Cover & stitched spine
        public static readonly Color CoverText = new Color(0.918f, 0.894f, 0.843f, 1f);
        public static readonly Color CoverTextDim = new Color(0.737f, 0.706f, 0.620f, 1f);
        public static readonly Color CoverGold = new Color(0.988f, 0.886f, 0.612f, 1f);
        public static readonly Color CoverBorder = new Color(0.898f, 0.663f, 0.231f, 0.80f);
        public static readonly Color CoverRim = new Color(0.898f, 0.663f, 0.231f, 0.25f);
        public static readonly Color CoverWarning = new Color(1f, 0.58f, 0.48f, 1f);

        public static readonly Color SpineBg = new Color(0.082f, 0.145f, 0.110f, 1f);
        public static readonly Color SpineGroove = new Color(0.047f, 0.086f, 0.067f, 1f);
        public static readonly Color SpineStitch = new Color(0.898f, 0.663f, 0.231f, 0.42f);

        // Ink & semantic accents on Birch-Vellum pages
        public static readonly Color InkPrimary = new Color(0.169f, 0.129f, 0.078f, 1f);
        public static readonly Color InkSecondary = new Color(0.349f, 0.294f, 0.220f, 1f);
        public static readonly Color InkMuted = new Color(0.510f, 0.451f, 0.369f, 1f);
        public static readonly Color RubricRed = new Color(0.620f, 0.169f, 0.110f, 1f);
        public static readonly Color BotanicalJade = new Color(0.122f, 0.420f, 0.271f, 1f);
        public static readonly Color CrystalBlue = new Color(0.078f, 0.357f, 0.541f, 1f);
        public static readonly Color GoldAccent = new Color(0.784f, 0.561f, 0.157f, 1f);
        public static readonly Color AmberAccent = new Color(0.710f, 0.467f, 0.110f, 1f);
        public static readonly Color HairlineInk = new Color(0.320f, 0.250f, 0.160f, 0.26f);
        public static readonly Color SubtleRule = new Color(0.420f, 0.340f, 0.230f, 0.20f);

        // Shared Birch-Vellum cards, ledgers, chips & seals
        public static readonly Color CardVellumNormal = new Color(0.840f, 0.780f, 0.655f, 0.42f);
        public static readonly Color CardVellumHover = new Color(0.910f, 0.855f, 0.735f, 0.68f);
        public static readonly Color CardVellumSel = new Color(0.965f, 0.915f, 0.790f, 0.88f);
        public static readonly Color CardVellumRun = new Color(0.760f, 0.885f, 0.790f, 0.66f);
        public static readonly Color CardVellumRunSel = new Color(0.820f, 0.930f, 0.845f, 0.86f);
        public static readonly Color CardVellumLocked = new Color(0.760f, 0.710f, 0.620f, 0.24f);
        public static readonly Color CardVellumLockedSel = new Color(0.835f, 0.785f, 0.690f, 0.52f);
        public static readonly Color CardVellumWarn = new Color(0.945f, 0.835f, 0.795f, 0.82f);

        public static readonly Color LedgerBg = new Color(0.835f, 0.775f, 0.655f, 0.44f);
        public static readonly Color LedgerHighlightBg = new Color(0.925f, 0.835f, 0.610f, 0.60f);
        public static readonly Color LedgerBorder = new Color(0.440f, 0.350f, 0.230f, 0.50f);
        public static readonly Color LedgerHighlightBorder = new Color(0.784f, 0.561f, 0.157f, 0.85f);

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
        public static readonly Color SealText = new Color(0.980f, 0.965f, 0.930f, 1f);

        // Action button roles
        public static readonly Color BtnJade = new Color(0.180f, 0.545f, 0.341f, 1f);
        public static readonly Color BtnWalnut = new Color(0.470f, 0.355f, 0.220f, 1f);
        public static readonly Color BtnRust = new Color(0.620f, 0.169f, 0.110f, 1f);

        // Shared book-shell textures
        public static readonly Texture2D Cover = ContentFinder<Texture2D>.Get(SharedDir + "RitualCover", true);
        public static readonly Texture2D CoverFrameAtlas = ContentFinder<Texture2D>.Get(SharedDir + "RitualCoverFrameAtlas", true);
        public static readonly Texture2D Page = ContentFinder<Texture2D>.Get(SharedDir + "RitualPage", true);
        public static readonly Texture2D PageFrameAtlas = ContentFinder<Texture2D>.Get(SharedDir + "RitualPageFrameAtlas", true);
        public static readonly Texture2D PanelAtlas = ContentFinder<Texture2D>.Get(SharedDir + "RitualPanelAtlas", true);
        public static readonly Texture2D ButtonAtlas = ContentFinder<Texture2D>.Get(SharedDir + "RitualButtonAtlas", true);
        public static readonly Texture2D Divider = ContentFinder<Texture2D>.Get(SharedDir + "RitualDivider", true);
        public static readonly Texture2D SpineShade = ContentFinder<Texture2D>.Get(SharedDir + "SpineShade", true);
        public static readonly Texture2D ManaCrystal = ContentFinder<Texture2D>.Get(SharedDir + "ManaCrystal", true);

        // ---------------------------------------------------------------- window chrome

        public static void ConfigureBookWindow(Window window, bool onlyOneOfType)
        {
            window.doWindowBackground = false;
            window.doCloseX = false;
            window.doCloseButton = false;
            window.draggable = true;
            window.forcePause = false;
            window.preventCameraMotion = false;
            window.absorbInputAroundWindow = false;
            window.closeOnClickedOutside = false;
            window.onlyOneOfTypeAllowed = onlyOneOfType;
            window.closeOnAccept = false;
            window.closeOnCancel = true;
            window.drawShadow = true;
        }

        public static Rect CenteredWindowRect(Vector2 size)
        {
            float x = Mathf.Clamp((UI.screenWidth - size.x) * 0.5f, 0f, Mathf.Max(0f, UI.screenWidth - size.x));
            float y = Mathf.Clamp((UI.screenHeight - size.y) * 0.5f, 0f, Mathf.Max(0f, UI.screenHeight - size.y));
            return GenUI.Rounded(new Rect(x, y, size.x, size.y));
        }

        // ---------------------------------------------------------------- book surfaces

        public static void DrawCover(Rect rect)
        {
            DrawStretched(rect, Cover, Color.white);
            DrawAtlas(rect, CoverFrameAtlas, Color.white);
        }

        public static void DrawPage(Rect pageRect, bool spineOnRight)
        {
            DrawStretched(pageRect, Page, Color.white);
            DrawSpineFoldShadow(pageRect, spineOnRight);
            DrawAtlas(pageRect, PageFrameAtlas, Color.white);
        }

        public static void DrawSpine(Rect spineRect)
        {
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

        public static void DrawSpineFoldShadow(Rect pageRect, bool onRightEdge)
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

        public static void DrawPageTitle(Rect titleRect, Rect dividerRect, string text)
        {
            DrawLabel(titleRect, text, GameFont.Medium, InkPrimary, TextAnchor.MiddleCenter);
            DrawIcon(dividerRect, Divider, Color.white);
        }

        public static void DrawCloseButton(Rect closeRect, Action onClose)
        {
            bool overClose = Mouse.IsOver(closeRect);
            if (overClose)
            {
                Widgets.DrawBoxSolidWithOutline(closeRect, new Color(1f, 1f, 1f, 0.08f), CoverBorder, 1);
            }

            DrawIcon(
                closeRect.ContractedBy(4f),
                TexButton.CloseXSmall,
                overClose ? CoverGold : CoverTextDim);
            TooltipHandler.TipRegion(closeRect, "CloseButton".Translate());
            if (Widgets.ButtonInvisible(closeRect, doMouseoverSound: true))
            {
                onClose?.Invoke();
            }
        }

        // ---------------------------------------------------------------- controls & primitives

        public static bool DrawButton(Rect rect, string label, BookButtonRole role, bool enabled)
        {
            Color baseColor = role == BookButtonRole.Primary ? BtnJade
                : role == BookButtonRole.Danger ? BtnRust
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
            Color textColor = enabled ? CoverText : Fade(CoverText, 0.52f);
            DrawLabel(rect.ContractedBy(6f, 0f), label, GameFont.Small, textColor,
                TextAnchor.MiddleCenter, ellipsis: true);
            return enabled && Widgets.ButtonInvisible(rect, doMouseoverSound: true);
        }

        public static void DrawPanel(Rect rect, Color tint)
        {
            DrawAtlas(rect, PanelAtlas, tint);
        }

        public static void DrawBox(Rect rect, Color fill, Color border, int thickness = 1)
        {
            Widgets.DrawBoxSolidWithOutline(rect, fill, border, thickness);
        }

        public static void ComputeBookSpread(Rect windowRect, float headerHeight, out Rect headerRect,
            out Rect leftPageRect, out Rect spineRect, out Rect rightPageRect)
        {
            const float frameInset = 14f;
            const float headerPadX = 10f;
            const float spineWidth = 14f;

            headerRect = new Rect(
                windowRect.x + frameInset + headerPadX,
                windowRect.y + frameInset - 2f,
                windowRect.width - (frameInset + headerPadX) * 2f,
                headerHeight);

            float bodyTop = headerRect.yMax + 6f;
            float bodyHeight = Mathf.Max(140f, windowRect.height - bodyTop - frameInset);
            Rect bodyRect = new Rect(windowRect.x + frameInset, bodyTop, windowRect.width - frameInset * 2f, bodyHeight);

            float pageWidth = Mathf.Floor((bodyRect.width - spineWidth) * 0.5f);
            float rightWidth = bodyRect.width - spineWidth - pageWidth;

            leftPageRect = new Rect(bodyRect.x, bodyRect.y, pageWidth, bodyRect.height);
            spineRect = new Rect(leftPageRect.xMax, bodyRect.y, spineWidth, bodyRect.height);
            rightPageRect = new Rect(spineRect.xMax, bodyRect.y, rightWidth, bodyRect.height);
        }

        public static void DrawBookSpread(Rect windowRect, float headerHeight, out Rect headerRect,
            out Rect leftPageRect, out Rect rightPageRect)
        {
            DrawCover(windowRect);
            ComputeBookSpread(windowRect, headerHeight, out headerRect, out leftPageRect, out Rect spineRect,
                out rightPageRect);
            DrawPage(leftPageRect, spineOnRight: true);
            DrawPage(rightPageRect, spineOnRight: false);
            DrawSpine(spineRect);
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

        public static void DrawInfoChip(Rect rect, string label, string value, Color bg, Color border,
            Color labelColor, Color valueColor, Texture2D icon = null, Color? iconTint = null, string tooltip = null)
        {
            DrawBox(rect, bg, border, 1);

            float x = rect.x + 6f;
            if (icon != null)
            {
                float iconSize = Mathf.Min(14f, rect.height - 6f);
                DrawIcon(new Rect(x, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize),
                    icon, iconTint ?? valueColor);
                x += iconSize + 4f;
            }

            bool hasValue = !value.NullOrEmpty();
            float rightPad = 6f;
            float valueW = hasValue ? Mathf.Min(rect.xMax - x - rightPad, Mathf.Ceil(CalcTextWidth(value, GameFont.Small)) + 2f) : 0f;
            float labelAvail = Mathf.Max(0f, rect.xMax - rightPad - valueW - (hasValue ? 4f : 0f) - x);

            if (!label.NullOrEmpty() && labelAvail > 4f)
            {
                DrawLabel(new Rect(x, rect.y, labelAvail, rect.height), label, GameFont.Tiny, labelColor,
                    TextAnchor.MiddleLeft, ellipsis: true);
            }

            if (hasValue && valueW > 0f)
            {
                DrawLabel(new Rect(rect.xMax - rightPad - valueW, rect.y, valueW, rect.height), value,
                    GameFont.Small, valueColor, TextAnchor.MiddleRight, ellipsis: false);
            }

            if (!tooltip.NullOrEmpty())
            {
                TooltipHandler.TipRegion(rect, tooltip);
            }
        }

        public static void DrawPillBadge(Rect rect, string text, Color bg, Color border, Color textColor,
            GameFont font = GameFont.Tiny, string tooltip = null)
        {
            DrawBox(rect, bg, border, 1);
            DrawLabel(rect.ContractedBy(4f, 0f), text, font, textColor, TextAnchor.MiddleCenter, ellipsis: true);
            if (!tooltip.NullOrEmpty())
            {
                TooltipHandler.TipRegion(rect, tooltip);
            }
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

        public static Color Fade(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, color.a * alpha);
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
