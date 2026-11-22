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

        /// <summary>
        /// Appends a shaded 4-corner quad with uniform cross-spine weight (<c>u = 0.5</c>) so translucent
        /// 3D shield curtain slices and shatter shards interpolate smooth vertex colors in a single batch.
        /// </summary>
        public void AddShadedQuad(
            Vector3 p00,
            Vector3 p10,
            Vector3 p11,
            Vector3 p01,
            Color bottomColor,
            Color topColor,
            float hdrIntensity)
        {
            if (bottomColor.a <= 0.0005f && topColor.a <= 0.0005f)
            {
                return;
            }

            int baseIdx = vertices.Count;
            Color bot = AdaptColor(bottomColor);
            Color top = AdaptColor(topColor);
            var uvBot = new Vector4(0.5f, 0f, hdrIntensity, 1f);
            var uvTop = new Vector4(0.5f, 1f, hdrIntensity, 1f);

            vertices.Add(p00); colors.Add(bot); uvs.Add(uvBot);
            vertices.Add(p10); colors.Add(bot); uvs.Add(uvBot);
            vertices.Add(p11); colors.Add(top); uvs.Add(uvTop);
            vertices.Add(p01); colors.Add(top); uvs.Add(uvTop);

            indices.Add(baseIdx);
            indices.Add(baseIdx + 1);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx + 3);
        }

        public void AddShadedTriangle(
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            Color c0,
            Color c1,
            Color c2,
            float hdrIntensity)
        {
            if (c0.a <= 0.0005f && c1.a <= 0.0005f && c2.a <= 0.0005f)
            {
                return;
            }

            int baseIdx = vertices.Count;
            var uv = new Vector4(0.5f, 0.5f, hdrIntensity, 1f);
            vertices.Add(p0); colors.Add(AdaptColor(c0)); uvs.Add(uv);
            vertices.Add(p1); colors.Add(AdaptColor(c1)); uvs.Add(uv);
            vertices.Add(p2); colors.Add(AdaptColor(c2)); uvs.Add(uv);

            indices.Add(baseIdx);
            indices.Add(baseIdx + 1);
            indices.Add(baseIdx + 2);
        }

        /// <summary>
        /// Appends a surface-clamped wave-band quad with explicit cross-band profile coordinates (<c>u in [0..1]</c>,
        /// <c>0.5</c> = wave crest) so analytically boundary-clipped shield ripples preserve their exact profile
        /// right up to the shield edge with zero out-of-bounds geometry.
        /// </summary>
        public void AddWaveBandQuad(
            Vector3 inner0,
            Vector3 outer0,
            Vector3 outer1,
            Vector3 inner1,
            float uIn0,
            float uOut0,
            float uOut1,
            float uIn1,
            Color color,
            float hdrIntensity,
            float edgePower)
        {
            if (color.a <= 0.0005f)
            {
                return;
            }

            int baseIdx = vertices.Count;
            Color col = AdaptColor(color);
            vertices.Add(inner0); colors.Add(col); uvs.Add(new Vector4(uIn0, 0f, hdrIntensity, edgePower));
            vertices.Add(outer0); colors.Add(col); uvs.Add(new Vector4(uOut0, 0f, hdrIntensity, edgePower));
            vertices.Add(outer1); colors.Add(col); uvs.Add(new Vector4(uOut1, 1f, hdrIntensity, edgePower));
            vertices.Add(inner1); colors.Add(col); uvs.Add(new Vector4(uIn1, 1f, hdrIntensity, edgePower));

            indices.Add(baseIdx);
            indices.Add(baseIdx + 1);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx + 3);
        }

        /// <summary>
        /// Appends one radial sector of a boundary-clipped standing glow disc (<c>frac0, frac1 in [0..1]</c> are
        /// normalized radii <c>r / glowRadius</c>), keeping 100% of the triangle inside the shield silhouette.
        /// </summary>
        public void AddClippedGlowSector(
            Vector3 center,
            Vector3 rim0,
            Vector3 rim1,
            float frac0,
            float frac1,
            Color innerColor,
            Color outerColor,
            float hdrIntensity,
            float edgePower)
        {
            if (innerColor.a <= 0.0005f)
            {
                return;
            }

            int baseIdx = vertices.Count;
            Color cCenter = AdaptColor(innerColor);
            Color cRim0 = AdaptColor(Color.Lerp(innerColor, outerColor, frac0));
            Color cRim1 = AdaptColor(Color.Lerp(innerColor, outerColor, frac1));

            vertices.Add(center); colors.Add(cCenter); uvs.Add(new Vector4(0.5f, 0.5f, hdrIntensity, edgePower));
            vertices.Add(rim0); colors.Add(cRim0); uvs.Add(new Vector4(0.5f * (1f - frac0), 0f, hdrIntensity, edgePower));
            vertices.Add(rim1); colors.Add(cRim1); uvs.Add(new Vector4(0.5f * (1f - frac1), 1f, hdrIntensity, edgePower));

            indices.Add(baseIdx);
            indices.Add(baseIdx + 1);
            indices.Add(baseIdx + 2);
        }

        /// <summary>
        /// Builds the flower-petal-clipped additive glow fan inside one hanging glass globe.
        /// The upper perimeter arches strictly underneath the white petal cap (<c>+capSideLiftFactor*ry</c> at
        /// sides dipping to <c>-capDipFactor*ry</c> at center), while the lower rim follows the glass ellipse.
        /// </summary>
        public void AddLampGlobeCavityGlow(
            Vector3 center,
            float rx,
            float ry,
            float capDipFactor,
            float capSideLiftFactor,
            float bottomScale,
            int arcSegments,
            Color coreColor,
            Color edgeColor,
            float hdrIntensity,
            float edgePower)
        {
            if (rx <= 0.001f || ry <= 0.001f || arcSegments < 6 || coreColor.a <= 0.0005f)
            {
                return;
            }

            Color coreAdapted = AdaptColor(coreColor);
            Color edgeAdapted = AdaptColor(edgeColor);

            int centerIdx = vertices.Count;
            vertices.Add(new Vector3(center.x, center.y, center.z - 0.10f * ry));
            colors.Add(coreAdapted);
            uvs.Add(new Vector4(0.5f, 0.5f, hdrIntensity, edgePower));

            int ringStartIdx = vertices.Count;
            int topSegments = Math.Max(4, arcSegments / 2);
            float ySide = capSideLiftFactor * ry;
            float yMid = -capDipFactor * ry;
            float yCtrl = 2f * yMid - ySide;

            // 1. Top scalloped cap boundary left (-0.96*rx) -> right (+0.96*rx)
            for (int i = 0; i <= topSegments; i++)
            {
                float t = i / (float)topSegments;
                float u = 1f - t;
                float px = Mathf.Lerp(-0.96f * rx, 0.96f * rx, t);
                float pz = u * u * ySide + 2f * u * t * yCtrl + t * t * ySide;
                vertices.Add(new Vector3(center.x + px, center.y, center.z + pz));
                colors.Add(edgeAdapted);
                uvs.Add(new Vector4(0f, t * 0.5f, hdrIntensity, edgePower));
            }

            // 2. Lower glass globe rim right (0.05*pi) -> left (0.95*pi)
            for (int i = 0; i <= arcSegments; i++)
            {
                float t = i / (float)arcSegments;
                float ang = Mathf.Lerp(0.05f * Mathf.PI, 0.95f * Mathf.PI, t);
                float px = Mathf.Cos(ang) * (rx * 0.98f);
                float pz = -0.04f * ry - Mathf.Sin(ang) * (ry * bottomScale);
                vertices.Add(new Vector3(center.x + px, center.y, center.z + pz));
                colors.Add(edgeAdapted);
                uvs.Add(new Vector4(0f, 0.5f + t * 0.5f, hdrIntensity, edgePower));
            }

            int ringCount = vertices.Count - ringStartIdx;
            for (int i = 0; i < ringCount; i++)
            {
                int next = (i + 1) % ringCount;
                indices.Add(centerIdx);
                indices.Add(ringStartIdx + i);
                indices.Add(ringStartIdx + next);
            }
        }

        public void AddSolidPolygonFan(IReadOnlyList<Vector3> polygon, Color color, float hdrIntensity)
        {
            if (polygon == null || polygon.Count < 3 || color.a <= 0.0005f)
            {
                return;
            }

            int baseIdx = vertices.Count;
            Color col = AdaptColor(color);
            var uv = new Vector4(0.5f, 0.5f, hdrIntensity, 1f);
            for (int i = 0; i < polygon.Count; i++)
            {
                vertices.Add(polygon[i]);
                colors.Add(col);
                uvs.Add(uv);
            }

            for (int i = 1; i < polygon.Count - 1; i++)
            {
                indices.Add(baseIdx);
                indices.Add(baseIdx + i);
                indices.Add(baseIdx + i + 1);
            }
        }

        public void AddSolidLine(Vector3 a, Vector3 b, float width, Color color, float hdrIntensity)
        {
            if (width <= 0f || color.a <= 0.0005f)
            {
                return;
            }

            float dx = b.x - a.x;
            float dz = b.z - a.z;
            float len = Mathf.Sqrt(dx * dx + dz * dz);
            if (len < 1e-5f)
            {
                return;
            }

            float nx = -dz / len;
            float nz = dx / len;
            float hw = width * 0.5f;

            int baseIdx = vertices.Count;
            Color col = AdaptColor(color);
            var uv = new Vector4(0.5f, 0.5f, hdrIntensity, 1f);

            vertices.Add(new Vector3(a.x - nx * hw, a.y, a.z - nz * hw)); colors.Add(col); uvs.Add(uv);
            vertices.Add(new Vector3(a.x + nx * hw, a.y, a.z + nz * hw)); colors.Add(col); uvs.Add(uv);
            vertices.Add(new Vector3(b.x + nx * hw, b.y, b.z + nz * hw)); colors.Add(col); uvs.Add(uv);
            vertices.Add(new Vector3(b.x - nx * hw, b.y, b.z - nz * hw)); colors.Add(col); uvs.Add(uv);

            indices.Add(baseIdx);
            indices.Add(baseIdx + 1);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx + 3);
        }

        /// <summary>
        /// Appends a surface-clamped refraction wave quad for Layer 14 (<c>WotF_Shader_LumenDistortion</c>).
        /// <c>UV0 = (uCross, weight, sampleOffsetWorldX, sampleOffsetWorldZ)</c>.
        /// </summary>
        public void AddDistortionBandQuad(
            Vector3 inner0,
            Vector3 outer0,
            Vector3 outer1,
            Vector3 inner1,
            float uIn0,
            float uOut0,
            float uOut1,
            float uIn1,
            Vector2 sampleOffsetXZ0,
            Vector2 sampleOffsetXZ1,
            float weight0,
            float weight1)
        {
            if ((weight0 <= 0.0005f && weight1 <= 0.0005f)
                || (sampleOffsetXZ0.sqrMagnitude <= 1e-9f && sampleOffsetXZ1.sqrMagnitude <= 1e-9f))
            {
                return;
            }

            int baseIdx = vertices.Count;
            vertices.Add(inner0); colors.Add(Color.white); uvs.Add(new Vector4(uIn0, weight0, sampleOffsetXZ0.x, sampleOffsetXZ0.y));
            vertices.Add(outer0); colors.Add(Color.white); uvs.Add(new Vector4(uOut0, weight0, sampleOffsetXZ0.x, sampleOffsetXZ0.y));
            vertices.Add(outer1); colors.Add(Color.white); uvs.Add(new Vector4(uOut1, weight1, sampleOffsetXZ1.x, sampleOffsetXZ1.y));
            vertices.Add(inner1); colors.Add(Color.white); uvs.Add(new Vector4(uIn1, weight1, sampleOffsetXZ1.x, sampleOffsetXZ1.y));

            indices.Add(baseIdx);
            indices.Add(baseIdx + 1);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx + 3);
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

        /// <summary>
        /// Evaluates the 3D funnel radius and clockwise corkscrew spine offset at normalized height <paramref name="v"/>.
        /// </summary>
        public static void EvaluateCordiumFunnelSlice(
            float v,
            float baseRadius,
            float waistRadius,
            float topRadius,
            float heightCells,
            float swayAmplitude,
            float elapsedSeconds,
            out float rAtV,
            out float swayX,
            out float swayZ,
            out float heightY)
        {
            if (v < 0.22f)
            {
                float u = v / 0.22f;
                float smooth = u * u * (3f - 2f * u);
                rAtV = Mathf.Lerp(baseRadius, waistRadius, smooth);
            }
            else
            {
                float u = (v - 0.22f) / 0.78f;
                rAtV = Mathf.Lerp(waistRadius, topRadius, Mathf.Pow(u, 1.28f));
            }

            float coilPhase = v * 4.2f - elapsedSeconds * 4.4f;
            float swayEnv = swayAmplitude * Mathf.Pow(v, 1.15f) * (1f - 0.20f * v);
            swayX = swayEnv * Mathf.Cos(coilPhase);
            swayZ = swayEnv * Mathf.Sin(coilPhase);
            heightY = heightCells * v;
        }

        /// <summary>
        /// Builds the true 3D stacked-ring Cordium fire tornado funnel plus 3D tangential whipping flame tongues
        /// projected via RimLumen's exact 60-degree orthographic Vertical3D basis (<c>Ortho60AspectZ</c> and
        /// <c>Ortho60HeightZ</c>) in strict back-to-front painter's order for <c>WotF_Shader_LumenTornado</c>.
        /// </summary>
        public void AddCordiumTornadoFunnel3D(
            Vector3 basePos,
            float baseRadius,
            float waistRadius,
            float topRadius,
            float heightCells,
            float swayAmplitude,
            int heightSteps,
            int crossSteps,
            int flameTongueCount,
            float elapsedSeconds,
            float collapseDissolve,
            float masterAlpha,
            float hdrIntensity)
        {
            if (heightSteps < 4 || crossSteps < 4 || masterAlpha <= 0.002f)
            {
                return;
            }

            float linearFlag = linearBlendSpace ? 1f : 0f;
            int halfSteps = Mathf.Max(4, crossSteps / 2);

            // 1. North / back inner wall (theta in [0, pi], sin(theta) >= 0) — reveals hollow 3D vortex throat
            AddTornadoHalfShell(
                basePos, baseRadius, waistRadius, topRadius, heightCells, swayAmplitude,
                heightSteps, halfSteps, elapsedSeconds, collapseDissolve, masterAlpha * 0.94f,
                hdrIntensity, linearFlag, isFrontHalf: false);

            // 2. Back-half 3D whipping flame tongues occluded behind the core/front wall
            if (flameTongueCount > 0)
            {
                AddCordiumFlameTongues3D(
                    basePos, baseRadius, waistRadius, topRadius, heightCells, swayAmplitude,
                    flameTongueCount, elapsedSeconds, collapseDissolve, masterAlpha,
                    hdrIntensity, linearFlag, frontHalfPass: false);
            }

            // 3. Dense molten inner core spine below v=0.76 (hides particle spawn interior while keeping top mouth hollow)
            AddTornadoInnerCore(
                basePos, baseRadius, waistRadius, topRadius, heightCells, swayAmplitude,
                Mathf.Max(6, (int)(heightSteps * 0.72f)), Mathf.Max(4, halfSteps / 2),
                elapsedSeconds, collapseDissolve, masterAlpha * 0.88f, hdrIntensity * 0.78f, linearFlag);

            // 4. South / front outer shell (theta in [pi, 2*pi], sin(theta) <= 0)
            AddTornadoHalfShell(
                basePos, baseRadius, waistRadius, topRadius, heightCells, swayAmplitude,
                heightSteps, halfSteps, elapsedSeconds, collapseDissolve, masterAlpha,
                hdrIntensity, linearFlag, isFrontHalf: true);

            // 5. Front-half 3D whipping flame tongues peeling clockwise off the horizontal rings
            if (flameTongueCount > 0)
            {
                AddCordiumFlameTongues3D(
                    basePos, baseRadius, waistRadius, topRadius, heightCells, swayAmplitude,
                    flameTongueCount, elapsedSeconds, collapseDissolve, masterAlpha,
                    hdrIntensity, linearFlag, frontHalfPass: true);
            }
        }

        private void AddTornadoHalfShell(
            Vector3 basePos,
            float baseRadius,
            float waistRadius,
            float topRadius,
            float heightCells,
            float swayAmplitude,
            int heightSteps,
            int halfSteps,
            float elapsedSeconds,
            float collapseDissolve,
            float alpha,
            float hdrIntensity,
            float linearFlag,
            bool isFrontHalf)
        {
            int baseIdx = vertices.Count;
            int cols = halfSteps + 1;
            float thetaStart = isFrontHalf ? Mathf.PI : 0f;
            float signedHdr = isFrontHalf ? hdrIntensity : -hdrIntensity;

            for (int iy = 0; iy <= heightSteps; iy++)
            {
                float v = iy / (float)heightSteps;
                EvaluateCordiumFunnelSlice(
                    v, baseRadius, waistRadius, topRadius, heightCells, swayAmplitude, elapsedSeconds,
                    out float rAtV, out float swayX, out float swayZ, out float heightY);

                for (int ix = 0; ix <= halfSteps; ix++)
                {
                    float frac = ix / (float)halfSteps;
                    float theta = thetaStart + Mathf.PI * frac;
                    float sinT = Mathf.Sin(theta);
                    float cosT = Mathf.Cos(theta);

                    // Soft limb feathering at theta = 0, pi so silhouette edges never look hard-cut
                    float limb = Mathf.Pow(Mathf.Abs(sinT), 0.52f);
                    float uAz = theta / (Mathf.PI * 2f);

                    float lx = swayX + cosT * rAtV;
                    float lz = swayZ + sinT * rAtV;

                    float wx = basePos.x + lx;
                    float wz = basePos.z + lz * Ortho60AspectZ + heightY * Ortho60HeightZ;

                    vertices.Add(new Vector3(wx, basePos.y, wz));
                    colors.Add(new Color(limb, linearFlag, collapseDissolve, alpha));
                    uvs.Add(new Vector4(uAz, v, elapsedSeconds, signedHdr));
                }
            }

            AppendGridIndices(baseIdx, heightSteps, halfSteps, cols);
        }

        private void AddTornadoInnerCore(
            Vector3 basePos,
            float baseRadius,
            float waistRadius,
            float topRadius,
            float heightCells,
            float swayAmplitude,
            int coreHeightSteps,
            int coreCrossSteps,
            float elapsedSeconds,
            float collapseDissolve,
            float alpha,
            float hdrIntensity,
            float linearFlag)
        {
            int baseIdx = vertices.Count;
            int cols = coreCrossSteps + 1;
            const float maxCoreV = 0.74f;

            for (int iy = 0; iy <= coreHeightSteps; iy++)
            {
                float uV = iy / (float)coreHeightSteps;
                float v = uV * maxCoreV;
                float topFade = uV > 0.78f ? 1f - (uV - 0.78f) / 0.22f : 1f;
                EvaluateCordiumFunnelSlice(
                    v, baseRadius, waistRadius, topRadius, heightCells, swayAmplitude, elapsedSeconds,
                    out float rAtV, out float swayX, out float swayZ, out float heightY);

                float coreR = rAtV * 0.56f;
                for (int ix = 0; ix <= coreCrossSteps; ix++)
                {
                    float s = (ix / (float)coreCrossSteps) * 2f - 1f;
                    float edge = Mathf.Sqrt(Mathf.Max(0f, 1f - s * s));
                    float uCyl = 0.5f + Mathf.Asin(Mathf.Clamp(s, -0.985f, 0.985f)) / Mathf.PI * 0.5f;

                    float lx = swayX + s * coreR;
                    float lz = swayZ - edge * coreR * 0.15f;

                    vertices.Add(new Vector3(basePos.x + lx, basePos.y, basePos.z + lz * Ortho60AspectZ + heightY * Ortho60HeightZ));
                    colors.Add(new Color(edge, linearFlag, collapseDissolve, alpha * topFade));
                    uvs.Add(new Vector4(uCyl, v, elapsedSeconds, -hdrIntensity));
                }
            }

            AppendGridIndices(baseIdx, coreHeightSteps, coreCrossSteps, cols);
        }

        private void AddCordiumFlameTongues3D(
            Vector3 basePos,
            float baseRadius,
            float waistRadius,
            float topRadius,
            float heightCells,
            float swayAmplitude,
            int tongueCount,
            float elapsedSeconds,
            float collapseDissolve,
            float masterAlpha,
            float hdrIntensity,
            float linearFlag,
            bool frontHalfPass)
        {
            const int tongueSteps = 7;
            const float golden = 0.61803399f;

            for (int k = 0; k < tongueCount; k++)
            {
                float cyclePhase = elapsedSeconds * 1.38f + k * golden;
                float cycleIdx = Mathf.Floor(cyclePhase);
                float uLife = cyclePhase - cycleIdx;
                float lifeAlpha = Mathf.Sin(uLife * Mathf.PI);
                if (lifeAlpha <= 0.03f)
                {
                    continue;
                }

                float hHash = Frac01(k * 0.3719f + cycleIdx * 0.2931f);
                float aHash = Frac01(k * 0.7137f + cycleIdx * 0.5173f);
                float v0 = Mathf.Lerp(0.16f, 0.90f, hHash);
                float theta0 = aHash * (Mathf.PI * 2f) - elapsedSeconds * (11.2f - 3.6f * v0);
                float sweepAngle = 1.08f + 0.32f * uLife;

                for (int s = 0; s < tongueSteps; s++)
                {
                    float f0 = s / (float)tongueSteps;
                    float f1 = (s + 1) / (float)tongueSteps;
                    float fMid = (f0 + f1) * 0.5f;
                    float thetaMid = theta0 - fMid * sweepAngle;
                    bool isFrontSeg = Mathf.Sin(thetaMid) <= 0f;
                    if (isFrontSeg != frontHalfPass)
                    {
                        continue;
                    }

                    EvaluateTonguePoint(
                        basePos, baseRadius, waistRadius, topRadius, heightCells, swayAmplitude,
                        elapsedSeconds, v0, theta0, sweepAngle, uLife, f0,
                        out Vector3 p0, out float w0, out float uAz0);
                    EvaluateTonguePoint(
                        basePos, baseRadius, waistRadius, topRadius, heightCells, swayAmplitude,
                        elapsedSeconds, v0, theta0, sweepAngle, uLife, f1,
                        out Vector3 p1, out float w1, out float uAz1);

                    float dx = p1.x - p0.x;
                    float dz = p1.z - p0.z;
                    float len = Mathf.Sqrt(dx * dx + dz * dz);
                    if (len < 0.0001f)
                    {
                        continue;
                    }

                    float nx = -dz / len;
                    float nz = dx / len;
                    float a0 = masterAlpha * lifeAlpha * (1f - f0 * 0.55f);
                    float a1 = masterAlpha * lifeAlpha * (1f - f1 * 0.78f);
                    float hdr = (frontHalfPass ? hdrIntensity : -hdrIntensity * 0.85f) * 1.28f;
                    // Keep shader vUv.y in bright mid-flame band so topCutoff never clips upper tongues
                    float vTex0 = Mathf.Lerp(0.24f, 0.56f, f0);
                    float vTex1 = Mathf.Lerp(0.24f, 0.56f, f1);

                    int segIdx = vertices.Count;
                    vertices.Add(new Vector3(p0.x - nx * w0, p0.y, p0.z - nz * w0));
                    colors.Add(new Color(0.75f, linearFlag, collapseDissolve, a0));
                    uvs.Add(new Vector4(uAz0, vTex0, elapsedSeconds, hdr));

                    vertices.Add(new Vector3(p0.x + nx * w0, p0.y, p0.z + nz * w0));
                    colors.Add(new Color(0.75f, linearFlag, collapseDissolve, a0));
                    uvs.Add(new Vector4(uAz0, vTex0, elapsedSeconds, hdr));

                    vertices.Add(new Vector3(p1.x - nx * w1, p1.y, p1.z - nz * w1));
                    colors.Add(new Color(0.55f, linearFlag, collapseDissolve, a1));
                    uvs.Add(new Vector4(uAz1, vTex1, elapsedSeconds, hdr));

                    vertices.Add(new Vector3(p1.x + nx * w1, p1.y, p1.z + nz * w1));
                    colors.Add(new Color(0.55f, linearFlag, collapseDissolve, a1));
                    uvs.Add(new Vector4(uAz1, vTex1, elapsedSeconds, hdr));

                    AppendQuadIndices(segIdx);
                }
            }
        }

        private static void EvaluateTonguePoint(
            Vector3 basePos,
            float baseRadius,
            float waistRadius,
            float topRadius,
            float heightCells,
            float swayAmplitude,
            float elapsedSeconds,
            float v0,
            float theta0,
            float sweepAngle,
            float uLife,
            float frac,
            out Vector3 screenWorldPos,
            out float halfWidth,
            out float uAz)
        {
            float v = Mathf.Clamp01(v0 + frac * 0.035f);
            EvaluateCordiumFunnelSlice(
                v, baseRadius, waistRadius, topRadius, heightCells, swayAmplitude, elapsedSeconds,
                out float rAtV, out float swayX, out float swayZ, out float heightY);

            float peelMult = Mathf.Lerp(0.95f, 1.38f + 0.20f * uLife, frac * frac);
            float r = rAtV * peelMult;
            float theta = theta0 - frac * sweepAngle;

            float lx = swayX + Mathf.Cos(theta) * r;
            float lz = swayZ + Mathf.Sin(theta) * r;
            float ly = heightY + frac * heightCells * 0.04f;

            screenWorldPos = new Vector3(
                basePos.x + lx,
                basePos.y + (theta < Mathf.PI ? -0.001f : 0.001f),
                basePos.z + lz * Ortho60AspectZ + ly * Ortho60HeightZ);

            float baseWidth = Mathf.Lerp(0.08f, 0.16f, v0);
            halfWidth = baseWidth * (1f - 0.85f * frac);
            uAz = Frac01(theta / (Mathf.PI * 2f));
        }

        private static float Frac01(float x) => x - Mathf.Floor(x);

        private void AppendGridIndices(int baseIdx, int ySteps, int xSteps, int cols)
        {
            for (int iy = 0; iy < ySteps; iy++)
            {
                for (int ix = 0; ix < xSteps; ix++)
                {
                    int i00 = baseIdx + iy * cols + ix;
                    int i10 = i00 + 1;
                    int i01 = i00 + cols;
                    int i11 = i01 + 1;

                    indices.Add(i00);
                    indices.Add(i10);
                    indices.Add(i11);
                    indices.Add(i00);
                    indices.Add(i11);
                    indices.Add(i01);
                }
            }
        }

        /// <summary>
        /// Builds the swirling ground magma collar disc at the foot of the Cordium tornado for
        /// <c>WotF_Shader_LumenTornado</c>, wrapping periodic azimuthal UV continuously around 360°.
        /// </summary>
        public void AddCordiumGroundEye(
            Vector3 basePos,
            float outerRadius,
            int radialSteps,
            int angularSegments,
            float elapsedSeconds,
            float collapseDissolve,
            float masterAlpha,
            float hdrIntensity)
        {
            if (outerRadius <= 0.05f || radialSteps < 2 || angularSegments < 8 || masterAlpha <= 0.002f)
            {
                return;
            }

            int baseIdx = vertices.Count;
            float linearFlag = linearBlendSpace ? 1f : 0f;
            int cols = angularSegments + 1;
            float innerRadius = outerRadius * 0.04f;

            for (int ir = 0; ir <= radialSteps; ir++)
            {
                float rNorm = ir / (float)radialSteps;
                float r = Mathf.Lerp(innerRadius, outerRadius, rNorm);
                float edge = Mathf.Pow(Mathf.Max(0f, 1f - rNorm), 0.72f);
                float vCoord = Mathf.Lerp(0.08f, 0.45f, rNorm);

                for (int ia = 0; ia <= angularSegments; ia++)
                {
                    float uAz = ia / (float)angularSegments;
                    float angle = uAz * Mathf.PI * 2f;
                    float wx = basePos.x + Mathf.Cos(angle) * r;
                    float wz = basePos.z + Mathf.Sin(angle) * r * Ortho60AspectZ;

                    vertices.Add(new Vector3(wx, basePos.y - 0.01f, wz));
                    colors.Add(new Color(edge, linearFlag, collapseDissolve, masterAlpha));
                    uvs.Add(new Vector4(uAz, vCoord, elapsedSeconds, hdrIntensity));
                }
            }

            for (int ir = 0; ir < radialSteps; ir++)
            {
                for (int ia = 0; ia < angularSegments; ia++)
                {
                    int i00 = baseIdx + ir * cols + ia;
                    int i10 = i00 + 1;
                    int i01 = i00 + cols;
                    int i11 = i01 + 1;

                    indices.Add(i00);
                    indices.Add(i10);
                    indices.Add(i11);
                    indices.Add(i00);
                    indices.Add(i11);
                    indices.Add(i01);
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
