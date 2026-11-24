using System;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Vfx;

namespace WhisperOfTheForest.Missiles
{
    /// <summary>
    /// Native carrier for one arcane missile. <see cref="Verse.Projectile"/> and the game's thing
    /// pipeline own spawning, registration, ticking (including the 1-visible / 15-offscreen update
    /// rate), saving and destruction; this class only supplies what the vanilla straight-line
    /// projectile cannot: a homing step delegated to <see cref="ArcaneMissile"/> and the payload
    /// dispatch already resolved by that model.
    ///
    /// Nothing visual lives here. The projectile only asks the Vfx layer for a follow effect when it
    /// spawns (including right after a load, from <see cref="SpawnSetup"/>) and for a burst at the
    /// impact point; the Vfx Def decides what either one looks like.
    ///
    /// <see cref="TickInterval"/> deliberately does not call the vanilla implementation: the base
    /// class would move the missile along its launch line, run its lifetime countdown and resolve
    /// hits through the private intercept checks, which would double-drive the same projectile.
    /// </summary>
    public class Projectile_ArcaneMissiles : Projectile
    {
        /// <summary>Gameplay state. Serialized with the projectile; own copy, never a Def.</summary>
        private ArcaneMissile state;

        /// <summary>Set once the trail was handed to the Vfx layer; not scribed, so a load asks again.</summary>
        private bool visualRequested;

        /// <summary>Set once when the payload/expiry was resolved; keeps settlement single.</summary>
        private bool settled;

        internal ArcaneMissile State => state;

        /// <summary>
        /// Attaches the already-initialised flight model and fills the native projectile slots other
        /// code may read. The vanilla launch countdown is pinned to zero because this projectile
        /// steers itself: nothing may treat it as a straight dart in flight.
        /// </summary>
        internal void Initialize(ArcaneMissile missile, Thing caster, LocalTargetInfo firedAt, LocalTargetInfo aimedAt)
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
        /// Runs on every spawn, including the one the save loader performs, so a missile restored
        /// mid-flight gets its trail back even while the game is paused.
        /// </summary>
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            RequestTrailVisual();
        }

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
        }

        /// <summary>Resolves the ending exactly once: impact effect, cleanup, destroy.</summary>
        private void Settle(ArcaneMissileEnd end)
        {
            if (settled)
            {
                return;
            }

            settled = true;

            if (end == ArcaneMissileEnd.Impact || end == ArcaneMissileEnd.Intercepted)
            {
                try
                {
                    RequestImpactVisual();
                }
                finally
                {
                    // The effect is fire-and-forget, but the missile must leave the map even if the
                    // visual Def is broken and throws: the exception still propagates.
                    Destroy();
                }

                return;
            }

            // The trail effect is a follow effect: the Vfx component drops it once this thing is gone.
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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref state, "arcaneMissile");
        }

        /// <summary>
        /// Asks for the trail of the volley's selected backend. A save can outlive a Def (the
        /// selection is a scribed Def reference), and that is the one case a visual cannot rebuild:
        /// it is reported and the missile keeps flying without one.
        /// </summary>
        private void RequestTrailVisual()
        {
            if (visualRequested || state?.settings == null)
            {
                return;
            }

            visualRequested = true;
            ArcaneMissileVisualDef visualDef = state.settings.visualDef;
            if (visualDef == null)
            {
                ArcaneMissileDiagnostics.Error(
                    "missile has no visualDef (saved selection could not be resolved); it keeps flying without a visual.");
                return;
            }

            VfxService.Play(new ArcaneMissileVfxRequest
            {
                map = Map,
                follow = this,
                origin = ExactPosition,
                target = state.TargetPosition(),
                direction = new Vector3(state.headingX, 0f, state.headingZ),
                vfxDefName = visualDef.defName,
                settings = state.settings
            });
        }

        private void RequestImpactVisual()
        {
            ArcaneMissileVisualDef visualDef = state.settings.visualDef;
            if (visualDef == null)
            {
                return;
            }

            ArcaneMissileVfxPosition.Of(state, out float vx, out float vy, out float vz);
            VfxService.Play(new ArcaneMissileVfxRequest
            {
                map = Map,
                origin = new Vector3(vx, vy, vz),
                target = state.TargetPosition(),
                direction = new Vector3(state.headingX, 0f, state.headingZ),
                vfxDefName = visualDef.impactVisualDefName,
                settings = state.settings
            });
        }
    }
}
