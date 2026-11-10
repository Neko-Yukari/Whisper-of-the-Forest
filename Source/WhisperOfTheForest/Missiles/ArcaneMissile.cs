using System;
using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Missiles
{
    /// <summary>How a missile left the simulation. Used by the map component for counters and visuals.</summary>
    internal enum ArcaneMissileEnd
    {
        Flying,
        Impact,
        Expired
    }

    /// <summary>
    /// One homing projectile. Gameplay state lives here and is scribed with the map; presentation is
    /// owned by the visual layer. Movement runs on game ticks, not frames, and every step is
    /// sub-sampled so a fast missile cannot skip a wall or a pawn.
    /// </summary>
    internal sealed class ArcaneMissile : IExposable
    {
        /// <summary>Longest distance advanced between two collision samples, in cells.</summary>
        private const float MaxSubstepCells = 0.18f;

        /// <summary>Approximate body radius added to the configured hit radius for pawns.</summary>
        private const float PawnBodyRadius = 0.3f;

        /// <summary>The caster cannot be hit while the volley is still leaving their cell.</summary>
        private const int CasterIgnoreTicks = 12;

        public Thing caster;

        /// <summary>Live tracking target; becomes null when it dies or leaves the map.</summary>
        public Thing targetThing;

        /// <summary>Cell the missile is currently steering to (follows a living target).</summary>
        public IntVec3 targetCell;

        /// <summary>Target chosen at launch, reported to hit strategies even after it is gone.</summary>
        public Thing originalTarget;

        /// <summary>Target cell chosen at launch.</summary>
        public IntVec3 originalTargetCell;

        public float originX;
        public float originZ;
        public float posX;
        public float posZ;
        public float headingX;
        public float headingZ;

        public int ageTicks;
        public int maxAgeTicks;
        public int randomSeed;
        public float wobblePhase;

        public ArcaneMissileSettings settings;

        /// <summary>Set exactly once, when this missile detonates.</summary>
        public IntVec3 lastImpactCell;

        /// <summary>Thing struck by the detonation; null for a ground impact.</summary>
        public Thing lastHit;

        public void Initialize(
            Thing caster,
            Thing targetThing,
            IntVec3 targetCell,
            float startX,
            float startZ,
            float dirX,
            float dirZ,
            ArcaneMissileSettings settings,
            int seed)
        {
            this.caster = caster;
            this.targetThing = targetThing;
            this.targetCell = targetCell;
            originalTarget = targetThing;
            originalTargetCell = targetCell;

            originX = startX;
            originZ = startZ;
            posX = startX;
            posZ = startZ;

            float length = MathF.Sqrt(dirX * dirX + dirZ * dirZ);
            if (length < 0.0001f)
            {
                dirX = 0f;
                dirZ = 1f;
            }
            else
            {
                dirX /= length;
                dirZ /= length;
            }

            headingX = dirX;
            headingZ = dirZ;
            this.settings = settings;
            ageTicks = 0;
            maxAgeTicks = Math.Max(1, (int)(settings.lifetimeSeconds * GenTicks.TicksPerRealSecond));

            randomSeed = seed;
            var random = new Random(seed);
            wobblePhase = (float)(random.NextDouble() * Math.PI * 2.0);
        }

        /// <summary>Advances one game tick and reports whether the missile is still flying.</summary>
        public ArcaneMissileEnd Tick(Map map)
        {
            if (map == null || settings == null)
            {
                return ArcaneMissileEnd.Expired;
            }

            if (settings.expireOnTargetLost && !HasLiveTarget(map))
            {
                ExperimentLog.Record("missiles", caster, "expired reason=targetLost age=" + ageTicks);
                return ArcaneMissileEnd.Expired;
            }

            ageTicks++;
            if (ageTicks > maxAgeTicks)
            {
                ExperimentLog.Record("missiles", caster, "expired reason=lifetime age=" + ageTicks);
                return ArcaneMissileEnd.Expired;
            }

            RefreshTarget(map);

            UnityEngine.Vector3 target = TargetPosition();
            float toTargetX = target.x - posX;
            float toTargetZ = target.z - posZ;
            float distance = MathF.Sqrt(toTargetX * toTargetX + toTargetZ * toTargetZ);

            float arrivalRadius = MathF.Max(0.35f, settings.hitRadius);
            if (distance <= arrivalRadius)
            {
                Impact(map, CellAt(posX, posZ), targetThing);
                return ArcaneMissileEnd.Impact;
            }

            Steer(toTargetX, toTargetZ);

            float cellsPerTick = settings.speedCellsPerSecond / GenTicks.TicksPerRealSecond;
            int substeps = Math.Max(1, (int)MathF.Ceiling(cellsPerTick / MaxSubstepCells));
            float stepX = headingX * cellsPerTick / substeps;
            float stepZ = headingZ * cellsPerTick / substeps;

            for (int i = 0; i < substeps; i++)
            {
                posX += stepX;
                posZ += stepZ;

                IntVec3 cell = CellAt(posX, posZ);
                if (!InBounds(map, cell))
                {
                    ExperimentLog.Record("missiles", caster, "expired reason=outOfBounds cell=" + cell);
                    return ArcaneMissileEnd.Expired;
                }

                // Walls and pawns win over arrival, so a fast dart can never reach past a wall.
                Thing hit = FindHit(map, cell);
                if (hit != null)
                {
                    Impact(map, cell, hit);
                    return ArcaneMissileEnd.Impact;
                }

                // Arrival must be sampled between substeps too: at 60 cells/s a tick moves a full
                // cell and a tick-boundary-only check can straddle a 0.35 cell arrival radius.
                float remainingX = target.x - posX;
                float remainingZ = target.z - posZ;
                if (MathF.Sqrt(remainingX * remainingX + remainingZ * remainingZ) <= arrivalRadius)
                {
                    Impact(map, cell, targetThing);
                    return ArcaneMissileEnd.Impact;
                }
            }

            float travelledX = posX - originX;
            float travelledZ = posZ - originZ;
            if (MathF.Sqrt(travelledX * travelledX + travelledZ * travelledZ) > settings.maxRangeCells)
            {
                ExperimentLog.Record("missiles", caster, "expired reason=range cells=" + MathF.Round(settings.maxRangeCells));
                return ArcaneMissileEnd.Expired;
            }

            return ArcaneMissileEnd.Flying;
        }

        /// <summary>True only while the launch target is spawned, alive and still on this map.</summary>
        private bool HasLiveTarget(Map map)
        {
            if (targetThing == null || targetThing.Destroyed || !targetThing.Spawned || targetThing.Map != map)
            {
                return false;
            }

            // A dead pawn can stay spawned for a while before the corpse/despawn pass runs.
            return !(targetThing is Pawn pawn && pawn.Dead);
        }

        private void RefreshTarget(Map map)
        {
            if (targetThing == null)
            {
                return;
            }

            if (targetThing.Destroyed || !targetThing.Spawned || targetThing.Map != map)
            {
                // Keep flying to the last known cell instead of teleporting or stalling.
                targetThing = null;
                return;
            }

            targetCell = targetThing.Position;
        }

        /// <summary>Current aim point: the live thing's draw position, else the last known cell.</summary>
        internal UnityEngine.Vector3 TargetPosition()
        {
            if (targetThing != null && targetThing.Spawned)
            {
                return targetThing.DrawPos;
            }

            return targetCell.ToVector3Shifted();
        }

        private void Steer(float toTargetX, float toTargetZ)
        {
            float desiredAngle = MathF.Atan2(toTargetX, toTargetZ);
            float currentAngle = MathF.Atan2(headingX, headingZ);
            float maxStep = settings.maxTurnDegreesPerSecond * (MathF.PI / 180f) / GenTicks.TicksPerRealSecond;

            float delta = WrapAngle(desiredAngle - currentAngle);
            if (delta > maxStep)
            {
                delta = maxStep;
            }
            else if (delta < -maxStep)
            {
                delta = -maxStep;
            }

            float newAngle = currentAngle + delta;
            headingX = MathF.Sin(newAngle);
            headingZ = MathF.Cos(newAngle);
        }

        private Thing FindHit(Map map, IntVec3 cell)
        {
            List<Thing> things = map.thingGrid.ThingsListAt(cell);

            for (int i = 0; i < things.Count; i++)
            {
                if (!(things[i] is Pawn pawn) || pawn.Dead)
                {
                    continue;
                }

                if (pawn == caster && ageTicks <= CasterIgnoreTicks)
                {
                    continue;
                }

                UnityEngine.Vector3 drawPos = pawn.DrawPos;
                float dx = drawPos.x - posX;
                float dz = drawPos.z - posZ;
                if (MathF.Sqrt(dx * dx + dz * dz) <= settings.hitRadius + PawnBodyRadius)
                {
                    return pawn;
                }
            }

            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing == caster || thing is Pawn)
                {
                    continue;
                }

                if (thing.def.Fillage == FillCategory.Full)
                {
                    return thing;
                }
            }

            if (targetThing != null && targetThing.Spawned && targetThing.Map == map && targetThing.Position == cell
                && !(targetThing is Pawn))
            {
                return targetThing;
            }

            return null;
        }

        /// <summary>
        /// Resolves the hit exactly once through the volley's payload. A throwing payload is logged
        /// and swallowed: the missile still ends, so damage can never repeat on later ticks.
        /// </summary>
        private void Impact(Map map, IntVec3 cell, Thing hit)
        {
            lastImpactCell = cell;
            lastHit = hit;

            ArcaneMissileSettings current = settings;
            IArcaneMissilePayload payload = current?.payload;

            if (payload == null)
            {
                ArcaneMissileDiagnostics.Error("missile hit with no payload; no effect applied.");
            }
            else
            {
                var context = new ArcaneMissileImpactContext
                {
                    Map = map,
                    Caster = caster,
                    Hit = hit,
                    OriginalTarget = originalTarget,
                    OriginalTargetCell = originalTargetCell,
                    ImpactCell = cell,
                    ImpactX = posX,
                    ImpactZ = posZ,
                    HeadingX = headingX,
                    HeadingZ = headingZ,
                    Settings = current
                };

                try
                {
                    payload.Resolve(context);
                }
                catch (Exception exception)
                {
                    ArcaneMissileDiagnostics.Error(
                        "payload " + payload.GetType().Name + " threw; the missile still ends: " + exception);
                }
            }

            string targetLabel = hit != null ? hit.LabelShort : "ground";
            ExperimentLog.Record(
                "missiles",
                caster,
                "impact payload=" + (payload != null ? payload.GetType().Name : "none")
                + " target=" + targetLabel + " cell=" + cell + " age=" + ageTicks);
        }

        private static float WrapAngle(float radians)
        {
            while (radians > MathF.PI)
            {
                radians -= MathF.PI * 2f;
            }

            while (radians < -MathF.PI)
            {
                radians += MathF.PI * 2f;
            }

            return radians;
        }

        private static IntVec3 CellAt(float x, float z)
        {
            return new IntVec3((int)MathF.Floor(x), 0, (int)MathF.Floor(z));
        }

        private static bool InBounds(Map map, IntVec3 cell)
        {
            return cell.x >= 0 && cell.z >= 0 && cell.x < map.Size.x && cell.z < map.Size.z;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref targetThing, "targetThing");
            Scribe_References.Look(ref originalTarget, "originalTarget");
            Scribe_Values.Look(ref targetCell, "targetCell");
            Scribe_Values.Look(ref originalTargetCell, "originalTargetCell");
            Scribe_Values.Look(ref originX, "originX");
            Scribe_Values.Look(ref originZ, "originZ");
            Scribe_Values.Look(ref posX, "posX");
            Scribe_Values.Look(ref posZ, "posZ");
            Scribe_Values.Look(ref headingX, "headingX");
            Scribe_Values.Look(ref headingZ, "headingZ");
            Scribe_Values.Look(ref ageTicks, "ageTicks");
            Scribe_Values.Look(ref maxAgeTicks, "maxAgeTicks");
            Scribe_Values.Look(ref randomSeed, "randomSeed");
            Scribe_Values.Look(ref wobblePhase, "wobblePhase");
            Scribe_Deep.Look(ref settings, "settings");
        }
    }
}
