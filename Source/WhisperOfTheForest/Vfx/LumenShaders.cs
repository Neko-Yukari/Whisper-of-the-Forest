using System;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>
    /// Cached world materials routed to Layer 15 (RimLumenRenderHost.LumenRenderPassId).
    /// Marked StaticConstructorOnStartup per static-asset loading rules; materials materialize lazily
    /// on main-thread draw once DefDatabase&lt;ShaderDef&gt; and ShaderDefRegistry are populated.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class LumenShaders
    {
        public const int LumenLayer = 15;

        public const string RibbonDepthShaderName = "WotF_Shader_LumenRibbonDepth";
        public const string RibbonOverShaderName = "WotF_Shader_LumenRibbonOver";

        private static Material ribbonDepth;
        private static Material ribbonOver;

        public static Material RibbonDepth => ribbonDepth ??= LoadMaterial(RibbonDepthShaderName);

        public static Material RibbonOver => ribbonOver ??= LoadMaterial(RibbonOverShaderName);

        public static void DrawLumen(Mesh mesh, Material material)
        {
            if (mesh == null || material == null || mesh.vertexCount == 0)
            {
                return;
            }

            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, LumenLayer);
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
