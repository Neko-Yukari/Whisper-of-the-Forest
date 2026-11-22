using System;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>
    /// Cached world materials routed to RimLumen's custom render layers:
    ///   - Layer 15 (<c>RimLumenRenderHost.LumenRenderPassId</c>): unclamped HDR additive ribbons &amp; shields.
    ///   - Layer 14 (<c>RimLumenRenderHost.DistortionPassId</c>): RG16F additive screen-space refraction offsets.
    /// Marked StaticConstructorOnStartup per static-asset loading rules; materials materialize lazily
    /// on main-thread draw once DefDatabase&lt;ShaderDef&gt; and ShaderDefRegistry are populated.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class LumenShaders
    {
        public const int LumenDistortionLayer = 14;
        public const int LumenLayer = 15;

        public const string RibbonDepthShaderName = "WotF_Shader_LumenRibbonDepth";
        public const string RibbonOverShaderName = "WotF_Shader_LumenRibbonOver";
        public const string DistortionShaderName = "WotF_Shader_LumenDistortion";
        public const string TornadoShaderName = "WotF_Shader_LumenTornado";
        public const string FacetOverShaderName = "WotF_Shader_LumenFacetOver";

        private static Material ribbonDepth;
        private static Material ribbonOver;
        private static Material distortion;
        private static Material tornado;
        private static Material facetOver;

        public static Material RibbonDepth => ribbonDepth ??= LoadMaterial(RibbonDepthShaderName);

        public static Material RibbonOver => ribbonOver ??= LoadMaterial(RibbonOverShaderName);

        public static Material Distortion => distortion ??= LoadMaterial(DistortionShaderName);

        public static Material Tornado => tornado ??= LoadMaterial(TornadoShaderName);

        public static Material FacetOver => facetOver ??= LoadMaterial(FacetOverShaderName);

        public static void DrawLumen(Mesh mesh, Material material)
        {
            if (mesh == null || material == null || mesh.vertexCount == 0)
            {
                return;
            }

            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, LumenLayer);
        }

        public static void DrawDistortion(Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount == 0)
            {
                return;
            }

            Graphics.DrawMesh(mesh, Matrix4x4.identity, Distortion, LumenDistortionLayer);
        }

        private static Material LoadMaterial(string shaderDefName)
        {
            ShaderDef def = DefDatabase<ShaderDef>.GetNamed(shaderDefName, false);
            if (def == null || def.Shader == null)
            {
                throw new InvalidOperationException("LumenShaders: missing ShaderDef '" + shaderDefName + "'");
            }

            return MaterialPool.MatFrom(def.Shader);
        }
    }
}
