using System;
using Verse;
using WhisperOfTheForest.Experimental.Vfx;

namespace WhisperOfTheForest.Experimental.Missiles
{
    /// <summary>
    /// Public entry point for firing arcane missiles. The psycast comp calls this, and the lab UI
    /// or any other feature can use the same API without replicating launch math:
    /// <code>
    /// ArcaneMissileLauncher.FireAtCell(pawn, cell);
    /// ArcaneMissileLauncher.Fire(pawn, new LocalTargetInfo(thing), mySettings);
    /// </code>
    /// All calls must happen on the game's main thread while the caster is spawned on a map.
    /// </summary>
    public static class ArcaneMissileLauncher
    {
        /// <summary>Launch distance in front of the caster, so missiles do not spawn inside them.</summary>
        private const float SpawnOffsetCells = 0.55f;

        public static ArcaneMissileSettings CreateDefaultSettings()
        {
            return ArcaneMissileSettings.CreateDefault();
        }

        /// <summary>
        /// Fires one volley at a thing or a cell. Returns the number of missiles actually launched.
        /// A null settings argument uses the built-in defaults (3 missiles, damage payload).
        /// </summary>
        public static int Fire(Pawn caster, LocalTargetInfo target, ArcaneMissileSettings settings = null)
        {
            if (caster == null || !caster.Spawned || caster.Map == null || !target.IsValid)
            {
                return 0;
            }

            Map map = caster.Map;
            ArcaneMissileSettings volley = (settings ?? ArcaneMissileSettings.CreateDefault()).Copy();
            volley.Clamp();
            ResolveSelections(volley);

            UnityEngine.Vector3 launch = caster.DrawPos;
            bool targetIsThing = target.HasThing && !target.ThingDestroyed && target.Thing.Spawned && target.Thing.Map == map;
            Thing targetThing = targetIsThing ? target.Thing : null;
            bool targetDead = targetThing is Pawn targetPawn && targetPawn.Dead;
            if (volley.expireOnTargetLost && (targetThing == null || targetDead))
            {
                ArcaneMissileDiagnostics.Error(
                    "launch rejected: expireOnTargetLost requires a live Thing target on the caster's map.");
                return 0;
            }

            IntVec3 targetCell = targetIsThing ? target.Thing.Position : target.Cell;
            UnityEngine.Vector3 targetPosition = targetIsThing ? target.Thing.DrawPos : targetCell.ToVector3Shifted();

            float directionX = targetPosition.x - launch.x;
            float directionZ = targetPosition.z - launch.z;
            float length = MathF.Sqrt(directionX * directionX + directionZ * directionZ);
            if (length < 0.0001f)
            {
                directionX = 0f;
                directionZ = 1f;
            }
            else
            {
                directionX /= length;
                directionZ /= length;
            }

            float baseAngle = MathF.Atan2(directionX, directionZ);
            float startX = launch.x + directionX * SpawnOffsetCells;
            float startZ = launch.z + directionZ * SpawnOffsetCells;
            float spreadRadians = volley.launchSpreadDegrees * (MathF.PI / 180f);

            ThingDef projectileDef = ArcaneMissileDefs.ProjectileThingDef;
            if (projectileDef == null)
            {
                ArcaneMissileDiagnostics.Error(
                    "projectile thing def " + ArcaneMissileDefs.ProjectileThingDefName
                    + " is missing; the volley was not fired.");
                return 0;
            }

            MapComponent_ArcaneMissiles component = MapComponent_ArcaneMissiles.GetOrCreate(map);
            int launched = 0;

            for (int i = 0; i < volley.missileCount; i++)
            {
                if (!component.CanAcceptMore())
                {
                    ExperimentLog.Record(
                        "missiles",
                        caster,
                        "dropped volley: missile cap " + MapComponent_ArcaneMissiles.MaxLiveMissiles + " reached");
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
                    startX,
                    startZ,
                    MathF.Sin(angle),
                    MathF.Cos(angle),
                    volley,
                    Gen.HashCombineInt(Find.TickManager.TicksGame, i * 397 + caster.thingIDNumber));

                var spawnCell = new IntVec3((int)MathF.Floor(startX), 0, (int)MathF.Floor(startZ));
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

            string targetLabel = targetIsThing ? targetThing.LabelShort : targetCell.ToString();
            ExperimentLog.Record(
                "missiles",
                caster,
                "volley launched=" + launched + "/" + volley.missileCount
                + " impact=" + (volley.impactDef != null ? volley.impactDef.defName : "none")
                + " visual=" + (volley.visualDef != null ? volley.visualDef.defName : "none")
                + " target=" + targetLabel);

            return launched;
        }

        public static int FireAtCell(Pawn caster, IntVec3 cell, ArcaneMissileSettings settings = null)
        {
            return Fire(caster, new LocalTargetInfo(cell), settings);
        }

        public static int FireAtThing(Pawn caster, Thing target, ArcaneMissileSettings settings = null)
        {
            return target == null ? 0 : Fire(caster, new LocalTargetInfo(target), settings);
        }

        /// <summary>
        /// Fires one segment of a longer spell from an explicit point and direction. Used by the
        /// trigger hand-over, where the origin is just behind the surface the previous dart hit and
        /// the direction is that surface's normal.
        ///
        /// <paramref name="segmentIndex"/> is the pointer the new darts carry, so the segment they
        /// must run when they hit is the one that was just launched. Only the origin and aim differ
        /// from <see cref="Fire"/>: the same spread, clamps and map missile cap apply, and a null
        /// <paramref name="targetThing"/> aims at the cell so a dart can never home back onto the
        /// thing that was already hit. Returns the number of missiles actually launched.
        /// </summary>
        public static int FireSpellSegment(
            Pawn caster,
            ArcaneMissileSettings settings,
            int segmentIndex,
            UnityEngine.Vector3 origin,
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
                ArcaneMissileDiagnostics.Error(
                    "segment " + segmentIndex + " launch rejected: target cell " + targetCell + " is outside the map.");
                return 0;
            }

            if (targetThing != null
                && (targetThing.Destroyed || !targetThing.Spawned || targetThing.Map != map))
            {
                targetThing = null;
            }

            ArcaneMissileSettings volley = (settings ?? ArcaneMissileSettings.CreateDefault()).Copy();
            volley.Clamp();
            ResolveSelections(volley);
            volley.spellSegmentIndex = segmentIndex;

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

            ThingDef projectileDef = ArcaneMissileDefs.ProjectileThingDef;
            if (projectileDef == null)
            {
                ArcaneMissileDiagnostics.Error(
                    "projectile thing def " + ArcaneMissileDefs.ProjectileThingDefName
                    + " is missing; the segment was not fired.");
                return 0;
            }

            float baseAngle = MathF.Atan2(dirX, dirZ);
            float startX = origin.x;
            float startZ = origin.z;
            float spreadRadians = volley.launchSpreadDegrees * (MathF.PI / 180f);
            LocalTargetInfo target = targetThing != null
                ? new LocalTargetInfo(targetThing)
                : new LocalTargetInfo(targetCell);

            MapComponent_ArcaneMissiles component = MapComponent_ArcaneMissiles.GetOrCreate(map);
            int launched = 0;

            for (int i = 0; i < volley.missileCount; i++)
            {
                if (!component.CanAcceptMore())
                {
                    ExperimentLog.Record(
                        "missiles",
                        caster,
                        "dropped segment volley: missile cap " + MapComponent_ArcaneMissiles.MaxLiveMissiles + " reached");
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
                    startX,
                    startZ,
                    MathF.Sin(angle),
                    MathF.Cos(angle),
                    volley,
                    Gen.HashCombineInt(Find.TickManager.TicksGame, i * 397 + caster.thingIDNumber));

                var spawnCell = new IntVec3((int)MathF.Floor(startX), 0, (int)MathF.Floor(startZ));
                if (!GenGrid.InBounds(spawnCell, map))
                {
                    ExperimentLog.Record("missiles", caster, "dropped missile: segment spawn cell out of bounds " + spawnCell);
                    continue;
                }

                var projectile = (Projectile_ArcaneMissiles)ThingMaker.MakeThing(projectileDef);
                projectile.Initialize(missile, caster, target, target);
                GenSpawn.Spawn(projectile, spawnCell, map, WipeMode.Vanish);
                component.NotifyLaunched();
                launched++;
            }

            ExperimentLog.Record(
                "missiles",
                caster,
                "segment volley index=" + segmentIndex
                + " launched=" + launched + "/" + volley.missileCount
                + " target=" + (targetThing != null ? targetThing.LabelShort : targetCell.ToString()));

            return launched;
        }

        /// <summary>
        /// Fills in the documented selections when the caller left them null: RimUnity for visuals,
        /// and the Def matching <see cref="ArcaneMissileImpactMode"/> for the hit strategy.
        /// Explicit but broken defs are never replaced - they fail loudly in their Def/worker.
        /// </summary>
        private static void ResolveSelections(ArcaneMissileSettings volley)
        {
            if (volley.visualDef == null)
            {
                volley.visualDef = ArcaneMissileDefs.DefaultVisual;
                if (volley.visualDef == null)
                {
                    ArcaneMissileDiagnostics.Error(
                        "default visual def " + ArcaneMissileDefs.RimUnityVisualDefName
                        + " is missing; missiles will fly without a visual.");
                }
            }

            if (volley.impactDef == null)
            {
                volley.impactDef = ArcaneMissileDefs.ImpactForMode(volley.impactMode);
                if (volley.impactDef == null)
                {
                    ArcaneMissileDiagnostics.Error(
                        "impact def " + ArcaneMissileDefs.ImpactDefNameFor(volley.impactMode)
                        + " is missing; missiles will detonate without an effect.");
                }
            }
        }

        /// <summary>
        /// Debug helper for in-game verification of a visual backend: plays one probe burst at a
        /// cell with no gameplay effect. Uses the default (RimUnity) backend unless another Def is
        /// given, and keeps the effect alive for <paramref name="seconds"/>. Returns false only when
        /// the point is unusable.
        /// </summary>
        public static bool SpawnVisualProbe(
            Map map,
            IntVec3 cell,
            float seconds = 3f,
            string colorHex = null,
            ArcaneMissileVisualDef visualDef = null)
        {
            if (map == null || !cell.IsValid || !GenGrid.InBounds(cell, map))
            {
                return false;
            }

            ArcaneMissileVisualDef def = visualDef ?? ArcaneMissileDefs.DefaultVisual;
            if (def == null)
            {
                ArcaneMissileDiagnostics.Error("visual probe has no visual def.");
                return false;
            }

            ArcaneMissileSettings settings = ArcaneMissileSettings.CreateDefault();
            if (!string.IsNullOrEmpty(colorHex))
            {
                settings.missileColorHex = colorHex;
            }

            VfxService.Play(new ArcaneMissileVfxRequest
            {
                map = map,
                origin = new UnityEngine.Vector3(cell.x + 0.5f, settings.flightHeight, cell.z + 0.5f),
                direction = UnityEngine.Vector3.forward,
                vfxDefName = def.impactVisualDefName,
                settings = settings,
                seconds = Math.Max(0.5f, seconds)
            });
            return true;
        }
    }
}
