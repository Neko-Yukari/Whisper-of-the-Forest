using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Experimental.Vfx
{
    /// <summary>
    /// Drawing for the railgun worker: the two procedural textures, the shared additive material and
    /// the world-to-interface projection.
    ///
    /// The beam and the darkening tint are both submitted during the GUI pass instead of the world
    /// pass. RimUnity's ScreenEfx runs after the map is composited and before the interface, so a
    /// world-drawn beam would be darkened together with the map. Here the tint is submitted first and
    /// the beam after it in the same pass, so the beam stays the brightest thing on screen.
    /// </summary>
    internal static class RailgunVfxDraw
    {
        /// <summary>Additive, unlit, no depth: a beam over the map that cannot be dimmed by the clip.</summary>
        private const string AdditiveShaderName = "RimUnity_UIAdditive";

        private static RimUnity.Material beamMaterial;
        private static RimUnity.Material orbMaterial;
        private static RimUnity.Texture2D bandTexture;
        private static RimUnity.Texture2D orbTexture;
        private static int materialGameId = int.MinValue;
        private static bool materialsReady;

        /// <summary>Projects a world point to interface pixels (origin top-left). False when behind the camera.</summary>
        public static bool TryProject(Vector3 world, out RimUnity.Vector2 gui)
        {
            gui = default(RimUnity.Vector2);
            Camera camera = Find.Camera;
            if (camera == null)
            {
                return false;
            }

            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f)
            {
                return false;
            }

            float scale = camera.pixelHeight > 0
                ? UI.screenHeight / (float)camera.pixelHeight
                : 1f / Mathf.Max(Prefs.UIScale, 0.01f);
            gui = new RimUnity.Vector2(screen.x * scale, UI.screenHeight - screen.y * scale);
            return true;
        }

        /// <summary>One map cell in interface pixels, so widths can be authored in cells.</summary>
        public static float CellSize()
        {
            CameraDriver driver = Find.CameraDriver;
            return Mathf.Max(1f, driver != null ? driver.CellSizePixels : UI.CurUICellSize());
        }

        public static RimUnity.Material BeamMaterial()
        {
            EnsureMaterials();
            return beamMaterial;
        }

        public static RimUnity.Material OrbMaterial()
        {
            EnsureMaterials();
            return orbMaterial;
        }

        /// <summary>
        /// Covers the whole viewport with one alpha-blended quad, drawn in device pixels.
        ///
        /// It cannot mirror the picture: it never reads or samples the frame buffer, so no texel is
        /// relocated — the scene stays exactly where it is and only a translucent colour is composited
        /// over it. A screen pass that resamples the previous image is the only way to flip it.
        ///
        /// It cannot cover only part of the viewport either: the rect is the back buffer itself
        /// (Screen.width x Screen.height) drawn under an identity GUI matrix, so it does not depend on
        /// Prefs.UIScale, on UI.screenWidth/screenHeight being current, or on a GUI matrix left behind
        /// by another drawer. <see cref="Verse.Root"/> draws the game's own full-screen fade the same
        /// way, one screen-sized rect per frame.
        ///
        /// MapComponentOnGUI runs at the top level of the GUI pass
        /// (MapInterface.MapInterfaceOnGUI_BeforeMainTabs): no group or clip is active there, and the
        /// buttons, alerts, windows and tooltips are drawn afterwards, so the tint is over the map and
        /// under the interface.
        /// </summary>
        public static void DrawScreenTint(Color color)
        {
            if (color.a <= 0f || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Color previousColor = GUI.color;
            Matrix4x4 previousMatrix = GUI.matrix;
            try
            {
                GUI.color = color;
                GUI.matrix = Matrix4x4.identity;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture,
                    ScaleMode.StretchToFill, true);
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
            }
        }

        /// <summary>Full-width band between two interface points, drawn as one textured quad.</summary>
        public static void DrawBand(RimUnity.Material material, RimUnity.Vector2 from, RimUnity.Vector2 to,
            float halfWidthPixels, RimUnity.Color color)
        {
            var delta = new Vector2(to.x - from.x, to.y - from.y);
            float length = delta.magnitude;
            if (material == null || length < 0.01f)
            {
                return;
            }

            var normal = new Vector2(-delta.y / length, delta.x / length) * halfWidthPixels;
            RimUnity.UI.ImmediateMesh.DrawQuad(
                material,
                Offset(from, normal), Offset(to, normal), Offset(to, -normal), Offset(from, -normal),
                new RimUnity.Vector2(0f, 0f), new RimUnity.Vector2(1f, 0f),
                new RimUnity.Vector2(1f, 1f), new RimUnity.Vector2(0f, 1f),
                color);
        }

        /// <summary>Axis-aligned square (the charge orb), centred on an interface point.</summary>
        public static void DrawSquare(RimUnity.Material material, RimUnity.Vector2 center, float halfSizePixels,
            RimUnity.Color color)
        {
            if (material == null || halfSizePixels <= 0f)
            {
                return;
            }

            float min = center.x - halfSizePixels;
            float maxY = center.y + halfSizePixels;
            RimUnity.UI.ImmediateMesh.DrawQuad(
                material,
                new RimUnity.Vector2(min, center.y - halfSizePixels),
                new RimUnity.Vector2(center.x + halfSizePixels, center.y - halfSizePixels),
                new RimUnity.Vector2(center.x + halfSizePixels, maxY),
                new RimUnity.Vector2(min, maxY),
                new RimUnity.Vector2(0f, 0f), new RimUnity.Vector2(1f, 0f),
                new RimUnity.Vector2(1f, 1f), new RimUnity.Vector2(0f, 1f),
                color);
        }

        /// <summary>
        /// Shortens a beam to the map, so it ends at the edge instead of being drawn over the void.
        /// Returns false only when the origin is already outside the map.
        /// </summary>
        public static bool TryClipToMap(Map map, float originX, float originZ, float dirX, float dirZ,
            float maxLength, out float length)
        {
            length = 0f;
            if (map == null || maxLength <= 0f)
            {
                return false;
            }

            float enter = 0f;
            float exit = maxLength;
            if (!ClipAxis(originX, dirX, map.Size.x, ref enter, ref exit)
                || !ClipAxis(originZ, dirZ, map.Size.z, ref enter, ref exit)
                || exit <= 0f || enter > exit)
            {
                return false;
            }

            length = exit;
            return true;
        }

        public static RimUnity.Color WithAlpha(RimUnity.Color color, float alpha)
        {
            return new RimUnity.Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }

        private static RimUnity.Vector2 Offset(RimUnity.Vector2 point, Vector2 offset)
        {
            return new RimUnity.Vector2(point.x + offset.x, point.y + offset.y);
        }

        private static bool ClipAxis(float origin, float direction, int size, ref float enter, ref float exit)
        {
            if (Mathf.Abs(direction) <= 0.0001f)
            {
                return origin >= 0f && origin <= size;
            }

            float first = (0f - origin) / direction;
            float second = (size - origin) / direction;
            if (first > second)
            {
                float swap = first;
                first = second;
                second = swap;
            }

            if (first > enter)
            {
                enter = first;
            }

            if (second < exit)
            {
                exit = second;
            }

            return enter <= exit;
        }

        private static void EnsureMaterials()
        {
            // RimUnity drops its objects when the game session changes, so the wrappers are rebuilt
            // for a new session instead of being reused as stale handles.
            int gameId = Current.Game != null ? Current.Game.GetHashCode() : 0;
            if (materialsReady && materialGameId != gameId)
            {
                materialsReady = false;
                beamMaterial = null;
                orbMaterial = null;
                bandTexture = null;
                orbTexture = null;
            }

            if (materialsReady)
            {
                return;
            }

            RimUnity.Shader shader = RimUnity.Shader.Find(AdditiveShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "RimUnity shader " + AdditiveShaderName + " was not found; the railgun beam cannot be drawn.");
            }

            bandTexture = CreateBandTexture();
            orbTexture = CreateOrbTexture();
            beamMaterial = new RimUnity.Material(shader) { mainTexture = bandTexture };
            orbMaterial = new RimUnity.Material(shader) { mainTexture = orbTexture };
            materialGameId = gameId;
            materialsReady = true;
        }

        /// <summary>White band, bright in the middle and transparent at both edges, constant along U.</summary>
        private static RimUnity.Texture2D CreateBandTexture()
        {
            const int width = 8;
            const int height = 64;
            var pixels = new RimUnity.Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                float shape = Mathf.Sin(v * Mathf.PI);
                float alpha = Mathf.Clamp01(shape * shape * 1.35f);
                var pixel = new RimUnity.Color32(255, 255, 255, (byte)(alpha * 255f));
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = pixel;
                }
            }

            return Upload("WotF_RailgunBeam", width, height, pixels);
        }

        /// <summary>White soft dot with a hot centre, for the charge orb.</summary>
        private static RimUnity.Texture2D CreateOrbTexture()
        {
            const int size = 64;
            var pixels = new RimUnity.Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    float alpha = Mathf.Clamp01(1f - radius);
                    alpha = alpha * alpha * alpha;
                    pixels[y * size + x] = new RimUnity.Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            return Upload("WotF_RailgunOrb", size, size, pixels);
        }

        private static RimUnity.Texture2D Upload(string name, int width, int height, RimUnity.Color32[] pixels)
        {
            var texture = new RimUnity.Texture2D(width, height, RimUnity.TextureFormat.RGBA32, false, true)
            {
                name = name
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
