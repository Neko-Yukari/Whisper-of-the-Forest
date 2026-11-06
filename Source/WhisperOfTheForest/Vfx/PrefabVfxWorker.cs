using System;
using Verse;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>
    /// Plays an exported RimUnity prefab at the request origin. Main thread only.
    ///
    /// RimUnity advances particles on real frame time times <c>main.simulationSpeed</c> and already
    /// stops them while the game is paused, but ignores game speed; every frame the speed is set to the
    /// tick rate multiplier so particles stay in step with the ticks the spell resolves on.
    ///
    /// The instance is world-space, not map-scoped: it exists only while its map is the current one and
    /// is rebuilt when the player returns to that map.
    /// </summary>
    public class PrefabVfxWorker : VfxWorker
    {
        private PrefabVfxDef def;
        private int startTick;
        private RimUnity.GameObject instance;
        private RimUnity.ParticleSystem[] systems;

        protected override void OnStart()
        {
            def = (PrefabVfxDef)Def;
            startTick = Find.TickManager.TicksGame;
        }

        public override void Update(float deltaSeconds)
        {
            if (def.lifetime == VfxLifetime.FixedDuration && Find.TickManager.TicksGame - startTick >= def.durationTicks)
            {
                Finished = true;
                return;
            }

            if (Request.map != Find.CurrentMap)
            {
                Despawn();
                return;
            }

            if (instance == null)
            {
                Spawn();
            }

            float speed = Find.TickManager.TickRateMultiplier;
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].main.simulationSpeed = speed;
            }
        }

        protected override void Release()
        {
            Despawn();
        }

        private void Spawn()
        {
            RimUnity.GameObject template = RimUnity.Assets.PrefabLoader.Load(def.modContentPack, def.prefabPath);
            instance = RimUnity.Object.Instantiate(template);
            instance.transform.position = new RimUnity.Vector3(Request.origin.x, def.height, Request.origin.z);
            instance.SetActive(true);

            RimUnity.ParticleSystem root = instance.GetComponent<RimUnity.ParticleSystem>();
            if (root == null)
            {
                throw new InvalidOperationException("prefab " + def.prefabPath + " (vfx " + def.defName + ") needs a ParticleSystem on its root");
            }

            systems = instance.GetComponentsInChildren<RimUnity.ParticleSystem>(true);
            root.Play(true);
            VfxDiagnostics.Debug("prefab " + def.prefabPath + " spawned at " + Request.origin + " systems=" + systems.Length);
        }

        private void Despawn()
        {
            if (instance != null)
            {
                RimUnity.Object.Destroy(instance);
                VfxDiagnostics.Debug("prefab " + def.prefabPath + " despawned");
            }

            instance = null;
            systems = null;
        }
    }
}
