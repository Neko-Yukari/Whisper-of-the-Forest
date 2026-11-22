using System;
using System.Collections.Generic;
using UnityEngine;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Vfx;

namespace WhisperOfTheForest.Tests
{
    internal static class StreetLampAndSwitchTests
    {
        internal static IEnumerable<(string, Action)> All => new (string, Action)[]
        {
            ("streetlamp: night factor maps day=0 midpoint=0.5 night=1", NightFactorMapping),
            ("streetlamp: cursor within 2 cells startles butterfly, outside does not", ButterflyFleeRadius),
            ("streetlamp: flee dust rate tapers monotonically from start factor to zero", FleeDustRateTapering),
            ("streetlamp: flee flash peaks briefly on startle then fades to zero", FleeFlashEnvelope),
            ("mana-switch: switched off halts machine regardless of supply", SwitchGating),
        };

        private static void NightFactorMapping()
        {
            float day = StreetLampVfxMath.EvaluateNightFactor(0.9f, 0.72f, 0.32f);
            float mid = StreetLampVfxMath.EvaluateNightFactor(0.52f, 0.72f, 0.32f);
            float night = StreetLampVfxMath.EvaluateNightFactor(0.15f, 0.72f, 0.32f);

            Check.True(Mathf.Approximately(day, 0f), $"expected day=0, got {day}");
            Check.True(Mathf.Abs(mid - 0.5f) < 0.001f, $"expected mid=0.5, got {mid}");
            Check.True(Mathf.Approximately(night, 1f), $"expected night=1, got {night}");
        }

        private static void ButterflyFleeRadius()
        {
            var butterfly = new Vector2(10f, 10f);
            Check.True(
                StreetLampVfxMath.ShouldStartleButterfly(new Vector2(11.4f, 11.4f), butterfly, 2.0f),
                "distance 1.98 < 2.0 should startle butterfly");
            Check.True(
                !StreetLampVfxMath.ShouldStartleButterfly(new Vector2(12.0f, 10.0f), butterfly, 2.0f),
                "distance 2.0 >= 2.0 should not startle butterfly");
        }

        private static void FleeDustRateTapering()
        {
            float r0 = StreetLampVfxMath.EvaluateFleeDustRateMultiplier(0f, 2.6f, 1.65f);
            float rMid = StreetLampVfxMath.EvaluateFleeDustRateMultiplier(0.5f, 2.6f, 1.65f);
            float rEnd = StreetLampVfxMath.EvaluateFleeDustRateMultiplier(1f, 2.6f, 1.65f);

            Check.True(Mathf.Approximately(r0, 2.6f), $"expected start rate 2.6, got {r0}");
            Check.True(r0 > rMid && rMid > 0f, $"expected 0 < mid < start, got mid={rMid}");
            Check.True(Mathf.Approximately(rEnd, 0f), $"expected end rate 0, got {rEnd}");
        }

        private static void FleeFlashEnvelope()
        {
            float fStart = StreetLampVfxMath.EvaluateFleeFlashIntensity(0f, 0.16f);
            float fPeak = StreetLampVfxMath.EvaluateFleeFlashIntensity(0.045f, 0.16f);
            float fTail = StreetLampVfxMath.EvaluateFleeFlashIntensity(0.11f, 0.16f);
            float fEnd = StreetLampVfxMath.EvaluateFleeFlashIntensity(0.16f, 0.16f);

            Check.True(Mathf.Approximately(fStart, 0f), $"expected t=0 flash=0, got {fStart}");
            Check.True(fPeak > 0.9f, $"expected flash peak > 0.9, got {fPeak}");
            Check.True(fTail > 0f && fTail < fPeak, $"expected tail between 0 and peak, got {fTail}");
            Check.True(Mathf.Approximately(fEnd, 0f), $"expected end flash=0, got {fEnd}");
        }

        private static void SwitchGating()
        {
            Check.Equal(false, ManaSwitchUtility.ShouldOperate(false, true), "switched off with supply");
            Check.Equal(false, ManaSwitchUtility.ShouldOperate(true, false), "switched on without supply");
            Check.Equal(true, ManaSwitchUtility.ShouldOperate(true, true), "switched on with supply");
        }
    }
}
