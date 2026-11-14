using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Vfx;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// The Orbit form's projectile: one small Thing that circles its caster, releases its frozen
    /// payload once on the first pawn it touches and is used up. Position is recomputed every tick
    /// from the caster's live cell, so walking around keeps the orbit centred on the caster instead
    /// of on the place the spell was cast.
    ///
    /// The orb owns its own copy of the payload, so editing the book after the cast cannot rewrite an
    /// orb that is already circling.
    /// </summary>
    public class Thing_SpellOrbit : Thing
    {
        private Pawn caster;
        private List<SpellEffect> payload = new List<SpellEffect>();
        private float distance;
        private float angleDegrees;
        private float degreesPerSecond;
        private float hitRadius;
        private int expiresTick;

        public void Configure(Pawn caster, SpellCastPlan plan, float startAngleDegrees)
        {
            this.caster = caster;
            payload = SpellExecutor.CopySteps(plan.steps);
            distance = plan.orbit.distance;
            degreesPerSecond = plan.orbit.angularSpeedDegrees;
            hitRadius = plan.orbit.hitRadius;
            angleDegrees = startAngleDegrees;
            expiresTick = Find.TickManager.TicksGame + plan.orbit.lifetimeTicks;
            PlaceAroundCaster();
        }

        public override Vector3 DrawPos
        {
            get
            {
                if (caster == null || !caster.Spawned) return base.DrawPos;
                float radians = angleDegrees * Mathf.Deg2Rad;
                return caster.DrawPos + new Vector3(Mathf.Cos(radians) * distance, 0f, Mathf.Sin(radians) * distance);
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            VfxService.Play(new VfxRequest
            {
                map = map, follow = this, origin = DrawPos,
                vfxDefName = "WotF_Vfx_Orbit"
            });
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned)
            {
                return;
            }

            if (caster == null || caster.Destroyed || !caster.Spawned || caster.Map != Map)
            {
                Destroy();
                return;
            }

            if (Find.TickManager.TicksGame >= expiresTick)
            {
                Destroy();
                return;
            }

            angleDegrees = Mathf.Repeat(angleDegrees + degreesPerSecond / GenTicks.TicksPerRealSecond, 360f);
            PlaceAroundCaster();
            ReleaseOnTouch();
        }

        private void PlaceAroundCaster()
        {
            if (caster == null || !caster.Spawned || caster.Map != Map)
            {
                return;
            }

            float radians = angleDegrees * Mathf.Deg2Rad;
            var cell = new IntVec3(
                caster.Position.x + Mathf.RoundToInt(Mathf.Cos(radians) * distance),
                0,
                caster.Position.z + Mathf.RoundToInt(Mathf.Sin(radians) * distance));
            if (GenGrid.InBounds(cell, Map))
            {
                Position = cell;
            }
        }

        /// <summary>Releases the payload on the pawn nearest the orb within hitRadius, then removes the orb.</summary>
        private void ReleaseOnTouch()
        {
            Pawn target = null;
            float nearest = float.MaxValue;
            Vector3 orbPos = DrawPos;
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(Position, Map, hitRadius, true))
            {
                if (!(thing is Pawn pawn) || pawn == caster || !SpellTargets.CanTakeDamage(pawn))
                {
                    continue;
                }

                float distanceSquared = (pawn.DrawPos - orbPos).MagnitudeHorizontalSquared();
                if (distanceSquared < nearest)
                {
                    nearest = distanceSquared;
                    target = pawn;
                }
            }

            if (target == null)
            {
                return;
            }

            string report = SpellExecutor.ExecuteStepList(payload, caster, target, target.Position, Map);
            SpellDiagnostics.Message("orbit released on " + target.LabelShort + " at " + Position + " " + report);
            Destroy();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref caster, "caster");
            Scribe_Collections.Look(ref payload, "payload", LookMode.Deep);
            SpellEffect.CheckLoaded(payload, "Thing_SpellOrbit");
            Scribe_Values.Look(ref distance, "orbitDistance", 0f);
            Scribe_Values.Look(ref angleDegrees, "orbitAngle", 0f);
            Scribe_Values.Look(ref degreesPerSecond, "orbitDegreesPerSecond", 0f);
            Scribe_Values.Look(ref hitRadius, "orbitHitRadius", 0f);
            Scribe_Values.Look(ref expiresTick, "orbitExpiresTick", 0);

            if (payload == null)
            {
                payload = new List<SpellEffect>();
            }
        }
    }
}
