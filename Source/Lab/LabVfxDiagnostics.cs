using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Verse;
using WhisperOfTheForest.Vfx;

namespace WhisperOfTheForest
{
    internal static class LabVfxDiagnostics
    {
        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        public static string Capture(Map map)
        {
            return "\"action\":\"diag\",\"tick\":" + Find.TickManager.TicksGame
                + ",\"frameCount\":" + UnityEngine.Time.frameCount
                + ",\"paused\":" + Json.Bool(Find.TickManager.Paused)
                + ",\"bloom\":" + Attempt(Bloom)
                + ",\"effects\":" + Attempt(() => Effects(map));
        }

        private static string Bloom()
        {
            object settings = RimUnity.RimUnityMod.Bloom;
            // BloomSettings.cs:89-102: Snapshot clamps effective values; Describe reports raw settings only.
            string effective = Attempt(() =>
            {
                object snapshot = Invoke(settings, "Snapshot");
                return "{" + Field("threshold", () => Read(snapshot, "Threshold"))
                    + "," + Field("intensity", () => Read(snapshot, "Intensity"))
                    + "," + Field("softKnee", () => Read(snapshot, "SoftKnee"))
                    + "," + Field("scatter", () => Read(snapshot, "Scatter"))
                    + "," + Field("enabled", () => Read(snapshot, "Enabled")) + "}";
            });
            return "{" + Field("describe", () => Invoke(settings, "Describe"))
                + ",\"effective\":" + effective
                + "," + Field("effectiveLayerMask", () => Read(settings, "EffectiveLayerMask"))
                + "," + Field("layer29Enabled", () => ((int)Read(settings, "EffectiveLayerMask") & (1 << 29)) != 0)
                + "," + Field("mapHdrEnabled", () => Read(settings, "mapHdrEnabled"))
                + ",\"pipelines\":" + Attempt(Pipelines) + "}";
        }

        private static string Pipelines()
        {
            // BloomHooks.cs:49: enumerate existing pipelines, never call Get (which creates one).
            Type hooks = typeof(RimUnity.RimUnityMod).Assembly.GetType("RimUnity.Rendering.BloomHooks", true);
            var rows = new List<string>();
            foreach (object entry in (IEnumerable)Read(hooks, "Pipelines"))
            {
                rows.Add(Attempt(() =>
                {
                    object pipeline = Read(entry, "Value");
                    // BloomPipeline.cs:109,123-129,424,435: exact composite scene and allocated source size.
                    return "{" + Field("saturation", () => Read(Read(pipeline, "scene"), "Saturation"))
                        + "," + Field("sourceWidth", () => Read(pipeline, "width"))
                        + "," + Field("sourceHeight", () => Read(pipeline, "height"))
                        + "," + Field("targetsAllocated", () => Read(pipeline, "targets") != null)
                        + "," + Field("prepared", () => Read(pipeline, "prepared")) + "}";
                }));
            }
            if (rows.Count == 0) throw new InvalidOperationException("BloomHooks.Pipelines has no existing pipeline; composite saturation unavailable");
            return "[" + string.Join(",", rows) + "]";
        }

        private static string Effects(Map map)
        {
            var rows = new List<string>();
            // WotF MapComponent_Vfx.cs:16; PrefabVfxWorker.cs:21. No spawning/initialization on inspection.
            foreach (VfxWorker worker in (IEnumerable)Read(map.GetComponent<MapComponent_Vfx>(), "workers"))
            {
                if (worker.Finished) continue;
                string root = Attempt(() =>
                {
                    var instance = (RimUnity.GameObject)Read(worker, "instance");
                    if (instance == null) throw new InvalidOperationException("worker instance is null (not spawned)");
                    return "{" + Field("name", () => instance.name)
                        + "," + Field("scale", () => instance.transform.localScale)
                        + ",\"particleSystems\":" + Attempt(() => Systems(instance))
                        + ",\"textures\":" + Attempt(() => Textures(instance)) + "}";
                });
                rows.Add("{\"def\":" + Json.Str(worker.Def.defName)
                    + ",\"workerType\":" + Json.Str(worker.GetType().FullName) + ",\"root\":" + root + "}");
            }
            return "[" + string.Join(",", rows) + "]";
        }

        private static string Systems(RimUnity.GameObject root)
        {
            var rows = new List<string>();
            foreach (var system in root.GetComponentsInChildren<RimUnity.ParticleSystem>(true))
            {
                rows.Add("{" + Field("name", () => system.gameObject.name)
                    + "," + Field("particleCount", () => system.particleCount)
                    + ",\"particles\":" + Attempt(() =>
                    {
                        // ParticleSystem.cs:3838-3864 only copies; current size uses SizeAt, not Simulate.
                        var particles = new RimUnity.ParticleSystem.Particle[5];
                        int count = system.GetParticles(particles);
                        var samples = new List<string>();
                        for (int i = 0; i < count; i++)
                        {
                            var particle = particles[i];
                            samples.Add("{" + Field("startLifetime", () => particle.startLifetime)
                                + "," + Field("remainingLifetime", () => particle.remainingLifetime)
                                + "," + Field("startSize3D", () => particle.startSize3D)
                                + "," + Field("currentSize3D", () => particle.GetCurrentSize3D(system)) + "}");
                        }
                        return "[" + string.Join(",", samples) + "]";
                    }) + "}");
            }
            return "[" + string.Join(",", rows) + "]";
        }

        private static string Textures(RimUnity.GameObject root)
        {
            var rows = new List<string>();
            foreach (var component in root.GetComponentsInChildren<RimUnity.Component>(true))
            {
                string[] slots;
                if (component is RimUnity.ParticleSystemRenderer) slots = new[] { "sharedMaterial", "trailMaterial" };
                else if (component is RimUnity.MeshRenderer || component is RimUnity.SkinnedMeshRenderer
                    || component is RimUnity.LineRenderer || component is RimUnity.TrailRenderer) slots = new[] { "sharedMaterials" };
                else if (component is RimUnity.SpriteRenderer) slots = new[] { "sharedMaterial" };
                else continue;

                // Never read renderer.material/materials: these getters clone and assign materials.
                foreach (string slot in slots)
                {
                    rows.Add("{\"renderer\":" + Json.Str(component.gameObject.name)
                        + ",\"type\":" + Json.Str(component.GetType().Name)
                        + ",\"slot\":" + Json.Str(slot) + ",\"materials\":" + Attempt(() =>
                        {
                            object value = Read(component, slot);
                            if (value == null) return "null";
                            if (value is RimUnity.Material material) return "[" + MaterialTextures(material) + "]";
                            var materials = new List<string>();
                            foreach (RimUnity.Material item in (IEnumerable)value)
                                materials.Add(item == null ? "null" : Attempt(() => MaterialTextures(item)));
                            return "[" + string.Join(",", materials) + "]";
                        }) + "}");
                }
                if (component is RimUnity.SpriteRenderer spriteRenderer)
                    rows.Add("{\"spriteRenderer\":" + Json.Str(component.gameObject.name)
                        + ",\"texture\":" + Attempt(() => spriteRenderer.sprite == null ? "null" : Texture(spriteRenderer.sprite.texture)) + "}");
            }
            foreach (var system in root.GetComponentsInChildren<RimUnity.ParticleSystem>(true))
            {
                rows.Add("{\"particleSystem\":" + Json.Str(system.gameObject.name)
                    + ",\"textureSheetSprites\":" + Attempt(() =>
                    {
                        var module = system.textureSheetAnimation;
                        if (!module.enabled || module.mode != RimUnity.ParticleSystemAnimationMode.Sprites) return "[]";
                        var sprites = new List<string>();
                        foreach (object sprite in (IEnumerable)Read(module, "sprites"))
                            sprites.Add(Attempt(() => Texture(Read(sprite, "texture"))));
                        return "[" + string.Join(",", sprites) + "]";
                    }) + "}");
            }
            return "[" + string.Join(",", rows) + "]";
        }

        private static string MaterialTextures(RimUnity.Material material)
        {
            var rows = new List<string>();
            // Shader.EngineShader -> MaterialLayout.TextureSlots, also used in ParticleSystemRenderer.cs:925-960.
            object layout = Read(Read(material.shader, "EngineShader"), "MaterialLayout");
            foreach (object slot in (IEnumerable)Read(layout, "TextureSlots"))
            {
                rows.Add("{" + Field("property", () => Read(slot, "PropertyName"))
                    + ",\"texture\":" + Attempt(() => Texture(material.GetTexture((int)Read(slot, "PropertyId")))) + "}");
            }
            return "{\"name\":" + Json.Str(material.name) + ",\"textures\":[" + string.Join(",", rows) + "]}";
        }

        private static string Texture(object texture)
        {
            if (texture == null) return "null";
            return "{" + Field("name", () => Read(texture, "name"))
                + "," + Field("width", () => Read(texture, "width"))
                + "," + Field("height", () => Read(texture, "height"))
                + "," + Field("mipmapCount", () => Read(texture, "mipmapCount")) + "}";
        }

        private static object Read(object target, string name)
        {
            if (target == null) throw new InvalidOperationException("Cannot read " + name + " from null");
            Type type = target as Type ?? target.GetType();
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, Members);
                if (field != null) return field.GetValue(target is Type ? null : target);
                PropertyInfo property = current.GetProperty(name, Members);
                if (property != null) return property.GetValue(target is Type ? null : target, null);
            }
            throw new MissingMemberException(type.FullName, name);
        }

        private static object Invoke(object target, string name)
        {
            if (target == null) throw new InvalidOperationException("Cannot invoke " + name + " on null");
            MethodInfo method = target.GetType().GetMethod(name, Members, null, Type.EmptyTypes, null);
            if (method == null) throw new MissingMethodException(target.GetType().FullName, name);
            return method.Invoke(target, null);
        }

        private static string Field(string name, Func<object> read)
        {
            return Json.Str(name) + ":" + Attempt(() => Value(read()));
        }

        private static string Value(object value)
        {
            if (value == null) return "null";
            if (value is string text) return Json.Str(text);
            if (value is bool flag) return Json.Bool(flag);
            if (value is int number) return Json.Num(number);
            if (value is float scalar)
            {
                if (float.IsNaN(scalar) || float.IsInfinity(scalar)) throw new InvalidOperationException("Non-finite number: " + scalar);
                return scalar.ToString("R", CultureInfo.InvariantCulture);
            }
            if (value is RimUnity.Vector3 vector)
                return "[" + Value(vector.x) + "," + Value(vector.y) + "," + Value(vector.z) + "]";
            throw new NotSupportedException("Diagnostic value type: " + value.GetType().FullName);
        }

        private static string Attempt(Func<string> read)
        {
            try { return read(); }
            catch (Exception error)
            {
                if (error is TargetInvocationException invocation && invocation.InnerException != null)
                    error = invocation.InnerException;
                return "{\"readFailed\":true,\"error\":" + Json.Str(error.GetType().FullName + ": " + error.Message) + "}";
            }
        }
    }
}
