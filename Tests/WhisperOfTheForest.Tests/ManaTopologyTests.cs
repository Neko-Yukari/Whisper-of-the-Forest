using System;
using System.Collections.Generic;
using Verse;
using WhisperOfTheForest.Mana;

namespace WhisperOfTheForest.Tests
{
    /// <summary>
    /// Seam: which nodes share one network decides which batteries a workbench, turret, altar or
    /// gate can draw from (MapComponent_ManaNetwork builds its networks from this grouping).
    /// </summary>
    internal static class ManaTopologyTests
    {
        internal static IEnumerable<(string, Action)> All => new (string, Action)[]
        {
            ("mana: links chain across nodes, a far node stays alone", ChainAndIsland),
            ("mana: the smaller radius must reach", () => Linked(false, 5, 10f, 4f)),
            ("mana: both radii reaching links", () => Linked(true, 5, 10f, 5f)),
        };

        private static void ChainAndIsland()
        {
            int[] group = ManaTopology.Group(
                new[] { new IntVec3(0, 0, 0), new IntVec3(4, 0, 0), new IntVec3(8, 0, 0), new IntVec3(30, 0, 0) },
                new[] { 5f, 5f, 5f, 5f });
            Check.Equal("0,0,0,1", string.Join(",", group), "groups");
        }

        private static void Linked(bool expected, int distance, float radiusA, float radiusB)
        {
            int[] group = ManaTopology.Group(
                new[] { new IntVec3(0, 0, 0), new IntVec3(0, 0, distance) },
                new[] { radiusA, radiusB });
            Check.Equal(expected, group[0] == group[1], $"distance {distance}, radii {radiusA}/{radiusB}");
        }
    }
}
