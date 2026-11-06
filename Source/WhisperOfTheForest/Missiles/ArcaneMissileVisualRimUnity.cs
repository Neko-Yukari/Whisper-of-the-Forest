using System;
using WhisperOfTheForest.Vfx;
using Verse;

namespace WhisperOfTheForest.Missiles
{
    /// <summary>
    /// RimUnity backend for one flying missile: a world-space particle comet with a TrailRenderer
    /// ribbon, created for the followed projectile and destroyed with it. Main thread only.
    ///
    /// The effect is world-space, not map-scoped, so it is created only while its map is the current
    /// one and torn down when the player looks at another map; it is rebuilt when the map comes back.
    /// </summary>
    public class ArcaneMissileRimUnityVfxWorker : VfxWorker
    {
        /// <summary>
        /// Additive particle shader without depth testing: a flying comet must stay visible above grass
        /// and tree canopies instead of being clipped by their depth.
        /// </summary>
        private const string ParticleShaderName = "WotF_ParticleAdditiveNoDepth";

        private static RimUnity.Material particleMaterial;
        private static RimUnity.Material trailMaterial;
        private static int materialsGameId = int.MinValue;
        private static bool materialsReady;

        private ArcaneMissileSettings settings;
        private RimUnity.Color color;
        private RimUnity.GameObject root;
        private RimUnity.ParticleSystem particles;

        protected override void OnStart()
        {
            ArcaneMissileVfxRequest request = Request as ArcaneMissileVfxRequest;
            if (request == null || request.settings == null)
            {
                throw new ArgumentException("the RimUnity missile effect needs an ArcaneMissileVfxRequest with settings");
            }

            settings = request.settings;
            color = VfxColors.Parse(settings.missileColorHex);
        }

        public override void Update(float deltaSeconds)
        {
            if (Request.map != Find.CurrentMap)
            {
                Despawn();
                return;
            }

            Projectile_ArcaneMissiles missile = Request.follow as Projectile_ArcaneMissiles;
            if (missile?.State == null)
            {
                return;
            }

            if (root == null)
            {
                Spawn();
            }

            ArcaneMissileVfxPosition.Of(missile.State, out float x, out float y, out float z);
            root.transform.position = new RimUnity.Vector3(x, y, z);
        }

        protected override void Release()
        {
            Despawn();
        }

        private void Spawn()
        {
            EnsureMaterials();
            ArcaneMissileVfxPosition.Of((Request.follow as Projectile_ArcaneMissiles).State,
                out float x, out float y, out float z);

            root = new RimUnity.GameObject("WotF_ArcaneMissileFx");
            root.transform.position = new RimUnity.Vector3(x, y, z);

            particles = root.AddComponent<RimUnity.ParticleSystem>();

            RimUnity.ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.duration = 1f;
            main.playOnAwake = false;
            main.simulationSpace = RimUnity.ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            main.startLifetime = new RimUnity.ParticleSystem.MinMaxCurve(0.22f, 0.42f);
            main.startSpeed = new RimUnity.ParticleSystem.MinMaxCurve(0.02f, 0.18f);
            main.startSize = new RimUnity.ParticleSystem.MinMaxCurve(0.22f * settings.missileSize, 0.5f * settings.missileSize);
            main.startColor = new RimUnity.ParticleSystem.MinMaxGradient(color);
            main.gravityModifier = 0f;

            particles.emission.enabled = true;
            particles.emission.rateOverTime = new RimUnity.ParticleSystem.MinMaxCurve(45f);

            particles.shape.enabled = true;
            particles.shape.shapeType = RimUnity.ParticleSystemShapeType.Sphere;
            particles.shape.radius = 0.05f;

            ApplyRenderer(particles);

            RimUnity.TrailRenderer trail = root.AddComponent<RimUnity.TrailRenderer>();
            trail.time = settings.trailTime;
            trail.minVertexDistance = 0.08f;
            trail.startWidth = 0.34f * settings.missileSize;
            trail.endWidth = 0.02f;
            trail.startColor = color;
            trail.endColor = new RimUnity.Color(color.r, color.g, color.b, 0f);
            trail.sharedMaterial = trailMaterial;
            trail.emitting = true;
            trail.Clear();

            particles.Play(true);
        }

        private void Despawn()
        {
            if (root != null)
            {
                RimUnity.Object.Destroy(root);
            }

            root = null;
            particles = null;
        }

        /// <summary>The shared particle material; also used by the impact burst of this backend.</summary>
        internal static RimUnity.Material ParticleMaterial()
        {
            EnsureMaterials();
            return particleMaterial;
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

        private static void EnsureMaterials()
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
                return;
            }

            RimUnity.Shader shader = RimUnity.Shader.Find(ParticleShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "RimUnity shader " + ParticleShaderName + " was not found; the arcane missile trail cannot be drawn.");
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
                    float radius = (float)Math.Sqrt(nx * nx + ny * ny);
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
    }

    /// <summary>
    /// The detonation burst of the RimUnity backend: one particle object that scatters, then the
    /// worker ends on the game clock. A debug probe (request.seconds &gt; 0) keeps emitting for the
    /// requested time instead, which is how a backend can be checked without firing a missile.
    /// </summary>
    public class ArcaneMissileRimUnityImpactVfxWorker : VfxWorker
    {
        private const float ImpactBurstSeconds = 0.7f;

        private RimUnity.GameObject burst;
        private ArcaneMissileSettings settings;
        private float durationSeconds;
        private int startTick;

        protected override void OnStart()
        {
            ArcaneMissileVfxRequest request = Request as ArcaneMissileVfxRequest;
            if (request == null || request.settings == null)
            {
                throw new ArgumentException("the RimUnity missile impact needs an ArcaneMissileVfxRequest with settings");
            }

            settings = request.settings;
            durationSeconds = request.seconds > 0f ? request.seconds : ImpactBurstSeconds;
            startTick = Find.TickManager.TicksGame;
            CreateBurst(looping: request.seconds > 0f);
        }

        public override void Update(float deltaSeconds)
        {
            if (Request.map != Find.CurrentMap)
            {
                Finished = true;
                return;
            }

            float elapsed = (Find.TickManager.TicksGame - startTick) / (float)GenTicks.TicksPerRealSecond;
            if (elapsed >= durationSeconds)
            {
                Finished = true;
            }
        }

        protected override void Release()
        {
            if (burst != null)
            {
                RimUnity.Object.Destroy(burst);
            }

            burst = null;
        }

        private void CreateBurst(bool looping)
        {
            RimUnity.Color color = VfxColors.Parse(settings.missileColorHex);
            float size = settings.missileSize * 1.6f;

            burst = new RimUnity.GameObject("WotF_ArcaneMissileBurst");
            burst.transform.position = new RimUnity.Vector3(Request.origin.x, Request.origin.y, Request.origin.z);

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

            var renderer = system.gameObject.GetComponent<RimUnity.ParticleSystemRenderer>();
            if (renderer == null)
            {
                renderer = system.gameObject.AddComponent<RimUnity.ParticleSystemRenderer>();
            }

            renderer.sharedMaterial = ArcaneMissileRimUnityVfxWorker.ParticleMaterial();
            renderer.renderMode = RimUnity.ParticleSystemRenderMode.Billboard;
            renderer.sortMode = RimUnity.ParticleSystemSortMode.None;
            system.Play(true);

            if (!looping)
            {
                var random = new Random(Gen.HashCombineInt(Find.TickManager.TicksGame, (int)(Request.origin.x * 100f + Request.origin.z)));
                for (int i = 0; i < 22; i++)
                {
                    float angle = (float)(random.NextDouble() * Math.PI * 2.0);
                    float speed = 0.4f + (float)random.NextDouble() * 1.8f;
                    var emit = new RimUnity.ParticleSystem.EmitParams
                    {
                        position = RimUnity.Vector3.zero,
                        velocity = new RimUnity.Vector3((float)Math.Cos(angle) * speed, 0.35f + (float)random.NextDouble() * 0.8f, (float)Math.Sin(angle) * speed),
                        startSize = size * (0.35f + (float)random.NextDouble() * 0.45f),
                        startColor = color,
                        startLifetime = 0.3f + (float)random.NextDouble() * 0.3f
                    };
                    system.Emit(emit, 1);
                }
            }
        }
    }
}
