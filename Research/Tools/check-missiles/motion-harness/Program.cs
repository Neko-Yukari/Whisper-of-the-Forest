using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using WhisperOfTheForest.Experimental;
using WhisperOfTheForest.Experimental.Missiles;
using Verse;

/// <summary>
/// Runs the production missile stepper and hit strategies against a shimmed map. Every scenario
/// below executes the real logic files (ArcaneMissile, the impact workers and the Def plumbing);
/// only the Verse/engine types are replaced.
/// </summary>
internal static class Program
{
    private static int failures;

    private static class TestDefs
    {
        public static readonly ArcaneMissileImpactDef Damage = new ArcaneMissileImpactDef
        {
            defName = "WotF_Impact_Damage",
            workerClass = typeof(ArcaneMissileImpactWorker_Damage)
        };

        public static readonly ArcaneMissileImpactDef Heal = new ArcaneMissileImpactDef
        {
            defName = "WotF_Impact_Heal",
            workerClass = typeof(ArcaneMissileImpactWorker_Heal)
        };

        public static readonly ArcaneMissileImpactDef Explode = new ArcaneMissileImpactDef
        {
            defName = "WotF_Impact_Explode",
            workerClass = typeof(ArcaneMissileImpactWorker_Explode)
        };

        public static readonly ArcaneMissileImpactDef Throwing = new ArcaneMissileImpactDef
        {
            defName = "harness_throwing",
            workerClass = typeof(ThrowingWorker)
        };

        public static readonly ArcaneMissileImpactDef Capture = new ArcaneMissileImpactDef
        {
            defName = "harness_capture",
            workerClass = typeof(CapturingWorker)
        };

        public static readonly ArcaneMissileImpactDef Counting = new ArcaneMissileImpactDef
        {
            defName = "harness_counting",
            workerClass = typeof(CountingWorker)
        };

        /// <summary>
        /// The native carrier def the launcher spawns. thingClass points at the harness subclass so
        /// one engine tick can be driven from outside; everything else is the shipped shape.
        /// </summary>
        public static readonly ThingDef Projectile = new ThingDef
        {
            defName = ArcaneMissileDefs.ProjectileThingDefName,
            thingClass = typeof(TestProjectile),
            Altitude = 0f,
            Fillage = FillCategory.None
        };
    }

    /// <summary>Exposes the protected engine tick entry so the harness can advance one tick.</summary>
    private sealed class TestProjectile : Projectile_ArcaneMissiles
    {
        public void Step(int delta)
        {
            TickInterval(delta);
        }
    }

    /// <summary>Counts resolutions so "no effect" cases can be asserted.</summary>
    private sealed class CountingWorker : ArcaneMissileImpactWorker
    {
        public static int Calls;

        public override void Resolve(ArcaneMissileImpactContext context)
        {
            Calls++;
        }
    }

    /// <summary>Records the context so the harness can assert on original target / actual hit.</summary>
    private sealed class CapturingWorker : ArcaneMissileImpactWorker
    {
        public static ArcaneMissileImpactContext Last;

        public override void Resolve(ArcaneMissileImpactContext context)
        {
            Last = context;
        }
    }

    /// <summary>Used to prove a throwing worker still ends the missile exactly once.</summary>
    private sealed class ThrowingWorker : ArcaneMissileImpactWorker
    {
        public static int Calls;

        public override void Resolve(ArcaneMissileImpactContext context)
        {
            Calls++;
            throw new InvalidOperationException("harness worker failure");
        }
    }

    /// <summary>Deterministic failing backend used by the map-component lifecycle tests.</summary>
    private sealed class FakeVisual : ArcaneMissileVisualSource
    {
        public static int Spawns;
        public static int Updates;
        public static int Impacts;
        public static int Disposes;
        public static bool FailSpawn;
        public static bool FailUpdate;
        public static bool FailDispose;

        public static void Reset(bool failSpawn = false, bool failUpdate = false, bool failDispose = false)
        {
            Spawns = 0;
            Updates = 0;
            Impacts = 0;
            Disposes = 0;
            FailSpawn = failSpawn;
            FailUpdate = failUpdate;
            FailDispose = failDispose;
        }

        public override void Spawn(ArcaneMissileVisualFrame frame)
        {
            Spawns++;
            if (FailSpawn)
            {
                throw new InvalidOperationException("fake spawn failure");
            }
        }

        public override void Update(ArcaneMissileVisualFrame frame)
        {
            Updates++;
            if (FailUpdate)
            {
                throw new InvalidOperationException("fake update failure");
            }
        }

        public override void Impact(ArcaneMissileImpactVisualContext context)
        {
            Impacts++;
        }

        public override void Dispose()
        {
            Disposes++;
            if (FailDispose)
            {
                throw new InvalidOperationException("fake dispose failure");
            }
        }
    }

    private static readonly ArcaneMissileVisualDef FakeVisualDef = new ArcaneMissileVisualDef
    {
        defName = "harness_visual",
        visualClass = typeof(FakeVisual)
    };

    private sealed class Track
    {
        public ArcaneMissile Missile;
        public int Ticks;
        public ArcaneMissileEnd End;
        public float DistanceBeforeLastTick;
        public float WouldBeDistanceAfterFullStep;
        public readonly List<(float x, float z)> Positions = new List<(float x, float z)>();
    }

    private static void Check(string name, bool ok, string detail)
    {
        if (!ok)
        {
            failures++;
        }

        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + " :: " + detail);
    }

    private static int nextThingId = 1000;

    private static Map NewMap(int width, int height)
    {
        return new Map { Size = new IntVec3(width, 1, height), thingGrid = new ThingGrid() };
    }

    private static Thing NewWall(Map map, int x, int z)
    {
        var wall = new Thing
        {
            thingIDNumber = ++nextThingId,
            def = new ThingDef { defName = "Wall", Fillage = FillCategory.Full },
            Spawned = true,
            Map = map,
            Position = new IntVec3(x, 0, z)
        };
        map.thingGrid.Register(wall);
        return wall;
    }

    private static Pawn NewPawn(Map map, int x, int z)
    {
        var pawn = new Pawn
        {
            thingIDNumber = ++nextThingId,
            def = new ThingDef { defName = "Pawn", Fillage = FillCategory.None },
            Spawned = true,
            Map = map,
            Position = new IntVec3(x, 0, z)
        };
        map.thingGrid.Register(pawn);
        return pawn;
    }

    private static ArcaneMissileSettings Defaults()
    {
        ArcaneMissileSettings settings = ArcaneMissileSettings.CreateDefault();
        settings.impactDef = TestDefs.Damage;
        settings.Clamp();
        return settings;
    }

    /// <summary>Missiles spawned through the real launcher/projectile path in the current test.</summary>
    private static readonly List<TestProjectile> liveProjectiles = new List<TestProjectile>();

    /// <summary>Registers the component the way the game's map constructor does.</summary>
    private static MapComponent_ArcaneMissiles NewComponent(Map map)
    {
        var component = new MapComponent_ArcaneMissiles(map);
        map.components.Add(component);
        return component;
    }

    /// <summary>Spawns one missile through the production launcher path (ThingMaker + GenSpawn).</summary>
    private static TestProjectile SpawnProjectile(Map map, ArcaneMissile missile)
    {
        MapComponent_ArcaneMissiles component = MapComponent_ArcaneMissiles.GetOrCreate(map);
        var projectile = (TestProjectile)ThingMaker.MakeThing(TestDefs.Projectile);
        projectile.Initialize(
            missile,
            null,
            new LocalTargetInfo(missile.targetCell),
            new LocalTargetInfo(missile.originalTargetCell));
        GenSpawn.Spawn(projectile, new IntVec3((int)missile.posX, 0, (int)missile.posZ), map);
        component.NotifyLaunched(projectile);
        liveProjectiles.Add(projectile);
        return projectile;
    }

    /// <summary>Drives the thing ticker for every live projectile, as the game would.</summary>
    private static bool TickLive(int maxTicks)
    {
        for (int i = 0; i < maxTicks; i++)
        {
            if (liveProjectiles.Count == 0)
            {
                return true;
            }

            for (int j = liveProjectiles.Count - 1; j >= 0; j--)
            {
                if (liveProjectiles[j].Destroyed)
                {
                    liveProjectiles.RemoveAt(j);
                    continue;
                }

                liveProjectiles[j].Step(1);
            }
        }

        return liveProjectiles.Count == 0;
    }

    /// <summary>Live projectiles whose visual backend failed; the state used to live on the component.</summary>
    private static int BrokenVisualCount()
    {
        int broken = 0;
        for (int i = 0; i < liveProjectiles.Count; i++)
        {
            if (liveProjectiles[i].VisualBroken)
            {
                broken++;
            }
        }

        return broken;
    }

    private static Track Run(
        Map map,
        ArcaneMissileSettings settings,
        Thing targetThing,
        IntVec3 targetCell,
        float startX,
        float startZ,
        float dirX,
        float dirZ,
        int seed,
        int maxTicks)
    {
        var missile = new ArcaneMissile();
        missile.Initialize(null, targetThing, targetCell, startX, startZ, dirX, dirZ, settings, seed);

        float targetX = targetCell.x + 0.5f;
        float targetZ = targetCell.z + 0.5f;
        float cellsPerTick = settings.speedCellsPerSecond / GenTicks.TicksPerRealSecond;

        var track = new Track { Missile = missile, End = ArcaneMissileEnd.Flying };

        for (int i = 0; i < maxTicks; i++)
        {
            float beforeDistance = Distance(missile.posX, missile.posZ, targetX, targetZ);
            float beforeX = missile.posX;
            float beforeZ = missile.posZ;

            ArcaneMissileEnd end = missile.Tick(map);
            track.Ticks = i + 1;
            track.Positions.Add((missile.posX, missile.posZ));

            if (end != ArcaneMissileEnd.Flying)
            {
                track.End = end;
                track.DistanceBeforeLastTick = beforeDistance;
                track.WouldBeDistanceAfterFullStep = Distance(
                    beforeX + missile.headingX * cellsPerTick,
                    beforeZ + missile.headingZ * cellsPerTick,
                    targetX,
                    targetZ);
                return track;
            }
        }

        return track;
    }

    private static float Distance(float x1, float z1, float x2, float z2)
    {
        float dx = x2 - x1;
        float dz = z2 - z1;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static float AngleDelta(float headingAx, float headingAz, float headingBx, float headingBz)
    {
        float a = MathF.Atan2(headingAx, headingAz);
        float b = MathF.Atan2(headingBx, headingBz);
        float delta = b - a;
        while (delta > MathF.PI)
        {
            delta -= MathF.PI * 2f;
        }

        while (delta < -MathF.PI)
        {
            delta += MathF.PI * 2f;
        }

        return MathF.Abs(delta);
    }

    private static void Main()
    {
        Console.WriteLine("== production ArcaneMissile + impact worker harness ==");

        // The launcher resolves its projectile ThingDef by name from the game's DefDatabase.
        DefDatabase<ThingDef>.Named[TestDefs.Projectile.defName] = TestDefs.Projectile;

        RunDefaultConvergence();
        RunReproducibility();
        RunHighSpeedArrival();
        RunWallAtHighSpeed();
        RunTurnLimit();
        RunSpeedIsConstant();
        RunDamageWorker();
        RunMovingTarget();
        RunOriginalTargetContext();
        RunHealWorker();
        RunExplodeWorker();
        RunThrowingWorker();
        RunLifecycleSpawnFailure();
        RunLifecycleBrokenAcrossMapSwitch();
        RunLifecycleUpdateFailure();
        RunLifecycleDisposeFailure();
        RunLifecycleMapVisibility();
        RunNativeCarrier();
        RunFlightLimitFlags();
        RunTargetLostCases();
        RunMenderValidTarget();
        RunLaunchValidation();
        RunLabFrames();
        RunExpiry();
        RunExtremeSettings();

        Console.WriteLine(failures == 0 ? "ALL PASS" : "FAILURES: " + failures);
        Environment.Exit(failures == 0 ? 0 : 1);
    }

    private static void RunDefaultConvergence()
    {
        ArcaneMissileSettings settings = Defaults();
        Track track = Run(NewMap(64, 64), settings, null, new IntVec3(20, 0, 0), 0.5f, 0.5f, 1f, 0f, 12345, 600);

        Check("default/converges", track.End == ArcaneMissileEnd.Impact,
            "end=" + track.End + " ticks=" + track.Ticks);
        Check("default/flight-time", track.Ticks >= 80 && track.Ticks <= 140,
            "ticks=" + track.Ticks + " expected ~" + MathF.Round(20f / settings.speedCellsPerSecond * 60f));
    }

    private static void RunReproducibility()
    {
        ArcaneMissileSettings settings = Defaults();
        Track first = Run(NewMap(64, 64), settings, null, new IntVec3(20, 0, 0), 0.5f, 0.5f, 1f, 0f, 424242, 600);
        Track second = Run(NewMap(64, 64), settings, null, new IntVec3(20, 0, 0), 0.5f, 0.5f, 1f, 0f, 424242, 600);

        bool identical = first.Positions.Count == second.Positions.Count;
        if (identical)
        {
            for (int i = 0; i < first.Positions.Count; i++)
            {
                if (first.Positions[i].x != second.Positions[i].x || first.Positions[i].z != second.Positions[i].z)
                {
                    identical = false;
                    break;
                }
            }
        }

        Check("reproducible/same-seed", identical,
            identical ? "identical path for " + first.Positions.Count + " ticks" : "runs differed");
    }

    private static void RunHighSpeedArrival()
    {
        ArcaneMissileSettings settings = Defaults();
        settings.speedCellsPerSecond = 60f;
        settings.Clamp();

        Track track = Run(NewMap(64, 64), settings, null, new IntVec3(20, 0, 0), 0.5f, 0.5f, 1f, 0f, 5150, 200);
        float arrival = MathF.Max(0.35f, settings.hitRadius);

        Check("highspeed/arrives", track.End == ArcaneMissileEnd.Impact,
            "speed=60 end=" + track.End + " ticks=" + track.Ticks);

        // Worst-case lattice: start exactly on a cell edge so tick boundaries sit 0.5 cells on
        // either side of the target centre (0.5 > arrival radius 0.45 on both sides).
        ArcaneMissileSettings straight = Defaults();
        straight.speedCellsPerSecond = 60f;
        straight.Clamp();

        Track straddle = Run(NewMap(64, 64), straight, null, new IntVec3(20, 0, 0), 0f, 0.5f, 1f, 0f, 5150, 200);

        Check("highspeed/lattice-arrives", straddle.End == ArcaneMissileEnd.Impact,
            "end=" + straddle.End + " ticks=" + straddle.Ticks);
        Check("highspeed/tick-boundaries-would-miss",
            straddle.DistanceBeforeLastTick > arrival && straddle.WouldBeDistanceAfterFullStep > arrival,
            "arrivalRadius=" + arrival + " distance at tick start=" + straddle.DistanceBeforeLastTick.ToString("F3")
            + " distance after the full 1.0 cell step=" + straddle.WouldBeDistanceAfterFullStep.ToString("F3"));
    }

    private static void RunWallAtHighSpeed()
    {
        ArcaneMissileSettings settings = Defaults();
        settings.speedCellsPerSecond = 60f;
        settings.Clamp();

        Map map = NewMap(64, 64);
        Thing wall = NewWall(map, 10, 0);
        Track track = Run(map, settings, null, new IntVec3(20, 0, 0), 0.5f, 0.5f, 1f, 0f, 99, 200);

        Check("wall/hit-not-passed", track.End == ArcaneMissileEnd.Impact && wall.DamageTaken > 0f,
            "wall damage=" + wall.DamageTaken + " end=" + track.End);
        Check("wall/stopped-inside-wall-cell", track.Missile.posX >= 10f && track.Missile.posX < 11f,
            "final x=" + track.Missile.posX.ToString("F3") + " (wall cell is [10,11))");
    }

    private static void RunTurnLimit()
    {
        ArcaneMissileSettings settings = Defaults();
        settings.maxTurnDegreesPerSecond = 120f;
        settings.Clamp();

        Map map = NewMap(64, 64);
        var missile = new ArcaneMissile();
        missile.Initialize(null, null, new IntVec3(0, 0, -20), 10.5f, 10.5f, 1f, 0f, settings, 31337);

        float previousX = missile.headingX;
        float previousZ = missile.headingZ;
        float maxDelta = settings.maxTurnDegreesPerSecond * (MathF.PI / 180f) / GenTicks.TicksPerRealSecond;
        float worst = 0f;
        bool finite = true;

        for (int i = 0; i < 240; i++)
        {
            if (missile.Tick(map) != ArcaneMissileEnd.Flying)
            {
                break;
            }

            float delta = AngleDelta(previousX, previousZ, missile.headingX, missile.headingZ);
            if (delta > worst)
            {
                worst = delta;
            }

            if (float.IsNaN(missile.posX) || float.IsInfinity(missile.posX)
                || float.IsNaN(missile.posZ) || float.IsInfinity(missile.posZ))
            {
                finite = false;
            }

            previousX = missile.headingX;
            previousZ = missile.headingZ;
        }

        Check("turn/limit-respected", worst <= maxDelta + 1e-4f,
            "worst per-tick turn=" + worst.ToString("F6") + " rad, limit=" + maxDelta.ToString("F6") + " rad");
        Check("turn/finite", finite, "no NaN/Infinity while circling");
    }

    private static void RunSpeedIsConstant()
    {
        ArcaneMissileSettings settings = Defaults();
        Map map = NewMap(64, 64);
        var missile = new ArcaneMissile();
        missile.Initialize(null, null, new IntVec3(40, 0, 0), 0.5f, 0.5f, 1f, 0f, settings, 2468);

        float expected = settings.speedCellsPerSecond / GenTicks.TicksPerRealSecond;
        float worstError = 0f;
        int samples = 0;

        for (int i = 0; i < 100; i++)
        {
            float x = missile.posX;
            float z = missile.posZ;
            if (missile.Tick(map) != ArcaneMissileEnd.Flying)
            {
                break;
            }

            float dx = missile.posX - x;
            float dz = missile.posZ - z;
            float moved = MathF.Sqrt(dx * dx + dz * dz);
            if (Distance(missile.posX, missile.posZ, 40.5f, 0.5f) > 6f)
            {
                float error = MathF.Abs(moved - expected);
                if (error > worstError)
                {
                    worstError = error;
                }

                samples++;
            }
        }

        Check("speed/constant", samples > 50 && worstError < 1e-3f,
            "samples=" + samples + " max per-tick displacement error=" + worstError.ToString("F6"));
    }

    private static void RunDamageWorker()
    {
        Map map = NewMap(64, 64);
        Pawn pawn = NewPawn(map, 12, 0);
        Track track = Run(map, Defaults(), pawn, pawn.Position, 0.5f, 0.5f, 1f, 0f, 8888, 300);

        Check("impact/damage-once", track.End == ArcaneMissileEnd.Impact
            && MathF.Abs(pawn.DamageTaken - Defaults().damageAmount) < 1e-4f,
            "damage=" + pawn.DamageTaken + " (expected exactly " + Defaults().damageAmount + ") end=" + track.End);
    }

    private static void RunMovingTarget()
    {
        Map map = NewMap(64, 64);
        Pawn runner = NewPawn(map, 6, 12);
        runner.HasDrawPosOverride = true;
        runner.DrawPosOverride = new UnityEngine.Vector3(6.5f, 0f, 12.5f);

        ArcaneMissileSettings settings = Defaults();
        var missile = new ArcaneMissile();
        missile.Initialize(null, runner, new IntVec3(6, 0, 12), 0.5f, 0.5f, 1f, 0f, settings, 2718);

        bool impacted = false;
        int ticks = 0;
        for (int i = 0; i < 400; i++)
        {
            float z = runner.DrawPosOverride.z + 4f / 60f;
            runner.DrawPosOverride = new UnityEngine.Vector3(6.5f, 0f, z);
            var cell = new IntVec3(6, 0, (int)MathF.Floor(z));
            if (runner.Position != cell)
            {
                map.thingGrid.Move(runner, cell);
            }

            ticks = i + 1;
            if (missile.Tick(map) != ArcaneMissileEnd.Flying)
            {
                impacted = true;
                break;
            }
        }

        Check("moving-target/hit", impacted && MathF.Abs(runner.DamageTaken - settings.damageAmount) < 1e-4f,
            "ticks=" + ticks + " damage=" + runner.DamageTaken + " runner end z=" + runner.DrawPosOverride.z.ToString("F1"));
    }

    private static void RunOriginalTargetContext()
    {
        Map map = NewMap(64, 64);
        Pawn pawn = NewPawn(map, 12, 0);
        ArcaneMissileSettings settings = Defaults();
        settings.impactDef = TestDefs.Capture;
        CapturingWorker.Last = default;

        var missile = new ArcaneMissile();
        IntVec3 launchCell = pawn.Position;
        missile.Initialize(null, pawn, launchCell, 0.5f, 0.5f, 1f, 0f, settings, 2024);

        bool impacted = false;
        for (int i = 0; i < 300; i++)
        {
            if (i == 10)
            {
                pawn.Destroyed = true;
                pawn.Spawned = false;
                map.thingGrid.Remove(pawn);
            }

            if (missile.Tick(map) != ArcaneMissileEnd.Flying)
            {
                impacted = true;
                break;
            }
        }

        ArcaneMissileImpactContext captured = CapturingWorker.Last;
        bool keptOriginal = captured.OriginalTarget == pawn && captured.OriginalTargetCell == launchCell;

        Check("context/original-target-kept", impacted && keptOriginal,
            "impacted=" + impacted + " originalTarget=" + (captured.OriginalTarget != null ? captured.OriginalTarget.LabelShort : "null")
            + " originalCell=" + captured.OriginalTargetCell);
        Check("context/dead-target-is-ground-hit", captured.Hit == null,
            "hit=" + (captured.Hit != null ? captured.Hit.LabelShort : "null") + " impactCell=" + captured.ImpactCell);
    }

    private static void RunHealWorker()
    {
        Map map = NewMap(64, 64);
        Pawn pawn = NewPawn(map, 12, 0);

        // Three separate wounds so a skipped entry would be visible in the totals.
        var first = new Hediff_Injury { Severity = 8f };
        var second = new Hediff_Injury { Severity = 4f };
        var third = new Hediff_Injury { Severity = 4f };
        pawn.health.hediffSet.hediffs.Add(first);
        pawn.health.hediffSet.hediffs.Add(second);
        pawn.health.hediffSet.hediffs.Add(third);

        ArcaneMissileSettings settings = Defaults();
        settings.impactDef = TestDefs.Heal;
        settings.healAmount = 10f;

        float before = first.Severity + second.Severity + third.Severity;
        Track track = Run(map, settings, pawn, pawn.Position, 0.5f, 0.5f, 1f, 0f, 4321, 300);
        float after = first.Severity + second.Severity + third.Severity;

        Check("impact/heal-once", track.End == ArcaneMissileEnd.Impact
            && MathF.Abs(before - after - 10f) < 1e-4f && pawn.DamageTaken == 0f,
            "total severity " + before + " -> " + after + " (exactly one 10-point heal across 3 wounds)");
    }

    private static void RunLifecycleSpawnFailure()
    {
        Map map = NewMap(64, 64);
        Find.CurrentMap = map;
        Pawn pawn = NewPawn(map, 10, 0);
        NewComponent(map);
        FakeVisual.Reset(failSpawn: true);

        ArcaneMissileSettings settings = Defaults();
        settings.visualDef = FakeVisualDef;

        var missile = new ArcaneMissile();
        missile.Initialize(null, pawn, pawn.Position, 0.5f, 0.5f, 1f, 0f, settings, 101);
        liveProjectiles.Clear();
        SpawnProjectile(map, missile);

        bool done = TickLive(300);

        Check("lifecycle/spawn-failure-once", done && FakeVisual.Spawns == 1 && FakeVisual.Updates == 0
            && MathF.Abs(pawn.DamageTaken - 7f) < 1e-4f,
            "spawn attempts=" + FakeVisual.Spawns + " updates=" + FakeVisual.Updates
            + " damage=" + pawn.DamageTaken + " (logic unaffected, no retry)");
        Check("lifecycle/broken-record-released", done && BrokenVisualCount() == 0,
            "broken live projectiles after the missile ended=" + BrokenVisualCount());
    }

    private static void RunLifecycleBrokenAcrossMapSwitch()
    {
        Map mapA = NewMap(64, 64);
        Map mapB = NewMap(32, 32);
        Find.CurrentMap = mapA;

        var component = NewComponent(mapA);
        FakeVisual.Reset(failSpawn: true);

        ArcaneMissileSettings settings = Defaults();
        settings.visualDef = FakeVisualDef;

        var missile = new ArcaneMissile();
        missile.Initialize(null, null, new IntVec3(30, 0, 0), 0.5f, 0.5f, 1f, 0f, settings, 105);
        liveProjectiles.Clear();
        SpawnProjectile(mapA, missile);

        component.MapComponentUpdate();
        int brokenAfterSpawnFailure = BrokenVisualCount();

        Find.CurrentMap = mapB;
        component.MapComponentUpdate();
        int brokenWhileHidden = BrokenVisualCount();
        int spawnsAfterHide = FakeVisual.Spawns;

        Find.CurrentMap = mapA;
        component.MapComponentUpdate();
        int spawnsAfterReturn = FakeVisual.Spawns;

        bool done = TickLive(400);
        int brokenAfterEnd = BrokenVisualCount();

        Check("lifecycle/broken-survives-map-switch",
            brokenAfterSpawnFailure == 1 && brokenWhileHidden == 1 && spawnsAfterReturn == spawnsAfterHide,
            "broken after spawn=" + brokenAfterSpawnFailure + " while hidden=" + brokenWhileHidden
            + " spawns on return=" + spawnsAfterReturn + " (no retry)");
        Check("lifecycle/broken-cleared-after-end", done && brokenAfterEnd == 0,
            "entries after end=" + brokenAfterEnd);
    }

    private static void RunLifecycleUpdateFailure()
    {
        Map map = NewMap(64, 64);
        Find.CurrentMap = map;
        Pawn pawn = NewPawn(map, 10, 0);
        NewComponent(map);
        FakeVisual.Reset(failUpdate: true);

        ArcaneMissileSettings settings = Defaults();
        settings.visualDef = FakeVisualDef;

        var missile = new ArcaneMissile();
        missile.Initialize(null, pawn, pawn.Position, 0.5f, 0.5f, 1f, 0f, settings, 102);
        liveProjectiles.Clear();
        SpawnProjectile(map, missile);

        bool done = TickLive(300);

        Check("lifecycle/update-failure-once", done && FakeVisual.Spawns == 1 && FakeVisual.Updates == 1
            && MathF.Abs(pawn.DamageTaken - 7f) < 1e-4f,
            "spawn=" + FakeVisual.Spawns + " updates=" + FakeVisual.Updates
            + " damage=" + pawn.DamageTaken + " (visual dropped, no re-create per tick)");
    }

    private static void RunLifecycleDisposeFailure()
    {
        Map map = NewMap(64, 64);
        Find.CurrentMap = map;
        Pawn pawn = NewPawn(map, 10, 0);
        var component = NewComponent(map);
        FakeVisual.Reset(failDispose: true);

        ArcaneMissileSettings settings = Defaults();
        settings.visualDef = FakeVisualDef;

        var missile = new ArcaneMissile();
        missile.Initialize(null, pawn, pawn.Position, 0.5f, 0.5f, 1f, 0f, settings, 103);
        liveProjectiles.Clear();
        SpawnProjectile(map, missile);

        int errorsBefore = Log.Errors.Count;
        bool done = TickLive(300);

        Check("lifecycle/dispose-failure-does-not-block", done && component.LiveMissileCount == 0
            && FakeVisual.Disposes >= 1 && Log.Errors.Count > errorsBefore,
            "live=" + component.LiveMissileCount + " disposes=" + FakeVisual.Disposes
            + " errorsLogged=" + (Log.Errors.Count - errorsBefore));
    }

    private static void RunLifecycleMapVisibility()
    {
        Map mapA = NewMap(64, 64);
        Map mapB = NewMap(32, 32);
        Find.CurrentMap = mapA;

        var component = NewComponent(mapA);
        FakeVisual.Reset();

        ArcaneMissileSettings settings = Defaults();
        settings.visualDef = FakeVisualDef;

        var missile = new ArcaneMissile();
        missile.Initialize(null, null, new IntVec3(30, 0, 0), 0.5f, 0.5f, 1f, 0f, settings, 104);
        liveProjectiles.Clear();
        SpawnProjectile(mapA, missile);

        // The frame update (not a tick) is what brings a visual up, here and after a load.
        component.MapComponentUpdate();
        bool spawned = FakeVisual.Spawns == 1;
        float beforeX = missile.posX;

        Find.CurrentMap = mapB;
        component.MapComponentUpdate();
        int disposesAfterHide = FakeVisual.Disposes;
        int spawnsBeforeHiddenTicks = FakeVisual.Spawns;

        // Gameplay is the thing ticker's job now, so it keeps advancing while the map is hidden.
        TickLive(2);
        float afterHiddenTicks = missile.posX;
        bool stayedHidden = FakeVisual.Spawns == spawnsBeforeHiddenTicks && FakeVisual.Updates == 0;

        Find.CurrentMap = mapA;
        component.MapComponentUpdate();

        Check("lifecycle/hide-on-other-map", spawned && disposesAfterHide == 1 && stayedHidden,
            "spawned=" + FakeVisual.Spawns + " disposedOnHide=" + disposesAfterHide
            + " spawnsWhileHiddenStayed=" + stayedHidden);
        Check("lifecycle/logic-keeps-running-while-hidden", afterHiddenTicks > beforeX,
            "x " + beforeX.ToString("F2") + " -> " + afterHiddenTicks.ToString("F2") + " over 2 hidden ticks");
        Check("lifecycle/rebuild-without-tick", FakeVisual.Spawns == 2 && FakeVisual.Updates == 0,
            "spawns=" + FakeVisual.Spawns + " updates=" + FakeVisual.Updates
            + " (rebuilt from frame update, so a paused load shows immediately)");
    }

    /// <summary>
    /// The migration's own claims: the spawned thing is what the map registry and save see, one
    /// delta batch equals that many single ticks, the override set owns tick/save/destroy, and the
    /// map component keeps no missile collection of its own.
    /// </summary>
    private static void RunNativeCarrier()
    {
        Map map = NewMap(64, 64);
        Find.CurrentMap = map;
        var component = NewComponent(map);
        FakeVisual.Reset();

        ArcaneMissileSettings settings = Defaults();
        settings.visualDef = FakeVisualDef;

        ArcaneMissile missile = NewMissile(settings, new IntVec3(40, 0, 0), 10.5f, 10.5f, 1f, 0f, 7801);
        liveProjectiles.Clear();
        TestProjectile projectile = SpawnProjectile(map, missile);
        component.MapComponentUpdate();

        bool registered = projectile.Spawned
            && ReferenceEquals(projectile.Map, map)
            && map.listerThings.ThingsOfDef(TestDefs.Projectile).Contains(projectile)
            && component.LiveMissileCount == 1;
        Check("native/spawn-registers-in-map", registered,
            "spawned=" + projectile.Spawned + " live=" + component.LiveMissileCount
            + " (map registry is the save source, not a mod list)");

        projectile.Step(1);
        var expected = new IntVec3((int)MathF.Floor(missile.posX), 0, (int)MathF.Floor(missile.posZ));
        Check("native/tick-syncs-thing-position", projectile.Position == expected,
            "thing=" + projectile.Position + " state=" + missile.posX.ToString("F2") + "," + missile.posZ.ToString("F2"));

        // A 15-tick offscreen batch must equal 15 single ticks, turning included.
        TestProjectile batch = SpawnProjectile(map, NewMissile(settings, new IntVec3(20, 0, 30), 20.5f, 20.5f, 1f, 0f, 7802));
        TestProjectile singles = SpawnProjectile(map, NewMissile(settings, new IntVec3(20, 0, 30), 20.5f, 20.5f, 1f, 0f, 7802));
        batch.Step(15);
        for (int i = 0; i < 15; i++)
        {
            singles.Step(1);
        }

        bool sameSteering = MathF.Abs(batch.State.posX - singles.State.posX) < 1e-4f
            && MathF.Abs(batch.State.posZ - singles.State.posZ) < 1e-4f
            && MathF.Abs(batch.State.headingX - singles.State.headingX) < 1e-6f
            && batch.State.ageTicks == singles.State.ageTicks;
        Check("native/delta-batch-equals-single-ticks", sameSteering,
            "batch=" + batch.State.ageTicks + " singles=" + singles.State.ageTicks
            + " dx=" + (batch.State.posX - singles.State.posX).ToString("F5"));

        bool ownsOverrides = typeof(Projectile_ArcaneMissiles).GetMethod("ExposeData").DeclaringType == typeof(Projectile_ArcaneMissiles)
            && typeof(Projectile_ArcaneMissiles)
                .GetMethod("TickInterval", BindingFlags.NonPublic | BindingFlags.Instance)
                .DeclaringType == typeof(Projectile_ArcaneMissiles)
            && typeof(Projectile_ArcaneMissiles).GetMethod("Destroy").DeclaringType == typeof(Projectile_ArcaneMissiles);
        Check("native/overrides-tick-save-destroy", ownsOverrides,
            "ExposeData/TickInterval/Destroy declared by Projectile_ArcaneMissiles");

        projectile.Destroy();
        bool unregistered = projectile.Destroyed && !map.listerThings.ThingsOfDef(TestDefs.Projectile).Contains(projectile);
        Check("native/destroy-unregisters", unregistered,
            "destroyed=" + projectile.Destroyed + " live=" + component.LiveMissileCount);

        bool collectionFree = true;
        foreach (FieldInfo field in typeof(MapComponent_ArcaneMissiles).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (typeof(IEnumerable).IsAssignableFrom(field.FieldType) && field.FieldType != typeof(string))
            {
                collectionFree = false;
            }
        }

        Check("component/no-missile-collection", collectionFree,
            "map component instance fields hold no collection, so no second simulation list");
    }

    private static ArcaneMissile NewMissile(
        ArcaneMissileSettings settings,
        IntVec3 targetCell,
        float startX,
        float startZ,
        float dirX,
        float dirZ,
        int seed)
    {
        var missile = new ArcaneMissile();
        missile.Initialize(null, null, targetCell, startX, startZ, dirX, dirZ, settings, seed);
        return missile;
    }

    private static void RunExplodeWorker()
    {
        ArcaneMissileSettings settings = Defaults();
        settings.impactDef = TestDefs.Explode;

        int before = GenExplosion.Calls;
        Track track = Run(NewMap(64, 64), settings, null, new IntVec3(10, 0, 0), 0.5f, 0.5f, 1f, 0f, 4040, 300);

        Check("impact/explode-once", track.End == ArcaneMissileEnd.Impact && GenExplosion.Calls == before + 1,
            "explosions=" + (GenExplosion.Calls - before) + " end=" + track.End);
    }

    private static void RunThrowingWorker()
    {
        ArcaneMissileSettings settings = Defaults();
        settings.impactDef = TestDefs.Throwing;

        int errorsBefore = Log.Errors.Count;
        int callsBefore = ThrowingWorker.Calls;
        Track track = Run(NewMap(64, 64), settings, null, new IntVec3(10, 0, 0), 0.5f, 0.5f, 1f, 0f, 3030, 300);

        Check("impact/worker-failure-ends-missile",
            track.End == ArcaneMissileEnd.Impact
            && ThrowingWorker.Calls == callsBefore + 1
            && Log.Errors.Count == errorsBefore + 1,
            "end=" + track.End + " workerCalls=+1 errorsLogged=+" + (Log.Errors.Count - errorsBefore)
            + " -> caller removes the missile, so no per-tick repeat");
    }

    private static string LastMissileLog()
    {
        return ExperimentLog.Entries.Count == 0 ? "" : ExperimentLog.Entries[ExperimentLog.Entries.Count - 1];
    }

    private static void RunFlightLimitFlags()
    {
        int before = ExperimentLog.Entries.Count;
        Track limited = Run(NewMap(250, 250), Defaults(), null, new IntVec3(60, 0, 0), 0.5f, 0.5f, 1f, 0f, 7101, 5000);
        string reason = ExperimentLog.Entries.Count > before ? LastMissileLog() : "";
        Check("limits/old-range-preserved",
            limited.End == ArcaneMissileEnd.Expired && reason.Contains("range"),
            "end=" + limited.End + " reason=" + reason);

        ArcaneMissileSettings shortLived = Defaults();
        shortLived.lifetimeSeconds = 1f;
        Track lifetime = Run(NewMap(250, 250), shortLived, null, new IntVec3(60, 0, 0), 0.5f, 0.5f, 1f, 0f, 7102, 5000);
        Check("limits/old-lifetime-preserved",
            lifetime.End == ArcaneMissileEnd.Expired && lifetime.Ticks <= 62,
            "end=" + lifetime.End + " ticks=" + lifetime.Ticks);

        ArcaneMissileSettings unlocked = Defaults();
        unlocked.ignoreFlightLimits = true;
        unlocked.speedCellsPerSecond = 1f;
        Map map = NewMap(250, 250);
        var missile = new ArcaneMissile();
        missile.Initialize(null, null, new IntVec3(60, 0, 0), 0.5f, 0.5f, 1f, 0f, unlocked, 7103);
        bool flyingAt10s = false;
        bool pastRange = false;
        ArcaneMissileEnd end = ArcaneMissileEnd.Flying;
        for (int i = 0; i < 5000; i++)
        {
            end = missile.Tick(map);
            if (i == 2100)
            {
                flyingAt10s = end == ArcaneMissileEnd.Flying;
                pastRange = missile.posX > 34f;
            }

            if (end != ArcaneMissileEnd.Flying)
            {
                break;
            }
        }

        Check("limits/flag-ignores-both", flyingAt10s && pastRange && end == ArcaneMissileEnd.Impact,
            "flyingAt35s=" + flyingAt10s + " pastRange34=" + pastRange + " end=" + end);

        ArcaneMissileSettings bounds = Defaults();
        bounds.ignoreFlightLimits = true;
        bounds.impactDef = TestDefs.Counting;
        int calls = CountingWorker.Calls;
        Track oob = Run(NewMap(20, 20), bounds, null, new IntVec3(100, 0, 0), 0.5f, 0.5f, 1f, 0f, 7104, 1000);
        Check("limits/out-of-bounds-no-effect",
            oob.End == ArcaneMissileEnd.Expired && CountingWorker.Calls == calls,
            "end=" + oob.End + " workerCalls=+" + (CountingWorker.Calls - calls));
    }

    private static void RunTargetLostCases()
    {
        ArcaneMissileSettings s = Defaults();
        s.expireOnTargetLost = true;
        s.ignoreFlightLimits = true;
        s.impactDef = TestDefs.Counting;

        Map map = NewMap(64, 64);
        Map otherMap = NewMap(64, 64);

        Pawn deadPawn = NewPawn(map, 10, 0);
        var deadMissile = new ArcaneMissile();
        deadMissile.Initialize(null, deadPawn, deadPawn.Position, 0.5f, 0.5f, 1f, 0f, s, 7201);
        deadPawn.Dead = true;   // still Spawned, still on this map, not Destroyed
        int calls = CountingWorker.Calls;
        ArcaneMissileEnd deadEnd = deadMissile.Tick(map);
        Check("targetlost/dead-still-spawned",
            deadEnd == ArcaneMissileEnd.Expired && CountingWorker.Calls == calls && LastMissileLog().Contains("targetLost"),
            "end=" + deadEnd + " workerCalls=+" + (CountingWorker.Calls - calls) + " log=" + LastMissileLog());

        Pawn gonePawn = NewPawn(map, 12, 0);
        var goneMissile = new ArcaneMissile();
        goneMissile.Initialize(null, gonePawn, gonePawn.Position, 0.5f, 0.5f, 1f, 0f, s, 7202);
        gonePawn.Spawned = false;
        calls = CountingWorker.Calls;
        ArcaneMissileEnd goneEnd = goneMissile.Tick(map);
        Check("targetlost/despawned",
            goneEnd == ArcaneMissileEnd.Expired && CountingWorker.Calls == calls,
            "end=" + goneEnd + " workerCalls=+" + (CountingWorker.Calls - calls));

        Pawn movedPawn = NewPawn(map, 14, 0);
        var movedMissile = new ArcaneMissile();
        movedMissile.Initialize(null, movedPawn, movedPawn.Position, 0.5f, 0.5f, 1f, 0f, s, 7203);
        movedPawn.Map = otherMap;
        calls = CountingWorker.Calls;
        ArcaneMissileEnd movedEnd = movedMissile.Tick(map);
        Check("targetlost/other-map",
            movedEnd == ArcaneMissileEnd.Expired && CountingWorker.Calls == calls,
            "end=" + movedEnd + " workerCalls=+" + (CountingWorker.Calls - calls));

        Pawn loadedPawn = NewPawn(map, 16, 0);
        var loadedMissile = new ArcaneMissile();
        loadedMissile.Initialize(null, loadedPawn, loadedPawn.Position, 0.5f, 0.5f, 1f, 0f, s, 7204);
        loadedMissile.targetThing = null;   // load lost the reference
        calls = CountingWorker.Calls;
        ArcaneMissileEnd loadedEnd = loadedMissile.Tick(map);
        Check("targetlost/null-reference",
            loadedEnd == ArcaneMissileEnd.Expired && CountingWorker.Calls == calls,
            "end=" + loadedEnd + " workerCalls=+" + (CountingWorker.Calls - calls));
    }

    private static void RunMenderValidTarget()
    {
        Map map = NewMap(250, 250);
        Pawn pawn = NewPawn(map, 40, 0);
        var injury = new Hediff_Injury { Severity = 20f };
        pawn.health.hediffSet.hediffs.Add(injury);

        ArcaneMissileSettings s = Defaults();
        s.speedCellsPerSecond = 4f;
        s.maxTurnDegreesPerSecond = 720f;
        s.lifetimeSeconds = 1f;
        s.maxRangeCells = 10f;
        s.ignoreFlightLimits = true;
        s.expireOnTargetLost = true;
        s.impactDef = TestDefs.Heal;
        s.healAmount = 10f;

        Track track = Run(map, s, pawn, pawn.Position, 0.5f, 0.5f, 1f, 0f, 7301, 3000);
        Check("mender/tracks-past-old-limits",
            track.End == ArcaneMissileEnd.Impact && track.Ticks > 61 && MathF.Abs(injury.Severity - 10f) < 1e-4f,
            "ticks=" + track.Ticks + " (old lifetime 60, old range 10) severity=" + injury.Severity);
    }

    private static void RunLaunchValidation()
    {
        Map map = NewMap(64, 64);
        Find.CurrentMap = map;
        Pawn caster = NewPawn(map, 5, 5);
        liveProjectiles.Clear();

        ArcaneMissileSettings s = Defaults();
        s.expireOnTargetLost = true;
        s.visualDef = FakeVisualDef;
        s.missileCount = 1;

        int errors = Log.Errors.Count;
        int cellLaunch = ArcaneMissileLauncher.Fire(caster, new LocalTargetInfo(new IntVec3(10, 0, 10)), s);
        bool cellRejected = cellLaunch == 0 && Log.Errors.Count > errors;

        Pawn dead = NewPawn(map, 12, 0);
        dead.Dead = true;
        errors = Log.Errors.Count;
        int deadLaunch = ArcaneMissileLauncher.Fire(caster, new LocalTargetInfo(dead), s);
        bool deadRejected = deadLaunch == 0 && Log.Errors.Count > errors;

        Pawn alive = NewPawn(map, 14, 0);
        int liveLaunch = ArcaneMissileLauncher.Fire(caster, new LocalTargetInfo(alive), s);
        int live = MapComponent_ArcaneMissiles.GetOrCreate(map).LiveMissileCount;

        Check("launch/rejects-cell-target", cellRejected, "launched=" + cellLaunch);
        Check("launch/rejects-dead-target", deadRejected, "launched=" + deadLaunch);
        Check("launch/accepts-live-target", liveLaunch == 1 && live == 1,
            "launched=" + liveLaunch + " live=" + live);
    }

    private static void RunLabFrames()
    {
        Map map = NewMap(64, 64);
        Find.CurrentMap = map;
        var component = NewComponent(map);
        Pawn target = NewPawn(map, 12, 0);

        ArcaneMissileSettings s = Defaults();
        s.visualDef = FakeVisualDef;
        var missile = new ArcaneMissile();
        missile.Initialize(null, target, target.Position, 0.5f, 0.5f, 1f, 0f, s, 7401);
        liveProjectiles.Clear();
        SpawnProjectile(map, missile);

        IReadOnlyList<ArcaneMissileLabFrame> frames = component.GetLabFrames();
        bool single = frames.Count == 1;
        ArcaneMissileLabFrame frame = single ? frames[0] : default;
        bool targetId = frame.TargetThingId == target.thingIDNumber;
        bool targetPoint = MathF.Abs(frame.TargetX - target.DrawPos.x) < 1e-4f
            && MathF.Abs(frame.TargetZ - target.DrawPos.z) < 1e-4f;

        bool copied = frame.Settings != null && !ReferenceEquals(frame.Settings, missile.settings);
        float originalSpeed = missile.settings.speedCellsPerSecond;
        if (copied)
        {
            frame.Settings.speedCellsPerSecond = 99f;
        }

        bool isolated = missile.settings.speedCellsPerSecond == originalSpeed;

        bool done = TickLive(400);
        bool cleared = component.GetLabFrames().Count == 0;

        Check("lab/frame-live-snapshot", single && targetId && targetPoint,
            "count=" + frames.Count + " targetId=" + frame.TargetThingId + " point=" + targetPoint);
        Check("lab/settings-copied", copied && isolated,
            "copied=" + copied + " missileUnchanged=" + isolated);
        Check("lab/live-only", done && cleared, "done=" + done + " framesAfterEnd=" + component.GetLabFrames().Count);
    }

    private static void RunExpiry()
    {
        ArcaneMissileSettings lifetime = Defaults();
        lifetime.lifetimeSeconds = 0.5f;
        lifetime.Clamp();
        Track lifetimeTrack = Run(NewMap(64, 64), lifetime, null, new IntVec3(40, 0, 0), 0.5f, 0.5f, 1f, 0f, 11, 400);

        ArcaneMissileSettings range = Defaults();
        range.maxRangeCells = 5f;
        range.Clamp();
        Track rangeTrack = Run(NewMap(64, 64), range, null, new IntVec3(40, 0, 0), 0.5f, 0.5f, 1f, 0f, 12, 400);

        Track boundsTrack = Run(NewMap(20, 20), Defaults(), null, new IntVec3(40, 0, 0), 0.5f, 0.5f, 1f, 0f, 13, 400);

        Check("expiry/lifetime", lifetimeTrack.End == ArcaneMissileEnd.Expired,
            "ticks=" + lifetimeTrack.Ticks + " end=" + lifetimeTrack.End);
        Check("expiry/range", rangeTrack.End == ArcaneMissileEnd.Expired,
            "final x=" + rangeTrack.Missile.posX.ToString("F2") + " end=" + rangeTrack.End);
        Check("expiry/out-of-bounds", boundsTrack.End == ArcaneMissileEnd.Expired,
            "end=" + boundsTrack.End);
    }

    private static void RunExtremeSettings()
    {
        ArcaneMissileSettings settings = Defaults();
        settings.wobbleAmplitude = 1.5f;
        settings.wobbleFrequency = 12f;
        settings.speedCellsPerSecond = 1f;
        settings.maxTurnDegreesPerSecond = 300f;
        settings.Clamp();

        Track track = Run(NewMap(64, 64), settings, null, new IntVec3(20, 0, 0), 0.5f, 0.5f, 1f, 0f, 60606, 900);

        bool finite = true;
        for (int i = 0; i < track.Positions.Count; i++)
        {
            if (float.IsNaN(track.Positions[i].x) || float.IsInfinity(track.Positions[i].x)
                || float.IsNaN(track.Positions[i].z) || float.IsInfinity(track.Positions[i].z))
            {
                finite = false;
                break;
            }
        }

        Check("extreme/finite", finite, "ticks=" + track.Ticks + " end=" + track.End);
    }
}

