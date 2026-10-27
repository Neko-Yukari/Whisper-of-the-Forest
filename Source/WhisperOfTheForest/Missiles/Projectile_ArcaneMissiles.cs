using System;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Experimental.Missiles
{
    /// <summary>
    /// Native carrier for one arcane missile. <see cref="Verse.Projectile"/> and the game's thing
    /// pipeline own spawning, registration, ticking (including the 1-visible / 15-offscreen update
    /// rate), saving and destruction; this class only supplies what the vanilla straight-line
    /// projectile cannot: a homing step delegated to <see cref="ArcaneMissile"/>, the payload dispatch
    /// already resolved by that model, and the per-missile visual lifecycle.
    ///
    /// <see cref="TickInterval"/> deliberately does not call the vanilla implementation: the base
    /// class would move the missile along its launch line, run its lifetime countdown and resolve
    /// hits through the private intercept checks, which would double-drive the same projectile.
    /// Consequence: shield/interceptor handling of the base class is not wired here (see
    /// Experiments/missiles.md, "native migration").
    /// </summary>
    public class Projectile_ArcaneMissiles : Projectile
    {
        /// <summary>Gameplay state. Serialized with the projectile; own copy, never a Def.</summary>
        private ArcaneMissile state;

        private ArcaneMissileVisualSource visual;

        /// <summary>Set when this missile's backend failed; never retried for this missile.</summary>
        private bool visualBroken;

        /// <summary>True while this missile's map is not the current one, so nothing is spawned.</summary>
        private bool visualsSuspended;

        /// <summary>Set once when the payload/expiry was resolved; keeps settlement single.</summary>
        private bool settled;

        internal ArcaneMissile State => state;

        internal bool VisualBroken => visualBroken;

        /// <summary>
        /// Attaches the already-initialised flight model and fills the native projectile slots other
        /// code may read. The vanilla launch countdown is pinned to zero because this projectile
        /// steers itself: nothing may treat it as a straight dart in flight.
        /// </summary>
        internal void Initialize(ArcaneMissile missile, Pawn caster, LocalTargetInfo firedAt, LocalTargetInfo aimedAt)
        {
            state = missile;
            launcher = caster;
            usedTarget = firedAt;
            intendedTarget = aimedAt;
            origin = new Vector3(missile.posX, def.Altitude, missile.posZ);
            destination = firedAt.Cell.ToVector3Shifted();
            ticksToImpact = 0;
            lifetime = 0;
        }

        /// <summary>Authoritative position for drawing and for the map grid: the model's own point.</summary>
        public override Vector3 ExactPosition =>
            state != null ? new Vector3(state.posX, def.Altitude, state.posZ) : base.ExactPosition;

        /// <summary>
        /// One engine tick batch. <paramref name="delta"/> is the number of game ticks since the last
        /// call (1 in view, up to 15 offscreen), so every tick is simulated separately: turning and
        /// collision sampling never get skipped just because the missile is off camera.
        /// </summary>
        protected override void TickInterval(int delta)
        {
            if (settled)
            {
                return;
            }

            if (state == null)
            {
                ArcaneMissileDiagnostics.Error(
                    "arcane missile projectile loaded without flight state; removing it instead of flying blind.");
                Destroy();
                return;
            }

            for (int i = 0; i < delta; i++)
            {
                ArcaneMissileEnd end = state.Tick(Map);
                if (end == ArcaneMissileEnd.Flying)
                {
                    continue;
                }

                Settle(end);
                return;
            }

            SyncPosition();
            // Sole per-tick visual driver: the map component only gates visibility and rebuilds,
            // so a visual is advanced exactly once per tick and never double-driven.
            TickVisual();
        }

        /// <summary>Resolves the ending exactly once: counters, impact effect, cleanup, destroy.</summary>
        private void Settle(ArcaneMissileEnd end)
        {
            if (settled)
            {
                return;
            }

            settled = true;
            MapComponent_ArcaneMissiles component = Map != null ? Map.GetComponent<MapComponent_ArcaneMissiles>() : null;

            if (end == ArcaneMissileEnd.Impact)
            {
                component?.NotifyImpact();
                PlayImpactVisual();
            }
            else
            {
                component?.NotifyExpired();
            }

            // Destroy() runs the despawn/cleanup chain; our overrides release the visual first.
            Destroy();
        }

        private void SyncPosition()
        {
            var cell = new IntVec3((int)MathF.Floor(state.posX), 0, (int)MathF.Floor(state.posZ));
            if (cell != Position && GenGrid.InBounds(cell, Map))
            {
                Position = cell;
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            ReleaseVisual("destroy");
            base.Destroy(mode);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            ReleaseVisual("despawn");
            base.DeSpawn(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref state, "arcaneMissile");
        }

        // ---------------------------------------------------------------- visuals

        /// <summary>Called by the map component when this map stops being the current one.</summary>
        internal void SuspendVisual()
        {
            visualsSuspended = true;
            ReleaseVisual("map hidden");
        }

        /// <summary>Called by the map component when the map is current again (or right after load).</summary>
        internal void ResumeVisual()
        {
            visualsSuspended = false;
            EnsureVisual();
        }

        internal void TickVisual()
        {
            if (visualsSuspended)
            {
                return;
            }

            EnsureVisual();
            if (visual == null)
            {
                return;
            }

            try
            {
                visual.Update(Frame());
            }
            catch (Exception exception)
            {
                ArcaneMissileDiagnostics.Error("visual failed to update; disabled for this missile: " + exception);
                ReleaseVisual("update failure");
                visualBroken = true;
            }
        }

        internal void ReleaseVisual(string reason)
        {
            if (visual == null)
            {
                return;
            }

            ArcaneMissileVisualSource current = visual;
            visual = null;
            TryDispose(current, reason);
        }

        internal ArcaneMissileVisualFrame Frame()
        {
            return new ArcaneMissileVisualFrame
            {
                Map = Map,
                X = state.posX,
                Y = state.settings.flightHeight,
                Z = state.posZ,
                HeadingX = state.headingX,
                HeadingZ = state.headingZ,
                AgeTicks = state.ageTicks,
                WobblePhase = state.wobblePhase,
                Settings = state.settings
            };
        }

        internal ArcaneMissileLabFrame ToLabFrame()
        {
            UnityEngine.Vector3 target = state.TargetPosition();
            return new ArcaneMissileLabFrame
            {
                X = state.posX,
                Z = state.posZ,
                HeadingX = state.headingX,
                HeadingZ = state.headingZ,
                TargetX = target.x,
                TargetZ = target.z,
                AgeTicks = state.ageTicks,
                TargetThingId = state.targetThing != null ? state.targetThing.thingIDNumber : 0,
                TargetCell = state.targetCell,
                Settings = state.settings.Copy()
            };
        }

        private void EnsureVisual()
        {
            if (visualsSuspended || visual != null || visualBroken || state == null || state.settings == null)
            {
                return;
            }

            ArcaneMissileVisualDef def = state.settings.visualDef;
            if (def == null)
            {
                visualBroken = true;
                ArcaneMissileDiagnostics.Error(
                    "missile has no visualDef (saved selection could not be resolved); it keeps flying without a visual.");
                return;
            }

            ArcaneMissileVisualSource source;
            try
            {
                source = def.CreateSource();
            }
            catch (Exception exception)
            {
                visualBroken = true;
                ArcaneMissileDiagnostics.Error("visual " + def.defName + " failed to spawn; disabled for this missile: " + exception);
                return;
            }

            if (source == null)
            {
                visualBroken = true;
                return;
            }

            try
            {
                source.Spawn(Frame());
                visual = source;
            }
            catch (Exception exception)
            {
                visualBroken = true;
                ArcaneMissileDiagnostics.Error("visual " + def.defName + " failed to spawn; disabled for this missile: " + exception);
                TryDispose(source, "spawn failure");
            }
        }

        private void PlayImpactVisual()
        {
            if (visualsSuspended || visual == null || state == null || state.settings == null)
            {
                return;
            }

            var context = new ArcaneMissileImpactVisualContext
            {
                Map = Map,
                Cell = state.lastImpactCell,
                X = state.posX,
                Y = state.settings.flightHeight,
                Z = state.posZ,
                Hit = state.lastHit,
                Settings = state.settings
            };

            try
            {
                visual.Impact(context);
            }
            catch (Exception exception)
            {
                ArcaneMissileDiagnostics.Error("impact visual failed: " + exception);
            }
        }

        private static void TryDispose(ArcaneMissileVisualSource source, string reason)
        {
            try
            {
                source?.Dispose();
            }
            catch (Exception exception)
            {
                // A failing backend must never block missile removal or map cleanup.
                ArcaneMissileDiagnostics.Error("visual dispose failed (" + reason + "), continuing: " + exception);
            }
        }
    }
}
