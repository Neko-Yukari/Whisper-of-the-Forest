using System;
using System.Collections.Generic;
using WhisperOfTheForest.Experimental;
using WhisperOfTheForest.Experimental.Missiles;
using Verse;

/// <summary>
/// Read-only analysis for two candidate missile profiles, executed against the production
/// ArcaneMissile stepper (compiled from Source unchanged) with range/lifetime effectively disabled
/// so only real map exit or arrival ends a run. Nothing here ships.
/// </summary>
internal static class AnalysisProgram
{
    private const int Tps = 60;
    private const int MapSize = 250;
    private const float Center = 125f;

    /// <summary>Human MoveSpeed 4.6 on soil (terrain cost 2 adds 2 ticks per cell).</summary>
    private static readonly float SoilSpeed = 60f / (60f / 4.6f + 2f);

    private const float FloorSpeed = 4.6f;

    private enum MoveMode
    {
        Stationary,
        Lateral,   // perpendicular to the launch line: the classic dodge
        Away       // straight away from the shooter
    }

    private sealed class RunResult
    {
        public bool Hit;
        public bool HitAfterPass;
        public bool Ended;
        public string Reason = "";
        public int Ticks;
        public float MinDistance = float.MaxValue;
        public bool TargetLeftMap;
        public float FinalGap;
    }

    private static readonly ArcaneMissileImpactDef CountingImpact = new ArcaneMissileImpactDef
    {
        defName = "analysis_impact",
        workerClass = typeof(ArcaneMissileImpactWorker_Damage)
    };

    private static Map NewMap()
    {
        return new Map { Size = new IntVec3(MapSize, 1, MapSize), thingGrid = new ThingGrid() };
    }

    private static Pawn NewPawn(Map map, float x, float z)
    {
        var pawn = new Pawn
        {
            def = new ThingDef { defName = "Pawn", Fillage = FillCategory.None },
            Spawned = true,
            Map = map,
            HasDrawPosOverride = true,
            DrawPosOverride = new UnityEngine.Vector3(x, 0f, z)
        };
        pawn.Position = new IntVec3((int)MathF.Floor(x), 0, (int)MathF.Floor(z));
        map.thingGrid.Register(pawn);
        return pawn;
    }

    private static void MovePawn(Map map, Pawn pawn, float x, float z)
    {
        pawn.DrawPosOverride = new UnityEngine.Vector3(x, 0f, z);
        var cell = new IntVec3((int)MathF.Floor(x), 0, (int)MathF.Floor(z));
        if (pawn.Position != cell)
        {
            map.thingGrid.Move(pawn, cell);
        }
    }

    private static ArcaneMissileSettings Settings(float speed, float turnDegrees)
    {
        ArcaneMissileSettings settings = ArcaneMissileSettings.CreateDefault();
        settings.speedCellsPerSecond = speed;
        settings.maxTurnDegreesPerSecond = turnDegrees;
        settings.impactDef = CountingImpact;
        settings.hitRadius = 0.45f;
        settings.flightHeight = 1.2f;
        settings.wobbleAmplitude = 0f;

        // Production flag: skip only the lifetime/range checks. Out-of-bounds still ends a run, so
        // the trajectory is not truncated artificially and no Clamp() bypass is needed.
        settings.ignoreFlightLimits = true;
        settings.expireOnTargetLost = false;
        return settings;
    }

    private static RunResult Run(
        float distance,
        float speed,
        float turnDegrees,
        float pawnSpeed,
        float delaySeconds,
        MoveMode mode,
        float capSeconds,
        bool stopPassed = false)
    {
        Map map = NewMap();
        Pawn pawn = NewPawn(map, Center + distance, Center);

        var missile = new ArcaneMissile();
        missile.Initialize(
            null,
            pawn,
            pawn.Position,
            Center + 0.55f,
            Center,
            1f,
            0f,
            Settings(speed, turnDegrees),
            1234);

        var result = new RunResult();
        int delayTicks = (int)MathF.Round(delaySeconds * Tps);
        int capTicks = (int)(capSeconds * Tps);
        float px = Center + distance;
        float pz = Center;
        float previousDistance = float.MaxValue;
        bool wasClosing = false;

        ExperimentLog.Entries.Clear();

        for (int tick = 0; tick < capTicks; tick++)
        {
            bool freeToMove = !(stopPassed && result.HitAfterPass);

            if (tick >= delayTicks && mode != MoveMode.Stationary && freeToMove)
            {
                if (mode == MoveMode.Lateral)
                {
                    pz += pawnSpeed / Tps;
                }
                else
                {
                    px += pawnSpeed / Tps;
                }

                if (px > MapSize - 1f || pz > MapSize - 1f)
                {
                    result.TargetLeftMap = true;
                    break;
                }

                MovePawn(map, pawn, px, pz);
            }

            ArcaneMissileEnd end = missile.Tick(map);
            result.Ticks = tick + 1;

            float dx = missile.posX - px;
            float dz = missile.posZ - pz;
            float distanceNow = MathF.Sqrt(dx * dx + dz * dz);
            if (distanceNow < result.MinDistance)
            {
                result.MinDistance = distanceNow;
            }

            if (wasClosing && distanceNow > previousDistance + 0.0001f)
            {
                result.HitAfterPass = true;
            }

            wasClosing = distanceNow < previousDistance - 0.0001f;
            previousDistance = distanceNow;

            if (end != ArcaneMissileEnd.Flying)
            {
                result.Ended = true;
                result.Hit = end == ArcaneMissileEnd.Impact && ReferenceEquals(missile.lastHit, pawn);
                result.Reason = LastReason();
                result.FinalGap = distanceNow;
                return result;
            }
        }

        result.FinalGap = previousDistance;
        result.Reason = "cap";
        return result;
    }

    private static string LastReason()
    {
        if (ExperimentLog.Entries.Count == 0)
        {
            return "";
        }

        string last = ExperimentLog.Entries[ExperimentLog.Entries.Count - 1];
        if (last.Contains("outOfBounds"))
        {
            return "outOfBounds";
        }

        if (last.Contains("lifetime"))
        {
            return "lifetime";
        }

        if (last.Contains("range"))
        {
            return "range";
        }

        return "impact";
    }

    private static void Main()
    {
        Console.WriteLine("PS target: Human MoveSpeed 4.6 -> floor " + FloorSpeed.ToString("F4")
            + " cells/s, soil " + SoilSpeed.ToString("F4") + " cells/s (terrain cost 2).");

        PrintRadiusTable();
        PrintA();
        PrintB();
    }

    private static void PrintRadiusTable()
    {
        Console.WriteLine();
        Console.WriteLine("turn radius at 13 cells/s (r = v / omega):");
        foreach (float turn in new[] { 5f, 8f, 10f, 12f, 15f })
        {
            float omega = turn * MathF.PI / 180f;
            Console.WriteLine("  " + turn.ToString("F0").PadLeft(2) + " deg/s -> r="
                + (13f / omega).ToString("F1") + " cells, half-turn=" + (MathF.PI * 13f / omega).ToString("F1") + " cells");
        }

        Console.WriteLine("B turn radius at 720 deg/s: 3 cells/s -> "
            + (3f / (720f * MathF.PI / 180f)).ToString("F2") + " cells, 4 cells/s -> "
            + (4f / (720f * MathF.PI / 180f)).ToString("F2") + " cells");
    }

    private static void PrintA()
    {
        var turns = new[] { 5f, 8f, 10f, 12f, 15f };
        var distances = new[] { 10f, 15f, 20f, 25f };
        var speeds = new[] { SoilSpeed, FloorSpeed };
        var delays = new[] { 0f, 0.25f, 0.5f };

        Console.WriteLine();
        Console.WriteLine("A) explosive, speed 13, 250x250 map, no lifetime/range cap");

        Console.WriteLine("A0 baseline stationary target (hits/2 speeds):");
        foreach (float turn in turns)
        {
            string line = "  turn " + turn.ToString("F0").PadLeft(2) + ": ";
            foreach (float distance in distances)
            {
                int hits = 0;
                foreach (float speed in speeds)
                {
                    if (Run(distance, 13f, turn, 0f, 0f, MoveMode.Stationary, 10f).Hit)
                    {
                        hits++;
                    }
                }

                line += "D" + distance.ToString("F0") + "=" + hits + "/2  ";
            }

            Console.WriteLine(line);
        }

        Console.WriteLine("A1 lateral dodge, hits/6 (2 speeds x 3 delays), exits=left map, loop=stayed in map:");
        Console.WriteLine("turn  D   hits  exits  loop  min-miss  reacquire");
        foreach (float turn in turns)
        {
            foreach (float distance in distances)
            {
                int hits = 0;
                int exits = 0;
                int loops = 0;
                int reacquire = 0;
                float minMiss = float.MaxValue;

                foreach (float speed in speeds)
                {
                    foreach (float delay in delays)
                    {
                        RunResult r = Run(distance, 13f, turn, speed, delay, MoveMode.Lateral, 20f);
                        if (r.Hit)
                        {
                            hits++;
                            if (r.HitAfterPass)
                            {
                                reacquire++;
                            }
                        }
                        else if (r.MinDistance < minMiss)
                        {
                            minMiss = r.MinDistance;
                        }

                        if (r.Reason == "outOfBounds")
                        {
                            exits++;
                        }
                        else if (!r.Hit)
                        {
                            loops++;
                        }
                    }
                }

                Console.WriteLine(turn.ToString("F0").PadLeft(3) + " " + distance.ToString("F0").PadLeft(3)
                    + "  " + hits.ToString().PadLeft(3) + "  " + exits.ToString().PadLeft(3)
                    + "  " + loops.ToString().PadLeft(3) + "  "
                    + (hits == 6 ? "n/a" : minMiss.ToString("F2")).PadLeft(8) + "  " + reacquire);
            }
        }

        Console.WriteLine("A2 dodge then target stops at first pass (loop-back test), hits/2 speeds:");
        foreach (float turn in turns)
        {
            string line = "  turn " + turn.ToString("F0").PadLeft(2) + ": ";
            foreach (float distance in distances)
            {
                int hits = 0;
                int oob = 0;
                string ticks = "";
                foreach (float speed in speeds)
                {
                    RunResult r = Run(distance, 13f, turn, speed, 0f, MoveMode.Lateral, 60f, stopPassed: true);
                    if (r.Hit)
                    {
                        hits++;
                        ticks = r.Ticks.ToString();
                    }
                    else if (r.Reason == "outOfBounds")
                    {
                        oob++;
                    }
                }

                line += "D" + distance.ToString("F0") + "=" + hits + "/2";
                if (oob > 0)
                {
                    line += "(oob" + oob + ")";
                }

                if (hits > 0)
                {
                    line += "@" + ticks + "t";
                }

                line += "  ";
            }

            Console.WriteLine(line);
        }

        Console.WriteLine("A3 D=20, moving target but NOT juking (hit=yes/no):");
        Console.WriteLine("turn  lateral1.5  away3.99  away4.6");
        foreach (float turn in turns)
        {
            bool lateral = Run(20f, 13f, turn, 1.5f, 0f, MoveMode.Lateral, 20f).Hit;
            bool awaySoil = Run(20f, 13f, turn, SoilSpeed, 0f, MoveMode.Away, 20f).Hit;
            bool awayFloor = Run(20f, 13f, turn, FloorSpeed, 0f, MoveMode.Away, 20f).Hit;
            Console.WriteLine(turn.ToString("F0").PadLeft(3) + "  " + (lateral ? "yes" : "no ").PadLeft(10)
                + "  " + (awaySoil ? "yes" : "no ").PadLeft(8) + "  " + (awayFloor ? "yes" : "no"));
        }

        Console.WriteLine("A4 lateral 1.5 cells/s (walking, not juking) at D=10/15:");
        foreach (float turn in turns)
        {
            bool d10 = Run(10f, 13f, turn, 1.5f, 0f, MoveMode.Lateral, 20f).Hit;
            bool d15 = Run(15f, 13f, turn, 1.5f, 0f, MoveMode.Lateral, 20f).Hit;
            Console.WriteLine("  turn " + turn.ToString("F0").PadLeft(2) + ": D10=" + (d10 ? "hit" : "miss")
                + "  D15=" + (d15 ? "hit" : "miss"));
        }
    }

    private static void PrintB()
    {
        Console.WriteLine();
        Console.WriteLine("B) healer, turn 720 deg/s, target straight away from 10 cells");
        Console.WriteLine("vm   vt      hit    ticks  final-gap  closing(vm-vt)  close-time(10 cells)");

        foreach (float speed in new[] { 3f, 4f })
        {
            foreach (float target in new[] { 0f, 2f, SoilSpeed, FloorSpeed })
            {
                RunResult r = Run(10f, speed, 720f, target, 0f, MoveMode.Away, 120f);
                float closing = speed - target;
                string closeTime = closing > 0.0001f ? (10f / closing).ToString("F1") + "s" : "never";
                Console.WriteLine(speed.ToString("F0") + "    " + target.ToString("F3") + "   "
                    + (r.Hit ? "yes" : "no ").PadLeft(4) + "   " + r.Ticks.ToString().PadLeft(5) + "  "
                    + r.FinalGap.ToString("F2").PadLeft(9) + "  " + closing.ToString("F3").PadLeft(12) + "  " + closeTime);
            }
        }

        Console.WriteLine();
        Console.WriteLine("B lateral dodge (4.6 cells/s) from 10 cells:");
        foreach (float speed in new[] { 3f, 4f })
        {
            RunResult r = Run(10f, speed, 720f, FloorSpeed, 0f, MoveMode.Lateral, 60f);
            Console.WriteLine("  vm=" + speed.ToString("F0") + " hit=" + r.Hit + " ticks=" + r.Ticks
                + " minDist=" + r.MinDistance.ToString("F2") + " reason=" + r.Reason);
        }
    }
}
