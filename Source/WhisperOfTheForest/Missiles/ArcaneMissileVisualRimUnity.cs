using System;
using System.Collections.Generic;
using System.Globalization;
using RimUnity;
using Verse;

namespace WhisperOfTheForest.Experimental.Missiles
{
    /// <summary>
    /// RimUnity backend: one GameObject per missile with a world-space particle comet and a
    /// TrailRenderer ribbon. Impact bursts and probes are separate objects, owned per map and
    /// destroyed by the map component's frame hook, so nothing depends on delayed callbacks.
    /// Main thread only.
    /// </summary>
    public class ArcaneMissileVisualRimUnity : ArcaneMissileVisualSource
    {
        private sealed class BurstRecord
        {
            public Map Map;
            public RimUnity.GameObject Object;
            public DateTime ExpiresAt;
        }

        private static readonly List<BurstRecord> bursts = new List<BurstRecord>();

        static ArcaneMissileVisualRimUnity()
        {
            RegisterMapScopedEffects(PruneBursts, DestroyBursts);
        }
        /// <summary>Built-in RimUnity additive particle shader (ShaderDef RimUnity_StandardParticle4).</summary>
        private const string ParticleShaderName = "RimUnity_StandardParticle4";

        private const float ImpactBurstSeconds = 0.7f;

        private static RimUnity.Material particleMaterial;
        private static RimUnity.Material trailMaterial;
        private static int materialsGameId = int.MinValue;
        private static bool materialsReady;
        private static bool materialsFailed;

        private RimUnity.GameObject root;
        private RimUnity.ParticleSystem particles;

        public override void Spawn(ArcaneMissileVisualFrame frame)
        {
            if (!EnsureMaterials())
            {
                return;
            }

            VisualPosition(frame, out float x, out float y, out float z);
            root = new RimUnity.GameObject("WotF_ArcaneMissileFx");
            root.transform.position = new RimUnity.Vector3(x, y, z);

            particles = root.AddComponent<RimUnity.ParticleSystem>();
            RimUnity.Color color = ParseColor(frame.Settings.missileColorHex, new RimUnity.Color(0.61f, 0.85f, 1f, 1f));

            RimUnity.ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.duration = 1f;
            main.playOnAwake = false;
            main.simulationSpace = RimUnity.ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            main.startLifetime = new RimUnity.ParticleSystem.MinMaxCurve(0.22f, 0.42f);
            main.startSpeed = new RimUnity.ParticleSystem.MinMaxCurve(0.02f, 0.18f);
            main.startSize = new RimUnity.ParticleSystem.MinMaxCurve(0.22f * frame.Settings.missileSize, 0.5f * frame.Settings.missileSize);
            main.startColor = new RimUnity.ParticleSystem.MinMaxGradient(color);
            main.gravityModifier = 0f;

            particles.emission.enabled = true;
            particles.emission.rateOverTime = new RimUnity.ParticleSystem.MinMaxCurve(45f);

            particles.shape.enabled = true;
            particles.shape.shapeType = RimUnity.ParticleSystemShapeType.Sphere;
            particles.shape.radius = 0.05f;

            ApplyRenderer(particles);

            RimUnity.TrailRenderer trail = root.AddComponent<RimUnity.TrailRenderer>();
            trail.time = frame.Settings.trailTime;
            trail.minVertexDistance = 0.08f;
            trail.startWidth = 0.34f * frame.Settings.missileSize;
            trail.endWidth = 0.02f;
            trail.startColor = color;
            trail.endColor = new RimUnity.Color(color.r, color.g, color.b, 0f);
            trail.sharedMaterial = trailMaterial;
            trail.emitting = true;
            trail.Clear();

            particles.Play(true);
        }

        public override void Update(ArcaneMissileVisualFrame frame)
        {
            if (root == null)
            {
                return;
            }

            VisualPosition(frame, out float x, out float y, out float z);
            root.transform.position = new RimUnity.Vector3(x, y, z);
        }

        public override void Impact(ArcaneMissileImpactVisualContext context)
        {
            CreateBurst(context, ImpactBurstSeconds, looping: false);
        }

        public override void Probe(ArcaneMissileImpactVisualContext context)
        {
            float seconds = context.Settings != null ? context.Settings.lifetimeSeconds : 3f;
            CreateBurst(context, Math.Max(0.6f, seconds), looping: true);
        }

        public override void Dispose()
        {
            if (root != null)
            {
                RimUnity.Object.Destroy(root);
            }

            root = null;
            particles = null;
        }

        /// <summary>Standalone burst object, destroyed by the RimUnity runtime after its delay.</summary>
        private static void CreateBurst(ArcaneMissileImpactVisualContext context, float seconds, bool looping)
        {
            if (!EnsureMaterials())
            {
                return;
            }

            ArcaneMissileSettings settings = context.Settings;
            float size = settings != null ? settings.missileSize * 1.6f : 0.9f;
            RimUnity.Color color = ParseColor(
                settings?.missileColorHex,
                new RimUnity.Color(0.61f, 0.85f, 1f, 1f));

            var burst = new RimUnity.GameObject("WotF_ArcaneMissileBurst");
            burst.transform.position = new RimUnity.Vector3(context.X, context.Y, context.Z);

            RimUnity.ParticleSystem system = burst.AddComponent<RimUnity.ParticleSystem>();
            RimUnity.ParticleSystem.MainModule main = system.main;
            main.loop = looping;
            main.duration = 2f;
            main.playOnAwake = false;
            main.simulationSpace = RimUnity.ParticleSystemSimulationSpace.World;
            main.maxParticles = 128;
            main.startLifetime = new RimUnity.ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSize = new RimUnity.ParticleSystem.MinMaxCurve(0.22f * size, 0.7f * size);
            main.startColor = new RimUnity.ParticleSystem.MinMaxGradient(color);
            main.gravityModifier = 0f;

            system.emission.enabled = looping;
            system.emission.rateOverTime = new RimUnity.ParticleSystem.MinMaxCurve(28f);
            system.shape.enabled = false;

            ApplyRenderer(system);
            system.Play(true);

            if (!looping)
            {
                var random = new Random(Gen.HashCombineInt(Find.TickManager.TicksGame, (int)(context.X * 100f + context.Z)));
                for (int i = 0; i < 22; i++)
                {
                    float angle = (float)(random.NextDouble() * Math.PI * 2.0);
                    float speed = 0.4f + (float)random.NextDouble() * 1.8f;
                    var emit = new RimUnity.ParticleSystem.EmitParams
                    {
                        position = RimUnity.Vector3.zero,
                        velocity = new RimUnity.Vector3(MathF.Cos(angle) * speed, 0.35f + (float)random.NextDouble() * 0.8f, MathF.Sin(angle) * speed),
                        startSize = size * (0.35f + (float)random.NextDouble() * 0.45f),
                        startColor = color,
                        startLifetime = 0.3f + (float)random.NextDouble() * 0.3f
                    };
                    system.Emit(emit, 1);
                }
            }

            bursts.Add(new BurstRecord
            {
                Map = context.Map,
                Object = burst,
                ExpiresAt = DateTime.UtcNow.AddSeconds(Math.Max(0.1f, seconds))
            });
        }

        /// <summary>Per-frame prune, driven by the map component even while the game is paused.</summary>
        private static void PruneBursts(Map map)
        {
            DateTime now = DateTime.UtcNow;
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                BurstRecord record = bursts[i];
                if (record.Object == null)
                {
                    bursts.RemoveAt(i);
                    continue;
                }

                if (record.Map != map || now < record.ExpiresAt)
                {
                    continue;
                }

                RimUnity.Object.Destroy(record.Object);
                bursts.RemoveAt(i);
            }
        }

        /// <summary>Map stopped being current (or was removed): destroy its bursts immediately.</summary>
        private static void DestroyBursts(Map map)
        {
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                BurstRecord record = bursts[i];
                if (record.Map != map)
                {
                    continue;
                }

                if (record.Object != null)
                {
                    RimUnity.Object.Destroy(record.Object);
                }

                bursts.RemoveAt(i);
            }
        }

        private static void ApplyRenderer(RimUnity.ParticleSystem system)
        {
            var renderer = system.gameObject.GetComponent<RimUnity.ParticleSystemRenderer>();
            if (renderer == null)
            {
                renderer = system.gameObject.AddComponent<RimUnity.ParticleSystemRenderer>();
            }

            renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = RimUnity.ParticleSystemRenderMode.Billboard;
            renderer.sortMode = RimUnity.ParticleSystemSortMode.None;
        }

        private static bool EnsureMaterials()
        {
            // RimUnity drops its objects when the game session changes; rebuild the cached
            // wrappers for the new session instead of reusing stale GPU handles.
            int gameId = Current.Game != null ? Current.Game.GetHashCode() : 0;
            if (materialsReady && materialsGameId != gameId)
            {
                materialsReady = false;
                particleMaterial = null;
                trailMaterial = null;
            }

            if (materialsReady)
            {
                return true;
            }

            if (materialsFailed)
            {
                return false;
            }

            try
            {
                RimUnity.Shader shader = RimUnity.Shader.Find(ParticleShaderName);
                if (shader == null)
                {
                    materialsFailed = true;
                    ArcaneMissileDiagnostics.Error(
                        "RimUnity shader " + ParticleShaderName + " was not found; RimUnity visuals are disabled.");
                    return false;
                }

                particleMaterial = new RimUnity.Material(shader)
                {
                    mainTexture = CreateSoftDot()
                };
                trailMaterial = new RimUnity.Material(shader)
                {
                    mainTexture = particleMaterial.mainTexture
                };

                materialsGameId = gameId;
                materialsReady = true;
                return true;
            }
            catch (Exception exception)
            {
                materialsFailed = true;
                ArcaneMissileDiagnostics.Error("RimUnity visual setup failed: " + exception);
                return false;
            }
        }

        /// <summary>Procedural soft dot so the additive particles do not render as hard squares.</summary>
        private static RimUnity.Texture2D CreateSoftDot()
        {
            const int size = 64;
            var texture = new RimUnity.Texture2D(size, size, RimUnity.TextureFormat.RGBA32, false, true);
            var pixels = new RimUnity.Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float radius = MathF.Sqrt(nx * nx + ny * ny);
                    float alpha = Math.Clamp(1f - radius, 0f, 1f);
                    alpha *= alpha;
                    pixels[y * size + x] = new RimUnity.Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.name = "WotF_ArcaneMissileSoftDot";
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>Accepts "#RRGGBB" or "#RRGGBBAA" (6/8 hex digits, leading hash optional).</summary>
        public static RimUnity.Color ParseColor(string hex, RimUnity.Color fallback)
        {
            if (string.IsNullOrEmpty(hex))
            {
                return fallback;
            }

            string digits = hex.Trim().TrimStart('#');
            if (digits.Length != 6 && digits.Length != 8)
            {
                return fallback;
            }

            if (!uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
            {
                return fallback;
            }

            if (digits.Length == 6)
            {
                return new RimUnity.Color(
                    ((value >> 16) & 0xFF) / 255f,
                    ((value >> 8) & 0xFF) / 255f,
                    (value & 0xFF) / 255f,
                    1f);
            }

            return new RimUnity.Color(
                ((value >> 24) & 0xFF) / 255f,
                ((value >> 16) & 0xFF) / 255f,
                ((value >> 8) & 0xFF) / 255f,
                (value & 0xFF) / 255f);
        }
    }
}
