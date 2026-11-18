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
    }
}
