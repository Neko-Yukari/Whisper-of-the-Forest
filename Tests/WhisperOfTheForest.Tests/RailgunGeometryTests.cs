using System;
using System.Collections.Generic;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Tests
{
    /// <summary>
    /// Seam: the corridor the railgun settles (RailgunCastService) and the beam the effect draws come
    /// from the same geometry.
    /// </summary>
    internal static class RailgunGeometryTests
    {
        internal static IEnumerable<(string, Action)> All => new (string, Action)[]
        {
            ("railgun: aiming at the caster's own cell is no direction", () => Check.True(!RailgunGeometry.TryDirection(3f, 3f, 3f, 3f, out _, out _), "coincident points")),
            ("railgun: the beam stops at the map edge", () => Length(true, 5f, 5f, 5f, 1f, 0f, 100f)),
            ("railgun: the beam is never longer than its range", () => Length(true, 3f, 5f, 5f, 1f, 0f, 3f)),
            ("railgun: a ray leaving the map at once has no length", () => Length(false, 0f, 10f, 5f, 1f, 0f, 100f)),
            ("railgun: within half the width is hit", () => Corridor(true, 4f, 0.5f)),
            ("railgun: beyond half the width is missed", () => Corridor(false, 4f, 0.6f)),
            ("railgun: behind the caster is missed", () => Corridor(false, -1f, 0f)),
            ("railgun: past the end is missed", () => Corridor(false, 11f, 0f)),
        };

        private static void Length(bool expected, float expectedLength, float x, float z, float dirX, float dirZ, float range)
        {
            bool ok = RailgunGeometry.TryLength(x, z, dirX, dirZ, range, 10, 10, out float length);
            Check.Equal(expected, ok, "has length");
            Check.Equal(expectedLength, length, "length");
        }

        // Corridor along +x from the origin, 10 cells long and 1 cell wide.
        private static void Corridor(bool expected, float along, float sideways)
        {
            bool hit = RailgunGeometry.WithinCorridor(0f, 0f, 1f, 0f, along, sideways, 0.5f, 10f, out _);
            Check.Equal(expected, hit, $"point ({along},{sideways})");
        }
    }
}
