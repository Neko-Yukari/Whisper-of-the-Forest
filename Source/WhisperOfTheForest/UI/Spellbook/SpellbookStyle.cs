using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Spellbook-specific palette accents, glyph/socket textures, micro-summary formatting,
    /// and inlaid text field. All shared book-shell chrome, vellum cards, ledgers, chips, buttons
    /// and labels live in <see cref="BookUi"/>.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class SpellbookStyle
    {
        public static int MaxGlyphs => SpellDefs.Tuning.maxGlyphs;

        // Shared palette forwards
        public static Color CoverText => BookUi.CoverText;
        public static Color CoverTextDim => BookUi.CoverTextDim;
        public static Color CoverGold => BookUi.CoverGold;
        public static Color CoverWarning => BookUi.CoverWarning;

        public static Color Ink => BookUi.InkPrimary;
        public static Color InkDim => BookUi.InkSecondary;
        public static Color InkFaint => BookUi.InkMuted;
        public static Color Good => BookUi.BotanicalJade;
        public static Color Warning => BookUi.RubricRed;

        public static Color Jade => BookUi.BtnJade;
        public static readonly Color JadeBright = new Color(0.45f, 0.86f, 0.74f, 1f);
        public static Color Walnut => BookUi.BtnWalnut;
        public static Color Rust => BookUi.BtnRust;

        /// <summary>Light text on the inlaid name/search fields.</summary>
        public static readonly Color FieldText = new Color(0.94f, 0.90f, 0.78f, 1f);

        /// <summary>Recessed fill for sockets sealed by a lower-tier spellbook.</summary>
        public static readonly Color SealedSocketBg = new Color(0.16f, 0.14f, 0.11f, 0.36f);
        public static readonly Color SealedSocketBorder = new Color(0.42f, 0.36f, 0.28f, 0.65f);
        public static readonly Color AnchorHighlight = new Color(0.84f, 0.59f, 0.14f, 0.95f);

        /// <summary>Alpha of a catalogue rune the current bar cannot take: still readable, visibly off.</summary>
        public const float BlockedAlpha = 0.44f;

        // Shared textures forwarded from BookUi
        public static Texture2D SpineShade => BookUi.SpineShade;
        public static Texture2D ButtonAtlas => BookUi.ButtonAtlas;
        public static Texture2D ManaCrystal => BookUi.ManaCrystal;

        // Spellbook-only widget textures
        private const string Ui = "Spellbook/UI/";
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        public static Texture2D InlayAtlas => Tex(Ui + "InlayAtlas");
        public static Texture2D Socket => Tex(Ui + "Socket");
        public static Texture2D Glow => Tex(Ui + "Glow");
        public static Texture2D VineTrack => Tex(Ui + "VineTrack");
        public static Texture2D IconOk => Tex(Ui + "IconOk");
        public static Texture2D IconWarn => Tex(Ui + "IconWarn");
        public static Texture2D IconRemove => Tex(Ui + "IconRemove");
        public static Texture2D IconSearch => Tex(Ui + "IconSearch");
        public static Texture2D IconHourglass => Tex(Ui + "IconHourglass");

        public static Texture2D GlyphIcon(SpellGlyphDef glyph)
        {
            return glyph == null ? null : Tex(SpellDefs.IconPathFor(glyph));
        }

        public static Texture2D Tex(string path)
        {
            if (path.NullOrEmpty())
            {
                return null;
            }

            if (textures.TryGetValue(path, out Texture2D texture))
            {
                return texture;
            }

            texture = ContentFinder<Texture2D>.Get(path, true);
            textures[path] = texture;
            return texture;
        }

        // ---------------------------------------------------------------- strings & semantic labels

        public static string T(string key) => Translator.Translate(key);

        public static string TF(string key, params object[] args) =>
            string.Format(Translator.Translate(key), args);

        public static string Seconds(int ticks) =>
            ((float)ticks / GenTicks.TicksPerRealSecond).ToString("0.##");

        public static string KindLabel(SpellGlyphKind kind)
        {
            switch (kind)
            {
                case SpellGlyphKind.Form:
                    return T("WotF_Spellbook.KindForm");
                case SpellGlyphKind.Effect:
                    return T("WotF_Spellbook.KindEffect");
                case SpellGlyphKind.Augment:
                    return T("WotF_Spellbook.KindAugment");
                case SpellGlyphKind.Super:
                    return T("WotF_Spellbook.KindSuper");
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        public static Color KindColor(SpellGlyphKind kind)
        {
            switch (kind)
            {
                case SpellGlyphKind.Form:
                    return new Color(0.11f, 0.44f, 0.39f, 1f);
                case SpellGlyphKind.Effect:
                    return new Color(0.64f, 0.31f, 0.09f, 1f);
                case SpellGlyphKind.Augment:
                    return new Color(0.42f, 0.25f, 0.58f, 1f);
                case SpellGlyphKind.Super:
                    return new Color(0.65f, 0.15f, 0.25f, 1f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        public static string FormLabel(SpellFormKind form)
        {
            switch (form)
            {
                case SpellFormKind.Self:
                    return T("WotF_Spellbook.FormSelf");
                case SpellFormKind.Touch:
                    return T("WotF_Spellbook.FormTouch");
                case SpellFormKind.Projectile:
                    return T("WotF_Spellbook.FormProjectile");
                case SpellFormKind.Orbit:
                    return T("WotF_Spellbook.FormOrbit");
                case SpellFormKind.Special:
                    return T("WotF_Spellbook.FormSpecial");
                default:
                    return form.ToString();
            }
        }

        public static string TargetClassLabel(SpellTargetClass targetClass)
        {
            switch (targetClass)
            {
                case SpellTargetClass.Humanlike:
                    return T("WotF_Spellbook.TargetHumanlike");
                case SpellTargetClass.Animal:
                    return T("WotF_Spellbook.TargetAnimal");
                case SpellTargetClass.Mechanoid:
                    return T("WotF_Spellbook.TargetMechanoid");
                case SpellTargetClass.Entity:
                    return T("WotF_Spellbook.TargetEntity");
                case SpellTargetClass.Building:
                    return T("WotF_Spellbook.TargetBuilding");
                case SpellTargetClass.Rock:
                    return T("WotF_Spellbook.TargetRock");
                case SpellTargetClass.Plant:
                    return T("WotF_Spellbook.TargetPlant");
                case SpellTargetClass.Item:
                    return T("WotF_Spellbook.TargetItem");
                case SpellTargetClass.Cell:
                    return T("WotF_Spellbook.TargetCell");
                default:
                    return targetClass.ToString();
            }
        }

        /// <summary>
        /// Compact single-line numeric micro-summary derived purely from the glyph Def's XML fields.
        /// Used on both left-page catalogue cards and the bottom rune inspector card.
        /// </summary>
        public static string GlyphMicroSummary(SpellGlyphDef glyph)
        {
            if (glyph == null)
            {
                return string.Empty;
            }

            switch (glyph.kind)
            {
                case SpellGlyphKind.Form:
                    if (glyph.projectile != null)
                    {
                        return TF("WotF_Spellbook.MicroProjectile", glyph.projectile.missileCount, glyph.range.ToString("0.#"));
                    }
                    if (glyph.orbit != null)
                    {
                        return TF("WotF_Spellbook.MicroOrbit", glyph.orbit.count, Seconds(glyph.orbit.lifetimeTicks));
                    }
                    if (glyph.defName == SpellDefs.FormTouch)
                    {
                        return TF("WotF_Spellbook.MicroTouch", glyph.range.ToString("0.#"));
                    }
                    return T("WotF_Spellbook.MicroSelf");

                case SpellGlyphKind.Super:
                    if (glyph.railgun != null)
                    {
                        return TF("WotF_Spellbook.MicroRailgun", glyph.railgun.damage.ToString("0.#"), glyph.railgun.hitWidth.ToString("0.#"));
                    }
                    if (glyph.tornado != null)
                    {
                        return TF("WotF_Spellbook.MicroTornado", glyph.tornado.damagePerPulse.ToString("0.#"), Seconds(glyph.tornado.durationTicks));
                    }
                    return T("WotF_Spellbook.KindSuper");

                case SpellGlyphKind.Augment:
                    if (glyph.defName == SpellDefs.AugmentBreakArmor)
                    {
                        return TF("WotF_Spellbook.MicroBreakArmor", glyph.factor.ToStringPercent("0"));
                    }
                    if (glyph.defName == SpellDefs.AugmentAoe)
                    {
                        return TF("WotF_Spellbook.MicroAoe", glyph.radius.ToString("0.#"));
                    }
                    if (glyph.defName == SpellDefs.AugmentSplit)
                    {
                        return TF("WotF_Spellbook.MicroSplit", glyph.missiles);
                    }
                    if (glyph.defName == SpellDefs.AugmentPierce)
                    {
                        return TF("WotF_Spellbook.MicroPierce", glyph.missiles);
                    }
                    if (glyph.defName == SpellDefs.AugmentSensitive)
                    {
                        return T("WotF_Spellbook.MicroSensitive");
                    }
                    if (glyph.factor > 0f)
                    {
                        return TF("WotF_Spellbook.MicroFactor", glyph.factor.ToString("0.##"));
                    }
                    return KindLabel(glyph.kind);

                case SpellGlyphKind.Effect:
                    if (glyph is HarmGlyphDef harm)
                    {
                        return TF("WotF_Spellbook.MicroHarm", harm.damage.ToString("0.#"), harm.armorPenetration.ToStringPercent("0"));
                    }
                    if (glyph is HealGlyphDef heal)
                    {
                        return TF("WotF_Spellbook.MicroHeal", heal.baseTendQuality.ToStringPercent("0"), Seconds(heal.clottingDurationTicks));
                    }
                    if (glyph is ExplosionGlyphDef exp)
                    {
                        return TF("WotF_Spellbook.MicroExplosion", exp.blastDamage.ToString("0.#"), exp.radius.ToString("0.#"));
                    }
                    if (glyph is LightningGlyphDef bolt)
                    {
                        return TF("WotF_Spellbook.MicroLightning", bolt.burnDamage.ToString("0.#"), bolt.empAmount.ToString("0.#"));
                    }
                    if (glyph is ColdSnapGlyphDef cold)
                    {
                        return TF("WotF_Spellbook.MicroColdSnap", cold.damage.ToString("0.#"), cold.vulnerableFactor.ToString("0.#"));
                    }
                    if (glyph is FreezeGlyphDef freeze)
                    {
                        return TF("WotF_Spellbook.MicroFreeze", Seconds(freeze.freezeDurationTicks));
                    }
                    if (glyph is SnareGlyphDef snare)
                    {
                        return TF("WotF_Spellbook.MicroSnare", Seconds(snare.rootDurationTicks));
                    }
                    if (glyph is HexGlyphDef hex)
                    {
                        return TF("WotF_Spellbook.MicroHex", Seconds(hex.hexDurationTicks));
                    }
                    if (glyph is KnockbackGlyphDef kb)
                    {
                        return TF("WotF_Spellbook.MicroKnockback", kb.pushDistance.ToString("0.#"));
                    }
                    if (glyph is PullGlyphDef pull)
                    {
                        return TF("WotF_Spellbook.MicroPull", pull.pullDistance.ToString("0.#"));
                    }
                    if (glyph is BlinkGlyphDef blink)
                    {
                        return TF("WotF_Spellbook.MicroBlink", blink.selfForwardDistance.ToString("0.#"));
                    }
                    if (glyph is GrowGlyphDef grow)
                    {
                        return TF("WotF_Spellbook.MicroGrow", grow.growthRate.ToString("0.#"), Seconds(grow.fertilityDurationTicks));
                    }
                    if (glyph is HackGlyphDef hack)
                    {
                        return TF("WotF_Spellbook.MicroHack", hack.hackStrength.ToString("0.#"), Seconds(hack.controlTicks));
                    }
                    if (glyph is MagelightGlyphDef light)
                    {
                        return TF("WotF_Spellbook.MicroMagelight", light.glowRadius.ToString("0.#"), Seconds(light.lifetimeTicks));
                    }
                    if (glyph is PlanarShieldGlyphDef shield)
                    {
                        return TF("WotF_Spellbook.MicroPlanarShield", shield.baseCharges, (shield.radius * 2f).ToString("0.#"));
                    }
                    if (glyph is DelayGlyphDef delay)
                    {
                        return TF("WotF_Spellbook.MicroDelay", Seconds(delay.delayTicks));
                    }
                    if (glyph is LingerGlyphDef linger)
                    {
                        return TF("WotF_Spellbook.MicroLinger", linger.radius.ToString("0.#"), Seconds(linger.lifetimeTicks));
                    }
                    if (glyph is RuneGlyphDef rune)
                    {
                        return TF("WotF_Spellbook.MicroRune", Seconds(rune.lifetimeTicks));
                    }
                    return KindLabel(glyph.kind);

                default:
                    return KindLabel(glyph.kind);
            }
        }

        // ---------------------------------------------------------------- shared BookUi forwards

        public static void DrawLabel(Rect rect, string text, GameFont font, Color color, TextAnchor anchor) =>
            BookUi.DrawLabel(rect, text, font, color, anchor, ellipsis: true);

        public static void DrawLabel(Rect rect, string text, GameFont font, Color color) =>
            BookUi.DrawLabel(rect, text, font, color, TextAnchor.UpperLeft, ellipsis: true);

        public static float DrawParagraph(Rect rect, string text, GameFont font, Color color)
        {
            float height = Mathf.Min(rect.height, BookUi.CalcTextHeight(text, font, rect.width));
            BookUi.DrawWrappedLabel(new Rect(rect.x, rect.y, rect.width, height), text, font, color);
            return height;
        }

        public static float TextWidth(string text, GameFont font) => BookUi.CalcTextWidth(text, font);

        public static void DrawIcon(Rect rect, Texture2D texture, Color tint) => BookUi.DrawIcon(rect, texture, tint);
        public static void DrawStretched(Rect rect, Texture2D texture, Color tint) => BookUi.DrawStretched(rect, texture, tint);
        public static void DrawAtlas(Rect rect, Texture2D atlas, Color tint) => BookUi.DrawAtlas(rect, atlas, tint);
        public static Color Fade(Color color, float alpha) => BookUi.Fade(color, alpha);
        public static Color Lighten(Color color, float amount) => BookUi.Lighten(color, amount);

        public static void DrawPageTitle(Rect titleRect, Rect dividerRect, string text) =>
            BookUi.DrawPageTitle(titleRect, dividerRect, text);
        public static void DrawPanel(Rect rect, Color tint) => BookUi.DrawPanel(rect, tint);
        public static bool Button(Rect rect, string label, BookButtonRole role, bool enabled) =>
            BookUi.DrawButton(rect, label, role, enabled);

        // ---------------------------------------------------------------- spellbook-only controls

        public static void DrawGlow(Vector2 center, float size, Color color, float alpha)
        {
            if (alpha <= 0f)
            {
                return;
            }

            BookUi.DrawStretched(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), Glow,
                new Color(color.r, color.g, color.b, color.a * alpha));
        }

        public static void DrawSocket(Rect rect, Texture2D icon, float alpha)
        {
            BookUi.DrawIcon(rect, Socket, new Color(1f, 1f, 1f, alpha));
            if (icon != null)
            {
                float inset = rect.width * 0.12f;
                BookUi.DrawIcon(new Rect(rect.x + inset, rect.y + inset, rect.width - 2f * inset, rect.height - 2f * inset), icon,
                    new Color(1f, 1f, 1f, alpha));
            }
        }

        private static GUIStyle fieldStyle;

        private static GUIStyle FieldStyle
        {
            get
            {
                if (fieldStyle == null)
                {
                    fieldStyle = new GUIStyle(Text.textFieldStyles[1]);
                    GUIStyleState[] states =
                    {
                        fieldStyle.normal, fieldStyle.hover, fieldStyle.focused, fieldStyle.active,
                        fieldStyle.onNormal, fieldStyle.onHover, fieldStyle.onFocused, fieldStyle.onActive
                    };
                    for (int i = 0; i < states.Length; i++)
                    {
                        states[i].background = null;
                        states[i].textColor = FieldText;
                    }
                }

                return fieldStyle;
            }
        }

        public static string TextField(Rect rect, string text, string placeholder, Texture2D leadingIcon)
        {
            BookUi.DrawAtlas(rect, InlayAtlas, Color.white);

            Rect inner = new Rect(rect.x + 6f, rect.y, rect.width - 10f, rect.height);
            if (leadingIcon != null)
            {
                float size = Mathf.Min(14f, rect.height - 8f);
                BookUi.DrawIcon(new Rect(inner.x, rect.y + (rect.height - size) * 0.5f, size, size), leadingIcon,
                    BookUi.Fade(FieldText, 0.55f));
                inner.xMin += size + 4f;
            }

            if (text.NullOrEmpty() && !placeholder.NullOrEmpty())
            {
                BookUi.DrawLabel(inner, placeholder, GameFont.Tiny, BookUi.Fade(FieldText, 0.45f), TextAnchor.MiddleLeft);
            }

            string result;
            using (TechLabDrawScope.Begin())
            {
                Text.Font = GameFont.Small;
                result = GUI.TextField(inner, text ?? "", FieldStyle);
            }

            return result ?? "";
        }
    }
}
