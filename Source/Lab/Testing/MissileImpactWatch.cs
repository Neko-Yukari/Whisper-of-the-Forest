using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Verse;
using WhisperOfTheForest.Missiles;

namespace WhisperOfTheForest.Testing
{
    /// <summary>
    /// Lab-only read-out of where arcane darts actually landed since this map loaded. Several reload
    /// tests park their scenes in one save, so the map-wide impact counter cannot say which scene a
    /// hit belongs to; a test filters this by its own arena rectangle instead.
    /// </summary>
    public sealed class MapComponent_MissileImpactWatch : MapComponent
    {
        private readonly Dictionary<IntVec3, int> impacts = new Dictionary<IntVec3, int>();

        public MapComponent_MissileImpactWatch(Map map) : base(map)
        {
        }

        internal void Record(IntVec3 cell)
        {
            impacts.TryGetValue(cell, out int count);
            impacts[cell] = count + 1;
        }

        /// <summary>Impact resolutions whose cell lies inside <paramref name="area"/>.</summary>
        public int CountIn(CellRect area)
        {
            return impacts.Where(pair => area.Contains(pair.Key)).Sum(pair => pair.Value);
        }

        public string Locations => string.Join("; ", impacts.Select(pair => pair.Key + "=" + pair.Value));
    }

    [HarmonyPatch(typeof(ArcaneMissile), "Impact")]
    internal static class Patch_LabMissileImpact
    {
        private static void Postfix(Map map, IntVec3 cell)
        {
            map.GetComponent<MapComponent_MissileImpactWatch>()?.Record(cell);
        }
    }
}
