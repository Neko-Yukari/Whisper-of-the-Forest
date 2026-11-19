using System;
using System.Collections.Generic;
using UnityEngine;
using WhisperOfTheForest.Shields;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Tests
{
    internal static class ShieldGeometryTests
    {
        internal static IEnumerable<(string, Action)> All => new (string, Action)[]
        {
            ("shield planar: segment stands perpendicular to effect->caster line", PlanarPerpendicularToCasterLine),
            ("shield planar: self cast steps forward along facing normal", PlanarSelfCastStepsForward),
            ("shield planar: crossing shot hits exact point while wide shot misses", PlanarSegmentIntersection),
            ("shield dome: blocks incoming chord on rim and ignores shots starting inside", DomeIncomingVsOutgoing),
            ("shield dome: pass-through crossing locates rim point for outgoing and incoming chords", DomePassThroughCrossing),
            ("shield spell: amplify scales charges with cap and AOE widens span with cap", PlanarGlyphCompileCurves),
        };

        private static void PlanarPerpendicularToCasterLine()
        {
            var caster = new Vector2(10.5f, 10.5f);
            var effect = new Vector2(13.5f, 14.5f);
            PlanarShieldGeometry geo = ShieldGeometry.ResolvePlanar(
                effect, caster, new Vector2(0f, 1f), isSelfCast: false, selfForwardOffset: 1f, spanLength: 6f);

            Vector2 toCaster = caster - effect;
            Vector2 span = geo.SegmentB - geo.SegmentA;
            Check.True(Mathf.Abs(Vector2.Dot(span, toCaster)) < 0.001f, "span perpendicular to effect->caster");
            Check.True(Mathf.Abs(span.magnitude - 6f) < 0.001f, $"span length expected 6, got {span.magnitude}");
            Check.True(Vector2.Dot(geo.Normal, effect - caster) > 0f, "normal faces outward away from caster");
        }

        private static void PlanarSelfCastStepsForward()
        {
            var pos = new Vector2(5.5f, 5.5f);
            PlanarShieldGeometry geo = ShieldGeometry.ResolvePlanar(
                pos, pos, new Vector2(1f, 0f), isSelfCast: true, selfForwardOffset: 1f, spanLength: 4f);

            Check.True(Mathf.Abs(geo.Center.x - 6.5f) < 0.001f && Mathf.Abs(geo.Center.y - 5.5f) < 0.001f,
                $"expected center (6.5, 5.5), got {geo.Center}");
            Check.True(Mathf.Abs(geo.SegmentA.x - 6.5f) < 0.001f && Mathf.Abs(geo.SegmentB.x - 6.5f) < 0.001f,
                "east-facing self shield runs north-south");
        }

        private static void PlanarSegmentIntersection()
        {
            var segA = new Vector2(8f, 10f);
            var segB = new Vector2(12f, 10f);

            bool hitFront = ShieldGeometry.TryIntersectSegment(
                segA, segB, new Vector2(10.5f, 12f), new Vector2(10.5f, 9f), out Vector2 pFront);
            Check.True(hitFront, "crossing step hits planar segment");
            Check.True(Mathf.Abs(pFront.x - 10.5f) < 0.001f && Mathf.Abs(pFront.y - 10f) < 0.001f,
                $"expected hit at (10.5, 10), got {pFront}");

            bool flankMiss = ShieldGeometry.TryIntersectSegment(
                segA, segB, new Vector2(12.6f, 12f), new Vector2(12.6f, 9f), out _);
            Check.True(!flankMiss, "shot past segment endpoint misses");
        }

        private static void DomeIncomingVsOutgoing()
        {
            var center = new Vector2(20f, 20f);
            const float radius = 10f;

            bool incoming = ShieldGeometry.TryIntersectCircleIncoming(
                center, radius, new Vector2(20f, 35f), new Vector2(20f, 25f), out Vector2 rimHit);
            Check.True(incoming, "outside->inside shot intercepted");
            Check.True(Mathf.Abs(rimHit.x - 20f) < 0.001f && Mathf.Abs(rimHit.y - 30f) < 0.01f,
                $"expected north rim (20, 30), got {rimHit}");

            bool outgoing = ShieldGeometry.TryIntersectCircleIncoming(
                center, radius, new Vector2(20f, 25f), new Vector2(20f, 35f), out _);
            Check.True(!outgoing, "shot starting inside dome passes out freely");
        }

        private static void DomePassThroughCrossing()
        {
            var center = new Vector2(20f, 20f);
            const float radius = 10f;

            bool outCross = ShieldGeometry.TryIntersectCircleCrossing(
                center, radius, new Vector2(20f, 25f), new Vector2(20f, 35f), out Vector2 pOut);
            Check.True(outCross, "outgoing friendly shot crosses rim");
            Check.True(Mathf.Abs(pOut.x - 20f) < 0.001f && Mathf.Abs(pOut.y - 30f) < 0.01f,
                $"expected outgoing rim crossing (20, 30), got {pOut}");

            bool interiorOnly = ShieldGeometry.TryIntersectCircleCrossing(
                center, radius, new Vector2(20f, 22f), new Vector2(20f, 26f), out _);
            Check.True(!interiorOnly, "purely interior step does not cross rim");
        }

        private static void PlanarGlyphCompileCurves()
        {
            var def = new PlanarShieldGlyphDef
            {
                defName = "T_Effect_PlanarShield",
                kind = SpellGlyphKind.Effect,
                baseCharges = 2,
                maxCharges = 10,
                lifetimeTicks = 120,
                radius = 1f,
                maxLength = 8f,
                selfForwardOffset = 0.5f,
                vfxDef = "T_Vfx_PlanarShield",
                compatibleAugments = new List<string> { SpellDefs.AugmentAmplify, SpellDefs.AugmentAoe }
            };

            var amp = new SpellGlyphDef { defName = SpellDefs.AugmentAmplify, kind = SpellGlyphKind.Augment, factor = 2f };
            var aoe = new SpellGlyphDef { defName = SpellDefs.AugmentAoe, kind = SpellGlyphKind.Augment, radius = 1.5f };

            var baseStep = (PlanarShieldEffect)def.Compile(Array.Empty<SpellGlyphDef>(), null);
            Check.Equal(2, baseStep.Charges, "base charges");
            Check.Equal(2f, baseStep.SpanLength, "base span length");

            var midStep = (PlanarShieldEffect)def.Compile(new[] { amp, amp, aoe }, null);
            Check.Equal(8, midStep.Charges, "2x Amplify (x2) -> 8");
            Check.Equal(5f, midStep.SpanLength, "1x AOE (+1.5 half-length) -> 5");

            var cappedStep = (PlanarShieldEffect)def.Compile(new[] { amp, amp, amp, aoe, aoe, aoe }, null);
            Check.Equal(10, cappedStep.Charges, "charges capped at maxCharges");
            Check.Equal(8f, cappedStep.SpanLength, "span capped at maxLength");
        }
    }
}
