using UnityEngine;

namespace WhisperOfTheForest.Shields
{
    public readonly struct PlanarShieldGeometry
    {
        public readonly Vector2 Center;
        public readonly Vector2 Normal;
        public readonly Vector2 SegmentA;
        public readonly Vector2 SegmentB;

        public PlanarShieldGeometry(Vector2 center, Vector2 normal, Vector2 segmentA, Vector2 segmentB)
        {
            Center = center;
            Normal = normal;
            SegmentA = segmentA;
            SegmentB = segmentB;
        }
    }

    /// <summary>
    /// Stateless 2D XZ geometry and charge/length curves shared by runtime shields and offline seam tests.
    /// </summary>
    public static class ShieldGeometry
    {
        private const float Epsilon = 1e-5f;

        /// <summary>
        /// Computes the planar barrier's center, outward normal and endpoints so the segment is strictly
        /// perpendicular to the line from <paramref name="effectPoint"/> to <paramref name="casterPoint"/>.
        /// When the two coincide (e.g. a Self cast), falls back to <paramref name="fallbackFacing"/> and
        /// optionally steps forward by <paramref name="selfForwardOffset"/>.
        /// </summary>
        public static PlanarShieldGeometry ResolvePlanar(
            Vector2 effectPoint,
            Vector2? casterPoint,
            Vector2 fallbackFacing,
            bool isSelfCast,
            float selfForwardOffset,
            float spanLength)
        {
            Vector2 normal;
            Vector2 center = effectPoint;

            Vector2 awayFromCaster = casterPoint.HasValue ? effectPoint - casterPoint.Value : Vector2.zero;
            if (!isSelfCast && awayFromCaster.sqrMagnitude > 1e-4f)
            {
                normal = awayFromCaster.normalized;
            }
            else
            {
                normal = fallbackFacing.sqrMagnitude > 1e-4f ? fallbackFacing.normalized : new Vector2(0f, 1f);
                if (isSelfCast && selfForwardOffset > 0f)
                {
                    center += normal * selfForwardOffset;
                }
            }

            var tangent = new Vector2(-normal.y, normal.x);
            float half = Mathf.Max(0.05f, spanLength * 0.5f);
            return new PlanarShieldGeometry(center, normal, center - tangent * half, center + tangent * half);
        }

        /// <summary>
        /// Incoming dome boundary test: rejects shots starting inside or on the circle (so interior combat
        /// and outgoing shots pass through) and returns the exact entry point on the rim for incoming chords.
        /// </summary>
        public static bool TryIntersectCircleIncoming(
            Vector2 center,
            float radius,
            Vector2 p0,
            Vector2 p1,
            out Vector2 hitPoint)
        {
            hitPoint = default;
            if (radius <= 0f)
            {
                return false;
            }

            Vector2 d0 = p0 - center;
            float rSq = radius * radius;
            if (d0.sqrMagnitude <= rSq)
            {
                return false;
            }

            Vector2 step = p1 - p0;
            float a = Vector2.Dot(step, step);
            if (a < 1e-10f)
            {
                return false;
            }

            float b = Vector2.Dot(d0, step);
            if (b >= 0f)
            {
                return false;
            }

            float c = d0.sqrMagnitude - rSq;
            float disc = b * b - a * c;
            if (disc < 0f)
            {
                return false;
            }

            float t = (-b - Mathf.Sqrt(disc)) / a;
            if (t < -Epsilon || t > 1f + Epsilon)
            {
                return false;
            }

            hitPoint = p0 + step * Mathf.Clamp01(t);
            return true;
        }

        /// <summary>
        /// Exact 2D segment-segment intersection between shield segment [segA, segB] and projectile step [p0, p1].
        /// </summary>
        public static bool TryIntersectSegment(
            Vector2 segA,
            Vector2 segB,
            Vector2 p0,
            Vector2 p1,
            out Vector2 hitPoint)
        {
            hitPoint = default;

            float minSegX = Mathf.Min(segA.x, segB.x) - Epsilon;
            float maxSegX = Mathf.Max(segA.x, segB.x) + Epsilon;
            float minSegY = Mathf.Min(segA.y, segB.y) - Epsilon;
            float maxSegY = Mathf.Max(segA.y, segB.y) + Epsilon;

            if (Mathf.Max(p0.x, p1.x) < minSegX || Mathf.Min(p0.x, p1.x) > maxSegX
                || Mathf.Max(p0.y, p1.y) < minSegY || Mathf.Min(p0.y, p1.y) > maxSegY)
            {
                return false;
            }

            Vector2 r = segB - segA;
            Vector2 s = p1 - p0;
            float rLenSq = r.sqrMagnitude;
            float sLenSq = s.sqrMagnitude;
            if (rLenSq < 1e-10f || sLenSq < 1e-10f)
            {
                return false;
            }

            float denom = Cross(r, s);
            Vector2 qp = p0 - segA;
            if (Mathf.Abs(denom) < 1e-7f)
            {
                if (Mathf.Abs(Cross(qp, r)) > 1e-4f * Mathf.Sqrt(rLenSq))
                {
                    return false;
                }

                float u0 = Vector2.Dot(qp, r) / rLenSq;
                float u1 = Vector2.Dot(p1 - segA, r) / rLenSq;
                float uMin = Mathf.Min(u0, u1);
                float uMax = Mathf.Max(u0, u1);
                if (uMax < -Epsilon || uMin > 1f + Epsilon)
                {
                    return false;
                }

                float uHit = Mathf.Clamp01(u0);
                hitPoint = segA + r * uHit;
                return true;
            }

            float u = Cross(qp, s) / denom;
            float t = Cross(qp, r) / denom;
            if (u < -Epsilon || u > 1f + Epsilon || t < -Epsilon || t > 1f + Epsilon)
            {
                return false;
            }

            hitPoint = p0 + s * Mathf.Clamp01(t);
            return true;
        }

        public static int ComputeCharges(int baseCharges, float magnitudeFactor, int maxCharges)
        {
            int scaled = Mathf.Max(1, Mathf.RoundToInt(baseCharges * magnitudeFactor));
            return maxCharges > 0 ? Mathf.Min(scaled, maxCharges) : scaled;
        }

        public static float ComputeSpanLength(float baseHalfLength, float extraHalfLength, float maxLength)
        {
            float length = Mathf.Max(0.1f, (baseHalfLength + extraHalfLength) * 2f);
            return maxLength > 0f ? Mathf.Min(length, maxLength) : length;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }
    }
}
