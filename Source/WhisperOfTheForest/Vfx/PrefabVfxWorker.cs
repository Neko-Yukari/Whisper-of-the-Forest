using System;
using Verse;
using Verse.Sound;

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
        private bool holdsBloom;
        private UnityEngine.Vector3 lastPosition;
        private int lostTick = -1;
        private int finishTick;
        private RimUnity.GameObject strike;
        private int strikeTick = -1;
        private RimUnity.Animator animator;

        protected override void OnStart()
        {
            def = (PrefabVfxDef)Def;
            startTick = Find.TickManager.TicksGame;
            lastPosition = Request.origin;
            if (def.scaleToWidth && Request.width <= 0f)
                throw new ArgumentException("vfx " + def.defName + " requires a positive request width");
        }

        public override void Trigger(string state)
        {
            if (instance == null) Spawn();
            animator.SetTrigger(state);
            animator.Update(0f);
            // State-entry bursts are emitted synchronously, before the owner's same-tick destruction.
            var node = instance.transform.Find(state);
            int emitted = 0;
            if (node != null)
                foreach (var ps in node.gameObject.GetComponentsInChildren<RimUnity.ParticleSystem>(true))
                {
                    ps.gameObject.SetActive(true);
                    var bursts = new RimUnity.ParticleSystem.Burst[ps.emission.burstCount];
                    ps.emission.GetBursts(bursts);
                    foreach (var burst in bursts)
                    {
                        int count = (int)burst.count.Evaluate(0f, 1f);
                        ps.Emit(count);
                        emitted += count;
                    }
                }
            var info = animator.GetCurrentAnimatorStateInfo(0);
            VfxDiagnostics.Info(def.defName + " trigger=" + state + " tick=" + Find.TickManager.TicksGame
                + " state=" + (info.IsName(state) ? state : "hash:" + info.shortNameHash)
                + " normalized=" + info.normalizedTime + " emitted=" + emitted);
        }

        public override void FollowLost()
        {
            if (lostTick >= 0 || Finished) return;
            if (!(def is SpellPrefabVfxDef spell) || def.fadeTicks == 0 || instance == null)
            {
                base.FollowLost();
                return;
            }
            lostTick = Find.TickManager.TicksGame;
            finishTick = lostTick + def.fadeTicks + spell.MaxParticleLifetimeTicks;
            foreach (var system in systems)
                system.Stop(false, RimUnity.ParticleSystemStopBehavior.StopEmitting);
            int particles = 0;
            foreach (var system in systems) particles += system.particleCount;
            VfxDiagnostics.Debug("prefab " + def.prefabPath + " detached tick=" + lostTick + " deadline=" + finishTick + " particlesAfterStop=" + particles);
        }

        public override void Update(float deltaSeconds)
        {
            if (Request.follow != null && (Request.follow.Destroyed || !Request.follow.Spawned || Request.follow.Map != Request.map))
            {
                FollowLost();
                if (Finished) return;
            }

            if (lostTick >= 0)
            {
                bool alive = false;
                if (systems != null)
                    foreach (var system in systems) alive |= system.IsAlive(false);
                if (!alive || Find.TickManager.TicksGame >= finishTick)
                {
                    VfxDiagnostics.Debug("prefab " + def.prefabPath + " tail ended tick=" + Find.TickManager.TicksGame);
                    Finished = true;
                    return;
                }
            }

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

            if (lostTick < 0 && Request.follow != null) lastPosition = Request.follow.DrawPos;
            var position = lastPosition;
            instance.transform.position = new RimUnity.Vector3(position.x, def.height, position.z);

            if (strike != null && def is SpellPrefabVfxDef timed)
            {
                int tick = Find.TickManager.TicksGame;
                if (strikeTick < 0 && lostTick < 0 && tick - startTick >= timed.strikeDelayTicks)
                {
                    strikeTick = tick;
                    strike.SetActive(true);
                    foreach (var system in strike.GetComponentsInChildren<RimUnity.ParticleSystem>(true)) system.Play(false);
                    timed.strikeSound.PlayOneShot(SoundInfo.InMap(new TargetInfo(Request.origin.ToIntVec3(), Request.map)));
                }
                if (strikeTick >= 0 && tick - strikeTick >= timed.strikeDurationTicks)
                    foreach (var line in strike.GetComponentsInChildren<RimUnity.LineRenderer>(true)) line.enabled = false;
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

        // A spawn that throws leaves no half-built instance behind: the worker is released and
        // finished before the exception propagates, so the defect is reported once, not every frame.
        private void Spawn()
        {
            try
            {
                SpawnInstance();
            }
            catch
            {
                Despawn();
                Finished = true;
                throw;
            }
        }

        private void SpawnInstance()
        {
            RimUnity.GameObject template = RimUnity.Assets.PrefabLoader.Load(def.modContentPack, def.prefabPath);
            instance = RimUnity.Object.Instantiate(template);
            float scale = def.scale * (def.scaleToWidth ? Request.width : 1f);
            instance.transform.localScale = new RimUnity.Vector3(scale, scale, scale);
            if (lostTick < 0 && Request.follow != null) lastPosition = Request.follow.DrawPos;
            var position = lastPosition;
            instance.transform.position = new RimUnity.Vector3(position.x, def.height, position.z);
            // RimUnity renderers share Component, not a Renderer base class.
            foreach (RimUnity.Component component in instance.GetComponentsInChildren<RimUnity.Component>(true))
            {
                if (component.gameObject.layer != 29 || !(component is RimUnity.ParticleSystemRenderer
                    || component is RimUnity.MeshRenderer || component is RimUnity.SkinnedMeshRenderer
                    || component is RimUnity.LineRenderer || component is RimUnity.TrailRenderer
                    || component is RimUnity.SpriteRenderer)) continue;
                VfxBloomLayers.Acquire(29);
                holdsBloom = true;
                break;
            }
            instance.SetActive(true);

            RimUnity.ParticleSystem root = instance.GetComponent<RimUnity.ParticleSystem>();
            if (root == null)
            {
                throw new InvalidOperationException("prefab " + def.prefabPath + " (vfx " + def.defName + ") needs a ParticleSystem on its root");
            }

            systems = instance.GetComponentsInChildren<RimUnity.ParticleSystem>(true);
            root.Play(true);
            animator = instance.GetComponent<RimUnity.Animator>();
            if (animator != null)
            {
                animator.updateMode = RimUnity.AnimatorUpdateMode.Normal;
                animator.speed = 1f;
            }
            if (def is SpellPrefabVfxDef declared && declared.states != null)
            {
                if (animator == null) throw new InvalidOperationException(def.defName + ": states require Animator");
                foreach (string state in declared.states)
                {
                    var node = instance.transform.Find(state);
                    if (node == null) continue;
                    foreach (var ps in node.gameObject.GetComponentsInChildren<RimUnity.ParticleSystem>(true))
                        ps.Stop(false, RimUnity.ParticleSystemStopBehavior.StopEmittingAndClear);
                    node.gameObject.SetActive(false);
                }
            }
            if (!string.IsNullOrEmpty(Request.initialState))
            {
                if (animator == null) throw new InvalidOperationException(def.defName + ": snapshot requires Animator");
                RimUnity.AnimationClip clip = null;
                foreach (var layer in animator.runtimeAnimatorController.layers)
                    foreach (var state in layer.states)
                        if (state.name == Request.initialState) clip = state.motion.clip;
                if (clip == null || clip.length <= 0f)
                    throw new InvalidOperationException(def.defName + ": missing snapshot clip '" + Request.initialState + "'");
                animator.Play(Request.initialState, -1, Request.initialElapsedTicks / (clip.length * 60f));
                animator.Update(0f);
            }
            if (def is SpellPrefabVfxDef timed && timed.strikeDurationTicks > 0)
            {
                strike = instance.transform.Find("Strike")?.gameObject;
                if (strike == null) throw new InvalidOperationException(def.defName + ": missing Strike node");
                if (timed.strikeSound == null) throw new InvalidOperationException(def.defName + ": unresolved strikeSound");
                foreach (var system in strike.GetComponentsInChildren<RimUnity.ParticleSystem>(true))
                    system.Stop(false, RimUnity.ParticleSystemStopBehavior.StopEmittingAndClear);
                strike.SetActive(false);
            }
            VfxDiagnostics.Debug("prefab " + def.prefabPath + " spawned at " + Request.origin + " systems=" + systems.Length);
        }

        private void Despawn()
        {
            if (instance != null)
            {
                RimUnity.Object.Destroy(instance);
                VfxDiagnostics.Debug("prefab " + def.prefabPath + " despawned");
            }

            if (holdsBloom)
            {
                VfxBloomLayers.Release(29);
                holdsBloom = false;
            }
            instance = null;
            systems = null;
            strike = null;
            animator = null;
        }
    }
}
