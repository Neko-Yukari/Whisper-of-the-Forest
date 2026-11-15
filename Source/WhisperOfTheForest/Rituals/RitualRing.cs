using System;
using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// A ritual ring is the square border of chalk marks around the altar. Only the border counts:
    /// the altar itself and the interior are free, so the player can stand inside the ring.
    /// </summary>
    public static class RitualRing
    {
        /// <summary>
        /// The border cells that still carry no mark of this colour, in reading order. The one rule
        /// the start check, the inspect line and the ritual panel all read, so they cannot disagree
        /// about which cells are missing.
        /// </summary>
        public static List<IntVec3> MissingCells(Map map, IntVec3 center, int size, ChalkColor color)
        {
            var missing = new List<IntVec3>();
            int radius = (size - 1) / 2;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius)
                    {
                        continue;
                    }

                    IntVec3 cell = center + new IntVec3(dx, 0, dz);
                    if (!cell.InBounds(map) || !HasMark(map, cell, color))
                    {
                        missing.Add(cell);
                    }
                }
            }

            return missing;
        }

        /// <summary>Every border cell of a ring of this size, in reading order, chalk or not.</summary>
        public static List<IntVec3> Cells(IntVec3 center, int size)
        {
            var cells = new List<IntVec3>();
            int radius = (size - 1) / 2;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) == radius)
                    {
                        cells.Add(center + new IntVec3(dx, 0, dz));
                    }
                }
            }

            return cells;
        }

        /// <summary>True when the cell lies on the outer border of a ring of this size.</summary>
        public static bool IsOnRing(IntVec3 center, int size, IntVec3 cell)
        {
            int radius = (size - 1) / 2;
            IntVec3 delta = cell - center;
            return Math.Max(Math.Abs(delta.x), Math.Abs(delta.z)) == radius;
        }

        private static bool HasMark(Map map, IntVec3 cell, ChalkColor color)
        {
            List<Thing> things = map.thingGrid.ThingsListAt(cell);
            for (int i = 0; i < things.Count; i++)
            {
                CompChalkMark mark = things[i].TryGetComp<CompChalkMark>();
                if (mark != null && mark.Color == color)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
