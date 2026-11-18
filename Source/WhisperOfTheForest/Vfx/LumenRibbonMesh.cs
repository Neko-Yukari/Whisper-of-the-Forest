using System;
using System.Collections.Generic;
using RimLumen.Core.Contracts;
using RimLumen.Core.Rendering;
using UnityEngine;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>
    /// Reusable dynamic World-Mesh builder for Layer 15 RimLumen ribbons, 3D funnel spirals, lightning bolts,
    /// elliptical shock rings, radial halos and camera-covering darken quads.
    ///
    /// Vertex channel contract matches <c>WotF_LumenRibbon.vert/frag.glsl</c>:
    ///   - Color: normalized stored/linear RGB tint + alpha [0..1]
    ///   - UV0 (Vector4): x = cross-ribbon u [0..1] (0.5 = spine peak), y = along-ribbon v [0..1],
    ///                    z = unclamped HDR intensity multiplier, w = edge softness power.
    /// </summary>
    public sealed class LumenRibbonMesh : IDisposable
    {
        // Matches RimLumen CameraBillboardBasis 60-degree orthographic Vertical3D pitch (sin 60° / cos 60°).
        public const float Ortho60AspectZ = 0.8660254f;
        public const float Ortho60HeightZ = 0.5f;

        private readonly List<Vector3> vertices = new List<Vector3>(1024);
        private readonly List<Color> colors = new List<Color>(1024);
        private readonly List<Vector4> uvs = new List<Vector4>(1024);
        private readonly List<int> indices = new List<int>(2048);

        private Mesh mesh;
        private bool linearBlendSpace;

        public LumenRibbonMesh(string name)
        {
            mesh = new Mesh
            {
                name = name
            };
            mesh.MarkDynamic();
        }

        public bool HasGeometry => indices.Count > 0;

        public Mesh Mesh => mesh;

        public void Clear()
        {
            vertices.Clear();
            colors.Clear();
            uvs.Clear();
            indices.Clear();
            linearBlendSpace = RimLumenRenderHost.CurrentFrameColorBlendSpace == LumenColorBlendSpace.Linear;
        }

        public void Upload()
        {
            if (mesh == null)
            {
                return;
            }

            mesh.Clear(false);
            if (vertices.Count == 0 || indices.Count == 0)
            {
                return;
            }

            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0, true);
        }

        public void Dispose()
        {
            Clear();
            if (mesh != null)
            {
                UnityEngine.Object.Destroy(mesh);
                mesh = null;
            }
        }

        public void AddRadialGlowDisc(
            Vector3 centerXZ,
            float radius,
            int segments,
            Color innerColor,
            Color outerColor,
            float hdrIntensity,
            float edgePower,
            Func<float, float, float> altitudeAt)
        {
            if (radius <= 0.001f || segments < 6 || innerColor.a <= 0.0005f)
            {
                return;
            }

            Color innerAdapted = AdaptColor(innerColor);
            Color outerAdapted = AdaptColor(outerColor);
            int centerIdx = vertices.Count;
            float cy = altitudeAt != null ? altitudeAt(centerXZ.x, centerXZ.z) : centerXZ.y;
            vertices.Add(new Vector3(centerXZ.x, cy, centerXZ.z));
            colors.Add(innerAdapted);
            uvs.Add(new Vector4(0.5f, 0.5f, hdrIntensity, edgePower));

            for (int i = 0; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float x = centerXZ.x + Mathf.Cos(angle) * radius;
                float z = centerXZ.z + Mathf.Sin(angle) * radius;
                float y = altitudeAt != null ? altitudeAt(x, z) : centerXZ.y;
                vertices.Add(new Vector3(x, y, z));
                colors.Add(outerAdapted);
                uvs.Add(new Vector4(0f, i / (float)segments, hdrIntensity, edgePower));
            }

            for (int i = 0; i < segments; i++)
            {
                indices.Add(centerIdx);
                indices.Add(centerIdx + 1 + i);
                indices.Add(centerIdx + 2 + i);
            }
        }

        public void AddTaperedSegment(
            Vector3 startXZ,
            Vector3 endXZ,
            float startWidth,
            float endWidth,
            Color startColor,
            Color endColor,
            float hdrIntensity,
            float edgePower,
            Func<float, float, float> altitudeAt)
        {
            float dx = endXZ.x - startXZ.x;
            float dz = endXZ.z - startXZ.z;
            float len = Mathf.Sqrt(dx * dx + dz * dz);
            if (len <= 0.0001f)
            {
                return;
            }

            float nx = -dz / len;
            float nz = dx / len;
            float hs = startWidth * 0.5f;
            float he = endWidth * 0.5f;

            int baseIdx = vertices.Count;
            AddRibbonPair(startXZ.x, startXZ.z, nx, nz, hs, startColor, 0f, hdrIntensity, edgePower, startXZ.y, altitudeAt);
            AddRibbonPair(endXZ.x, endXZ.z, nx, nz, he, endColor, 1f, hdrIntensity, edgePower, endXZ.y, altitudeAt);
            AppendQuadIndices(baseIdx);
        }

        public void AddPolyline(
            IReadOnlyList<Vector3> points,
            int count,
            float width,
            Color color,
            float hdrIntensity,
            float edgePower,
            bool taperEnds,
            Func<float, float, float> altitudeAt)
        {
            if (points == null || count < 2 || width <= 0f || color.a <= 0.0005f)
            {
                return;
            }

            int baseIdx = vertices.Count;
            float halfW = width * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Vector3 prev = points[i > 0 ? i - 1 : 0];
                Vector3 next = points[i < count - 1 ? i + 1 : count - 1];
                float tx = next.x - prev.x;
                float tz = next.z - prev.z;
                float tLen = Mathf.Sqrt(tx * tx + tz * tz);
                float nx = tLen > 0.0001f ? -tz / tLen : 0f;
                float nz = tLen > 0.0001f ? tx / tLen : 1f;

                float vAlong = i / (float)(count - 1);
                float endFactor = taperEnds ? Mathf.Max(0f, Mathf.Sin(vAlong * Mathf.PI)) : 1f;
                float hw = halfW * Mathf.Max(0.08f, endFactor);

                AddRibbonPair(points[i].x, points[i].z, nx, nz, hw, color, vAlong, hdrIntensity, edgePower, points[i].y, altitudeAt);
                if (i > 0)
                {
                    AppendQuadIndices(baseIdx + (i - 1) * 2);
                }
            }
        }

        public void AddSpiralArc(
            Vector3 centerXZ,
            float startRadius,
            float endRadius,
            float ellipseAspectZ,
            float startAngleRad,
            float sweepAngleRad,
            int segments,
            float startWidth,
            float endWidth,
            Color startColor,
            Color endColor,
            float hdrIntensity,
            float edgePower,
            Func<float, float, float> altitudeAt)
        {
            if (segments < 2 || (startWidth <= 0f && endWidth <= 0f))
            {
                return;
            }

            int baseIdx = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = startAngleRad + sweepAngleRad * t;
                float radius = Mathf.Lerp(startRadius, endRadius, t);
                float x = centerXZ.x + Mathf.Cos(angle) * radius;
                float z = centerXZ.z + Mathf.Sin(angle) * radius * ellipseAspectZ;

                // Analytical tangent of the spiral in XZ so ribbon width stays clean at sharp curvature.
                float dRadius = (endRadius - startRadius) / segments;
                float dAngle = sweepAngleRad / segments;
                float tx = (dRadius * Mathf.Cos(angle) - radius * dAngle * Mathf.Sin(angle));
                float tz = (dRadius * Mathf.Sin(angle) + radius * dAngle * Mathf.Cos(angle)) * ellipseAspectZ;
                float tLen = Mathf.Sqrt(tx * tx + tz * tz);
                float nx = tLen > 0.0001f ? -tz / tLen : 0f;
                float nz = tLen > 0.0001f ? tx / tLen : 1f;

                float taper = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.55f);
                float hw = Mathf.Lerp(startWidth, endWidth, t) * 0.5f * Mathf.Max(0.1f, taper);
                Color col = Color.Lerp(startColor, endColor, t);

                AddRibbonPair(x, z, nx, nz, hw, col, t, hdrIntensity, edgePower, centerXZ.y, altitudeAt);
                if (i > 0)
                {
                    AppendQuadIndices(baseIdx + (i - 1) * 2);
                }
            }
        }

        public void AddVerticalSpindle(
            Vector3 centerXZ,
            float halfHeightZ,
            float centerWidth,
            int segments,
            Color tipColor,
            Color coreColor,
            float hdrIntensity,
            float edgePower,
            Func<float, float, float> altitudeAt)
        {
            if (halfHeightZ <= 0.005f || centerWidth <= 0.001f || segments < 4)
            {
                return;
            }

            int baseIdx = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float signed = t * 2f - 1f; // -1 (south tip) -> +1 (north tip)
                float z = centerXZ.z + signed * halfHeightZ;
                float profile = Mathf.Pow(Mathf.Max(0f, 1f - signed * signed), 1.35f);
                float hw = centerWidth * 0.5f * profile;
                Color col = Color.Lerp(tipColor, coreColor, profile);

                AddRibbonPair(centerXZ.x, z, 1f, 0f, hw, col, t, hdrIntensity, edgePower, centerXZ.y, altitudeAt);
                if (i > 0)
                {
                    AppendQuadIndices(baseIdx + (i - 1) * 2);
                }
            }
        }

        public void AddTiltedEllipseRing(
            Vector3 center,
            Vector3 majorAxis,
            Vector3 minorAxis,
            float radius,
            float ellipseRatio,
            int segments,
            float thickness,
            Color color,
            float hdrIntensity,
            float edgePower,
            Func<float, float, float> altitudeAt = null)
        {
            if (radius <= 0.001f || thickness <= 0.0005f || segments < 8 || color.a <= 0.0005f)
            {
                return;
            }

            Color colAdapted = AdaptColor(color);
            int baseIdx = vertices.Count;
            float halfT = thickness * 0.5f;

            for (int i = 0; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                Vector3 radialOffset = majorAxis * cos + minorAxis * (sin * ellipseRatio);
                float radialLen = radialOffset.magnitude;
                Vector3 outward = radialLen > 0.0001f ? radialOffset / radialLen : majorAxis;

                Vector3 p = center + radialOffset * radius;
                Vector3 inner = p - outward * halfT;
                Vector3 outer = p + outward * halfT;

                if (altitudeAt != null)
                {
                    inner.y = altitudeAt(inner.x, inner.z);
                    outer.y = altitudeAt(outer.x, outer.z);
                }

                float vAlong = i / (float)segments;
                vertices.Add(inner); colors.Add(colAdapted); uvs.Add(new Vector4(0f, vAlong, hdrIntensity, edgePower));
                vertices.Add(outer); colors.Add(colAdapted); uvs.Add(new Vector4(1f, vAlong, hdrIntensity, edgePower));

                if (i > 0)
                {
                    AppendQuadIndices(baseIdx + (i - 1) * 2);
                }
            }
        }

        /// <summary>
        /// Draws a true 3D helical funnel spiral (radius shrinking while height Y rises) projected using
        /// RimLumen's 60-degree orthographic Vertical3D pitch (<c>dz * 0.8660254 + dy * 0.5</c>).
        /// </summary>
        public void AddFunnelSpiralArc(
            Vector3 anchorXZ,
            float startRadius,
            float endRadius,
            float startHeightY,
            float endHeightY,
            float ellipseAspectZ,
            float startAngleRad,
            float sweepAngleRad,
            int segments,
            float startWidth,
            float endWidth,
            Color startColor,
            Color endColor,
            float hdrIntensity,
            float edgePower,
            Func<float, float, float> altitudeAt)
        {
            if (segments < 2 || (startWidth <= 0f && endWidth <= 0f))
            {
                return;
            }

            int baseIdx = vertices.Count;
            float dRadiusDt = endRadius - startRadius;
            float dHeightDt = endHeightY - startHeightY;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = startAngleRad + sweepAngleRad * t;
                float radius = Mathf.Lerp(startRadius, endRadius, t);
                float heightProfile = Mathf.Pow(t, 1.15f);
                float heightY = Mathf.Lerp(startHeightY, endHeightY, heightProfile);

                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float x = anchorXZ.x + cos * radius;
                float z = anchorXZ.z + sin * radius * ellipseAspectZ + heightY * Ortho60HeightZ;

                float dHeightCur = 1.15f * Mathf.Pow(Mathf.Max(0.001f, t), 0.15f) * dHeightDt;
                float tx = dRadiusDt * cos - radius * sweepAngleRad * sin;
                float tz = (dRadiusDt * sin + radius * sweepAngleRad * cos) * ellipseAspectZ + dHeightCur * Ortho60HeightZ;
                float tLen = Mathf.Sqrt(tx * tx + tz * tz);
                float nx = tLen > 0.0001f ? -tz / tLen : 0f;
                float nz = tLen > 0.0001f ? tx / tLen : 1f;

                float taper = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.42f);
                float hw = Mathf.Lerp(startWidth, endWidth, t) * 0.5f * Mathf.Max(0.14f, taper);
                Color col = Color.Lerp(startColor, endColor, t);

                AddRibbonPair(x, z, nx, nz, hw, col, t, hdrIntensity, edgePower, anchorXZ.y, altitudeAt);
                if (i > 0)
                {
                    AppendQuadIndices(baseIdx + (i - 1) * 2);
                }
            }
        }

        /// <summary>
        /// Draws evenly spaced tapered arc segments around an ellipse so rotation of the base magic ring is
        /// unmistakably legible at a glance.
        /// </summary>
        public void AddSegmentedEllipseArcs(
            Vector3 centerXZ,
            float radius,
            float ellipseAspectZ,
            int arcCount,
            float arcFillFraction,
            float phaseAngleRad,
            int segmentsPerArc,
            float thickness,
            Color color,
            float hdrIntensity,
            float edgePower,
            Func<float, float, float> altitudeAt)
        {
            if (radius <= 0.001f || arcCount < 1 || segmentsPerArc < 3 || thickness <= 0.0005f || color.a <= 0.0005f)
            {
                return;
            }

            float step = Mathf.PI * 2f / arcCount;
            float sweep = step * Mathf.Clamp(arcFillFraction, 0.1f, 0.92f);
            for (int a = 0; a < arcCount; a++)
            {
                float start = phaseAngleRad + a * step;
                int baseIdx = vertices.Count;
                for (int s = 0; s <= segmentsPerArc; s++)
                {
                    float t = s / (float)segmentsPerArc;
                    float angle = start + sweep * t;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);

                    float x = centerXZ.x + cos * radius;
                    float z = centerXZ.z + sin * radius * ellipseAspectZ;
                    float tx = -sin * radius;
                    float tz = cos * radius * ellipseAspectZ;
                    float tLen = Mathf.Sqrt(tx * tx + tz * tz);
                    float nx = tLen > 0.0001f ? -tz / tLen : cos;
                    float nz = tLen > 0.0001f ? tx / tLen : sin;

                    float taper = Mathf.Sin(t * Mathf.PI);
                    float hw = thickness * 0.5f * Mathf.Max(0.18f, taper);
                    AddRibbonPair(x, z, nx, nz, hw, color, t, hdrIntensity, edgePower, centerXZ.y, altitudeAt);
                    if (s > 0)
                    {
                        AppendQuadIndices(baseIdx + (s - 1) * 2);
                    }
                }
            }
        }

        private void AddRibbonPair(
            float cx,
            float cz,
            float nx,
            float nz,
            float halfWidth,
            Color color,
            float vAlong,
            float hdrIntensity,
            float edgePower,
            float fallbackY,
            Func<float, float, float> altitudeAt)
        {
            Color colAdapted = AdaptColor(color);
            float lx = cx - nx * halfWidth;
            float lz = cz - nz * halfWidth;
            float rx = cx + nx * halfWidth;
            float rz = cz + nz * halfWidth;

            float ly = altitudeAt != null ? altitudeAt(lx, lz) : fallbackY;
            float ry = altitudeAt != null ? altitudeAt(rx, rz) : fallbackY;

            vertices.Add(new Vector3(lx, ly, lz));
            colors.Add(colAdapted);
            uvs.Add(new Vector4(0f, vAlong, hdrIntensity, edgePower));

            vertices.Add(new Vector3(rx, ry, rz));
            colors.Add(colAdapted);
            uvs.Add(new Vector4(1f, vAlong, hdrIntensity, edgePower));
        }

        private Color AdaptColor(Color stored)
        {
            if (!linearBlendSpace)
            {
                return stored;
            }

            return new Color(
                DecodeSrgbChannel(stored.r),
                DecodeSrgbChannel(stored.g),
                DecodeSrgbChannel(stored.b),
                stored.a);
        }

        private static float DecodeSrgbChannel(float c)
        {
            if (c <= 0f) return 0f;
            return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        }

        private void AppendQuadIndices(int i0)
        {
            indices.Add(i0);
            indices.Add(i0 + 1);
            indices.Add(i0 + 3);
            indices.Add(i0);
            indices.Add(i0 + 3);
            indices.Add(i0 + 2);
        }
    }
}
