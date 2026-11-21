using System;
using System.Collections.Generic;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Vfx;

namespace WhisperOfTheForest.Tests
{
    /// <summary>
    /// Seam: theme template selection across glyph chains and DD2 vortex convergence / 2.5D depth
    /// masking curves used by SpellCastVfxWorker and RailgunLumenVfxWorker.
    /// </summary>
    internal static class SpellCastVfxSeamTests
    {
        internal static IEnumerable<(string, Action)> All => new (string, Action)[]
        {
            ("vfx theme: resolves first themed effect glyph in spell chain", FirstEffectThemeWins),
            ("vfx theme: falls back to tuning defaultTheme when glyphs carry no theme", FallsBackToDefaultTheme),
            ("vfx quick cast: routes prep ticks below quickCastMaxTicks and sets radialRelease only for Self", QuickCastRoutingAndRadialSelf),
            ("vfx payload theme: timing glyph resolves payload theme from inner effect glyphs before timing fallback", PayloadThemeResolvesInnerEffectFirst),
            ("vfx tornado sky: surges to detonation peak, settles to sustain, and fades cleanly to zero", TornadoSkyTintEnvelope),
            ("vfx convergence: hits full collapse at prepSeconds - leadCollapseSeconds", ConvergenceHitsEarlyAtLead),
            ("vfx pawn mask: north lies below pawn depth and south sits above torso", PawnMaskSplitsNorthSouthAltitudes),
        };

        private static void FirstEffectThemeWins()
        {
            var defaultTheme = new SpellThemeDef { defName = "T_Theme_Default" };
            var firstEffectTheme = new SpellThemeDef { defName = "T_Theme_Runic" };
            var secondEffectTheme = new SpellThemeDef { defName = "T_Theme_Starlight" };

            SpellThemeDef priorDefault = SpellDefs.Tuning.defaultTheme;
            SpellThemeDef priorHarm = Glyphs.Get(Glyphs.Harm).theme;
            SpellThemeDef priorWait = Glyphs.Get(Glyphs.Wait).theme;
            try
            {
                SpellDefs.Tuning.defaultTheme = defaultTheme;
                Glyphs.Get(Glyphs.Harm).theme = firstEffectTheme;
                Glyphs.Get(Glyphs.Wait).theme = secondEffectTheme;

                var plan = new SpellCastPlan
                {
                    glyphs = new List<string> { Glyphs.Projectile, Glyphs.FormAugment, Glyphs.Harm, Glyphs.Amplify, Glyphs.Wait }
                };

                Check.Equal(firstEffectTheme, SpellDefs.ResolveTheme(plan), "resolved theme");
            }
            finally
            {
                SpellDefs.Tuning.defaultTheme = priorDefault;
                Glyphs.Get(Glyphs.Harm).theme = priorHarm;
                Glyphs.Get(Glyphs.Wait).theme = priorWait;
            }
        }

        private static void FallsBackToDefaultTheme()
        {
            var defaultTheme = new SpellThemeDef { defName = "T_Theme_Fallback" };
            SpellThemeDef priorDefault = SpellDefs.Tuning.defaultTheme;
            SpellThemeDef priorTouch = Glyphs.Get(Glyphs.Touch).theme;
            try
            {
                SpellDefs.Tuning.defaultTheme = defaultTheme;
                Glyphs.Get(Glyphs.Touch).theme = null;

                var plan = new SpellCastPlan
                {
                    glyphs = new List<string> { Glyphs.Touch }
                };

                Check.Equal(defaultTheme, SpellDefs.ResolveTheme(plan), "fallback theme");
            }
            finally
            {
                SpellDefs.Tuning.defaultTheme = priorDefault;
                Glyphs.Get(Glyphs.Touch).theme = priorTouch;
            }
        }

        private static void ConvergenceHitsEarlyAtLead()
        {
            const float prep = 1.0f;
            const float lead = 0.10f;

            float atHalf = SpellCastVfxMath.ComputeConvergence(0.45f, prep, lead);
            float atCollapse = SpellCastVfxMath.ComputeConvergence(0.90f, prep, lead);
            float atEnd = SpellCastVfxMath.ComputeConvergence(1.00f, prep, lead);

            Check.True(Math.Abs(atHalf - 0.5f) < 0.001f, $"midpoint convergence expected ~0.5, got {atHalf}");
            Check.Equal(1f, atCollapse, "reaches full collapse 0.1s before cast completes");
            Check.Equal(1f, atEnd, "holds full collapse until release");
        }

        private static void PawnMaskSplitsNorthSouthAltitudes()
        {
            const float casterZ = 10f;
            const float half = 0.06f;
            const float frontY = 9.45f;
            const float backY = 7.95f;

            float northY = SpellCastVfxMath.EvaluatePawnMaskAltitude(casterZ + 0.15f, casterZ, half, frontY, backY);
            float centerY = SpellCastVfxMath.EvaluatePawnMaskAltitude(casterZ, casterZ, half, frontY, backY);
            float southY = SpellCastVfxMath.EvaluatePawnMaskAltitude(casterZ - 0.15f, casterZ, half, frontY, backY);

            Check.Equal(backY, northY, "north vertex occluded behind pawn depth");
            Check.Equal(frontY, southY, "south vertex drawn over pawn torso");
            Check.True(centerY < frontY && centerY > backY, $"center vertex smoothly interpolates, got {centerY}");
        }

        private static void QuickCastRoutingAndRadialSelf()
        {
            Check.Equal(false, SpellCastVfxMath.ShouldUseQuickCast(0, 60), "zero prep ticks do not play cast VFX");
            Check.Equal(true, SpellCastVfxMath.ShouldUseQuickCast(15, 60), "short prep (< threshold) uses quick-cast cloud");
            Check.Equal(true, SpellCastVfxMath.ShouldUseQuickCast(59, 60), "59 ticks uses quick-cast cloud");
            Check.Equal(false, SpellCastVfxMath.ShouldUseQuickCast(60, 60), "at threshold uses full 3D vortex");
            Check.Equal(false, SpellCastVfxMath.ShouldUseQuickCast(95, 60), "above threshold uses full 3D vortex");

            Check.Equal(true, SpellQuickCastVfxWorker.IsRadialCastRelease(SpellFormKind.Self), "Self form uses radial release");
            Check.Equal(false, SpellQuickCastVfxWorker.IsRadialCastRelease(SpellFormKind.Projectile), "Projectile form uses directional push");
            Check.Equal(false, SpellQuickCastVfxWorker.IsRadialCastRelease(SpellFormKind.Touch), "Touch form uses directional push");
            Check.Equal(false, SpellQuickCastVfxWorker.IsRadialCastRelease(SpellFormKind.Orbit), "Orbit form uses directional push");
        }

        private static void PayloadThemeResolvesInnerEffectFirst()
        {
            var lingerFallback = new SpellThemeDef { defName = "T_Theme_LingerFallback" };
            var healVitality = new SpellThemeDef { defName = "T_Theme_Vitality" };
            var unthemedGlyph = new SpellGlyphDef { defName = "T_Effect_Unthemed", kind = SpellGlyphKind.Effect };
            var themedGlyph = new SpellGlyphDef { defName = "T_Effect_Vitality", kind = SpellGlyphKind.Effect, theme = healVitality };

            var defsWithInnerTheme = new List<SpellGlyphDef> { Glyphs.Get(Glyphs.Touch), unthemedGlyph, themedGlyph };
            Check.Equal(healVitality, SpellDefs.ResolveTheme(defsWithInnerTheme, 1, defsWithInnerTheme.Count, lingerFallback),
                "inner payload glyph theme wins over timing glyph fallback");

            var defsWithoutInnerTheme = new List<SpellGlyphDef> { Glyphs.Get(Glyphs.Touch), unthemedGlyph };
            Check.Equal(lingerFallback, SpellDefs.ResolveTheme(defsWithoutInnerTheme, 1, defsWithoutInnerTheme.Count, lingerFallback),
                "timing glyph fallback used when payload glyphs carry no theme");
        }

        private static void TornadoSkyTintEnvelope()
        {
            const int spawn = 100;
            const int expires = 700;
            const int fade = 30;
            const int pulse = 60;
            const int rise = 8;
            const int decay = 28;
            const float peak = 0.76f;
            const float sustain = 0.48f;
            const float wave = 0.04f;

            float before = CompTornadoSkyTint.EvaluateLerpFactor(99, spawn, expires, fade, pulse, rise, decay, peak, sustain, wave);
            float atPeak = CompTornadoSkyTint.EvaluateLerpFactor(spawn + rise, spawn, expires, fade, pulse, rise, decay, peak, sustain, wave);
            float atSustainNode = CompTornadoSkyTint.EvaluateLerpFactor(spawn + 120, spawn, expires, fade, pulse, rise, decay, peak, sustain, wave);
            float midFade = CompTornadoSkyTint.EvaluateLerpFactor(expires + 15, spawn, expires, fade, pulse, rise, decay, peak, sustain, 0f);
            float afterFade = CompTornadoSkyTint.EvaluateLerpFactor(expires + fade, spawn, expires, fade, pulse, rise, decay, peak, sustain, wave);

            Check.Equal(0f, before, "zero before tornado spawns");
            Check.True(Math.Abs(atPeak - peak) < 0.001f, $"expected detonation peak {peak}, got {atPeak}");
            Check.True(Math.Abs(atSustainNode - sustain) < 0.001f, $"expected sustain {sustain} at full pulse period, got {atSustainNode}");
            Check.True(Math.Abs(midFade - sustain * 0.5f) < 0.001f, $"expected half-faded {sustain * 0.5f}, got {midFade}");
            Check.Equal(0f, afterFade, "zero once fadeTicks complete");
        }
    }
}
