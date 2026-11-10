using System;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Missiles
{
    /// <summary>
    /// The entry point for firing arcane missiles. The caller always passes settings that came from a
    /// Def block (checked at load) with its payload set; the launcher copies them per volley and
    /// never corrects a value. All calls must happen on the game's main thread while the caster is
    /// spawned on a map.
    /// </summary>
    public static class ArcaneMissileLauncher
    {
        /// <summary>Launch distance in front of the caster, so missiles do not spawn inside them.</summary>
        private const float SpawnOffsetCells = 0.55f;

        /// <summary>
        /// Fires one volley from the caster at a thing or a cell. Returns the number of missiles
        /// actually launched.
        /// </summary>
        public static int Fire(Thing caster, LocalTargetInfo target, ArcaneMissileSettings settings)
        {
            if (caster == null || !caster.Spawned || caster.Map == null || !target.IsValid)
            {
                return 0;
            }

            Map map = caster.Map;
            Vector3 launch = caster.DrawPos;
            bool targetIsThing = target.HasThing && !target.ThingDestroyed && target.Thing.Spawned && target.Thing.Map == map;
            Thing targetThing = targetIsThing ? target.Thing : null;
            bool targetDead = targetThing is Pawn targetPawn && targetPawn.Dead;
            if (settings.expireOnTargetLost && (targetThing == null || targetDead))
            {
                ArcaneMissileDiagnostics.Error(
                    "launch rejected: expireOnTargetLost requires a live Thing target on the caster's map.");
                return 0;
            }

            IntVec3 targetCell = targetIsThing ? target.Thing.Position : target.Cell;
            Vector3 targetPosition = targetIsThing ? target.Thing.DrawPos : targetCell.ToVector3Shifted();
            Normalize(targetPosition.x - launch.x, targetPosition.z - launch.z, out float dirX, out float dirZ);

            var origin = new Vector3(launch.x + dirX * SpawnOffsetCells, 0f, launch.z + dirZ * SpawnOffsetCells);
            return Launch(caster, settings, origin, dirX, dirZ, targetCell, targetThing, target, "volley");
        }

        /// <summary>
        /// Fires a volley from an explicit point and direction: a pierced dart leaves from just behind
        /// the thing it hit. A null <paramref name="targetThing"/> aims at the cell, so a dart can
        /// never home back onto the thing that was already hit. Returns the number launched.
        /// </summary>
        public static int FireFrom(
            Thing caster,
            ArcaneMissileSettings settings,
            Vector3 origin,
            float dirX,
            float dirZ,
            IntVec3 targetCell,
            Thing targetThing)
        {
            if (caster == null || !caster.Spawned || caster.Map == null)
            {
                return 0;
            }

            Map map = caster.Map;
            if (!GenGrid.InBounds(targetCell, map))
            {
                ArcaneMissileDiagnostics.Error("launch rejected: target cell " + targetCell + " is outside the map.");
                return 0;
            }

            if (targetThing != null
                && (targetThing.Destroyed || !targetThing.Spawned || targetThing.Map != map))
            {
                targetThing = null;
            }

            Normalize(dirX, dirZ, out dirX, out dirZ);
            LocalTargetInfo target = targetThing != null
                ? new LocalTargetInfo(targetThing)
                : new LocalTargetInfo(targetCell);
            return Launch(caster, settings, origin, dirX, dirZ, targetCell, targetThing, target, "relaunch");
        }

        private static int Launch(
            Thing caster,
            ArcaneMissileSettings settings,
            Vector3 origin,
            float dirX,
            float dirZ,
            IntVec3 targetCell,
            Thing targetThing,
            LocalTargetInfo target,
            string label)
        {
            Map map = caster.Map;
            ThingDef projectileDef = ArcaneMissileDefs.ProjectileThingDef;
            if (projectileDef == null)
            {
                ArcaneMissileDiagnostics.Error(
                    "projectile thing def " + ArcaneMissileDefs.ProjectileThingDefName
                    + " is missing; the " + label + " was not fired.");
                return 0;
            }

            ArcaneMissileSettings volley = settings.Copy();
            float baseAngle = MathF.Atan2(dirX, dirZ);
            float spreadRadians = volley.launchSpreadDegrees * (MathF.PI / 180f);
            MapComponent_ArcaneMissiles component = MapComponent_ArcaneMissiles.GetOrCreate(map);
            int launched = 0;

            for (int i = 0; i < volley.missileCount; i++)
            {
                if (!component.CanAcceptMore())
                {
                    ExperimentLog.Record(
                        "missiles",
                        caster,
                        "dropped " + label + ": missile cap " + MapComponent_ArcaneMissiles.MaxLiveMissiles + " reached");
                    break;
                }

                float offset = volley.missileCount <= 1
                    ? 0f
                    : (i - (volley.missileCount - 1) * 0.5f) / (volley.missileCount - 1) * spreadRadians;

                float angle = baseAngle + offset;
                var missile = new ArcaneMissile();
                missile.Initialize(
                    caster,
                    targetThing,
                    targetCell,
                    origin.x,
                    origin.z,
                    MathF.Sin(angle),
                    MathF.Cos(angle),
                    volley,
                    Gen.HashCombineInt(Find.TickManager.TicksGame, i * 397 + caster.thingIDNumber));

                var spawnCell = new IntVec3((int)MathF.Floor(origin.x), 0, (int)MathF.Floor(origin.z));
                if (!GenGrid.InBounds(spawnCell, map))
                {
                    ExperimentLog.Record("missiles", caster, "dropped missile: launch cell out of bounds " + spawnCell);
                    continue;
                }

                // The game owns the projectile from here: Spawn registers it in the map's thing
                // registry, the thing ticker calls TickInterval, and the save serialises the thing.
                var projectile = (Projectile_ArcaneMissiles)ThingMaker.MakeThing(projectileDef);
                projectile.Initialize(missile, caster, target, target);
                GenSpawn.Spawn(projectile, spawnCell, map, WipeMode.Vanish);
                component.NotifyLaunched();
                launched++;
            }

            ExperimentLog.Record(
                "missiles",
                caster,
                label + " launched=" + launched + "/" + volley.missileCount
                + " visual=" + volley.visualDef.defName
                + " target=" + (targetThing != null ? targetThing.LabelShort : targetCell.ToString()));

            return launched;
        }

        private static void Normalize(float x, float z, out float dirX, out float dirZ)
        {
            float length = MathF.Sqrt(x * x + z * z);
            if (length < 0.0001f)
            {
                dirX = 0f;
                dirZ = 1f;
                return;
            }

            dirX = x / length;
            dirZ = z / length;
        }
    }
}
