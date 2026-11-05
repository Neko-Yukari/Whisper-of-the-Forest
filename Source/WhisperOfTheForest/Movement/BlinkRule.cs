using RimWorld;
using Verse;

namespace WhisperOfTheForest.Movement
{
    /// <summary>
    /// Legality rules for a single blink hop. A hop may only target the pawn's own next
    /// path node, so the route itself (reachability, forbidden cells, danger) is still
    /// decided by the vanilla pathfinder. These checks only reject nodes that stopped
    /// being legal after the path was calculated, and forbid corner cutting.
    /// </summary>
    internal static class BlinkRule
    {
        /// <summary>Returns null when the hop is legal, otherwise a short block reason.</summary>
        internal static string BlockReason(Pawn pawn, IntVec3 from, IntVec3 to)
        {
            Map map = pawn.Map;
            if (map == null)
            {
                return "no-map";
            }

            if (!to.InBounds(map))
            {
                return "out-of-bounds";
            }

            IntVec3 delta = to - from;
            if (delta.x < -1 || delta.x > 1 || delta.z < -1 || delta.z > 1)
            {
                return "not-adjacent";
            }

            if (delta.x == 0 && delta.z == 0)
            {
                return "zero-hop";
            }

            // Doors keep vanilla open/close bookkeeping: never blink out of or into a door cell.
            if (GridsUtility.GetDoor(from, map) != null)
            {
                return "leaving-door-cell";
            }

            if (GridsUtility.GetDoor(to, map) != null)
            {
                return "door-cell";
            }

            if (!GenGrid.WalkableBy(to, map, pawn))
            {
                return "not-walkable";
            }

            if (delta.x != 0 && delta.z != 0)
            {
                IntVec3 sideX = new IntVec3(from.x + delta.x, 0, from.z);
                IntVec3 sideZ = new IntVec3(from.x, 0, from.z + delta.z);
                bool sideXOpen = GenGrid.WalkableBy(sideX, map, pawn);
                bool sideZOpen = GenGrid.WalkableBy(sideZ, map, pawn);
                if (!sideXOpen && !sideZOpen)
                {
                    return "diagonal-corner";
                }

                // Same rule vanilla uses for two pass-through-only neighbours: no diagonal squeeze.
                if (sideXOpen && sideZOpen
                    && GridsUtility.GetDoor(sideX, map) != null
                    && GridsUtility.GetDoor(sideZ, map) != null)
                {
                    return "diagonal-door-squeeze";
                }
            }

            if (PawnUtility.PawnBlockingPathAt(to, pawn, false, false, false, false) != null)
            {
                return "pawn-occupied";
            }

            return null;
        }
    }
}
