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

        private static void SwitchGating()
        {
            Check.Equal(false, ManaSwitchUtility.ShouldOperate(false, true), "switched off with supply");
            Check.Equal(false, ManaSwitchUtility.ShouldOperate(true, false), "switched on without supply");
            Check.Equal(true, ManaSwitchUtility.ShouldOperate(true, true), "switched on with supply");
        }
    }
}
