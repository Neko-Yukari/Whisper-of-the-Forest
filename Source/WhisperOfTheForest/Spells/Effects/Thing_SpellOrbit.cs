using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Experimental.Spells
{
    /// <summary>
    /// The Orbit form's projectile: one small Thing that circles its caster and applies its frozen
    /// payload the first time it touches a pawn. Position is recomputed every tick from the caster's
    /// live cell, so walking around keeps the orbit centred on the caster instead of on the place the
    /// spell was cast.
    ///
    /// The orb owns its own copy of the payload, so editing the book after the cast cannot rewrite an
    /// orb that is already circling, and a save restores the orbit with its hit list.
    /// </summary>
    public class Thing_SpellOrbit : Thing
    {
        private Pawn caster;
        private List<SpellPlanStep> payload = new List<SpellPlanStep>();
        private float distance;
        private float angleDegrees;
        private float degreesPerSecond;
        private float hitRadius;
        private int expiresTick;

        /// <summary>Thing ids this orb already touched; one hit per pawn per orb.</summary>
        private List<int> hitThingIds = new List<int>();

        public void Configure(Pawn caster, SpellSegment segment, float startAngleDegrees)
        {
            this.caster = caster;
            payload = SpellExecutor.CopySteps(segment.steps);
            distance = segment.orbitDistance;
            degreesPerSecond = segment.orbitSpeedDegrees;
            hitRadius = segment.orbitHitRadius;
            angleDegrees = startAngleDegrees;
            expiresTick = Find.TickManager.TicksGame + segment.orbitLifetimeTicks;
            PlaceAroundCaster();
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
            TouchTargets();
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

        private void TouchTargets()
        {
            var targets = new List<Pawn>();
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(Position, Map, hitRadius, true))
            {
                if (!(thing is Pawn pawn) || pawn == caster || !SpellTargets.CanTakeDamage(pawn))
                {
                    continue;
                }

                if (hitThingIds.Contains(pawn.thingIDNumber))
                {
                    continue;
                }

                targets.Add(pawn);
            }

            for (int i = 0; i < targets.Count; i++)
            {
                Pawn pawn = targets[i];
                if (pawn.Destroyed || !pawn.Spawned)
                {
                    continue;
                }

                hitThingIds.Add(pawn.thingIDNumber);
                string report = SpellExecutor.ExecuteStepList(payload, caster, pawn, pawn.Position, Map);
                SpellDiagnostics.Message("orbit touched " + pawn.LabelShort + " at " + Position + " " + report);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref caster, "caster");
            Scribe_Collections.Look(ref payload, "payload", LookMode.Deep);
            Scribe_Values.Look(ref distance, "orbitDistance", 0f);
            Scribe_Values.Look(ref angleDegrees, "orbitAngle", 0f);
            Scribe_Values.Look(ref degreesPerSecond, "orbitDegreesPerSecond", 0f);
            Scribe_Values.Look(ref hitRadius, "orbitHitRadius", 0f);
            Scribe_Values.Look(ref expiresTick, "orbitExpiresTick", 0);
            Scribe_Collections.Look(ref hitThingIds, "orbitHitIds", LookMode.Value);

            if (payload == null)
            {
                payload = new List<SpellPlanStep>();
            }

            if (hitThingIds == null)
            {
                hitThingIds = new List<int>();
            }
        }
    }
}
