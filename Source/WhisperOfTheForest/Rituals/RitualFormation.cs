using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Where each participant stands. The chalk border is the only place that counts as "on the
    /// circle", so every participant gets one distinct border cell, nearest first. Vanilla picks
    /// standable ritual spots the same way - CompPsychicRitualSpot.cs:86 skips any cell whose
    /// passability is not Standable - which is also why chalk marks are Standable Defs.
    /// </summary>
    public static class RitualFormation
    {
        /// <summary>
        /// One station per participant, host first. A pawn that cannot reach any free border cell is
        /// reported through <paramref name="problem"/> instead of silently getting no station.
        /// </summary>
        public static List<RitualStation> AssignStations(Map map, IntVec3 center, int ringSize,
            Pawn host, IList<Pawn> helpers, out string problem)
        {
            problem = null;
            var stations = new List<RitualStation>();
            var free = new List<IntVec3>();
            foreach (IntVec3 cell in RitualRing.Cells(center, ringSize))
            {
                if (cell.InBounds(map) && cell.Standable(map))
                {
                    free.Add(cell);
                }
            }

            var order = new List<Pawn>(1 + helpers.Count);
            order.Add(host);
            order.AddRange(helpers);
            for (int i = 0; i < order.Count; i++)
            {
                Pawn pawn = order[i];
                if (pawn == null)
                {
                    continue;
                }

                IntVec3 best = IntVec3.Invalid;
                int bestDistance = int.MaxValue;
                for (int c = 0; c < free.Count; c++)
                {
                    IntVec3 cell = free[c];
                    if (!pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                    {
                        continue;
                    }

                    int distance = (cell - pawn.Position).LengthHorizontalSquared;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = cell;
                    }
                }

                if (best == IntVec3.Invalid)
                {
                    problem = "WotF_Ritual_NoStandingCell".Translate(pawn.LabelShort);
                    continue;
                }

                free.Remove(best);
                stations.Add(new RitualStation { Pawn = pawn, Cell = best });
            }

            return stations;
        }
    }
}
