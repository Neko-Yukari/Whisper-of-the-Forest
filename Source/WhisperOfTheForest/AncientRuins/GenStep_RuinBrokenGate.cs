using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Teleport;

namespace WhisperOfTheForest.AncientRuins
{
    /// <summary>
    /// The broken gate every ruin carries (设定/05). It stands on the ruin's own map, off any mana
    /// network, and is the only far end the colony gate can reach — the connection home is made from
    /// its menu once the party is ready to leave.
    ///
    /// It goes on the ruin's outskirts, on open ground: at least <see cref="minDistanceFromTemple"/>
    /// cells from the temple (GenStep_RuinTemple.TempleRectVar) and <see cref="minDistanceFromGuardArea"/>
    /// from the ground the guards hold (GenStep_RuinMechanoids.GuardAreaVar), with at least
    /// <see cref="minLandingCells"/> standable, mutually connected cells inside its own arrivalRadius to
    /// land a party on. A party that crosses in therefore arrives outside the fight and walks in, instead
    /// of landing in the room it has to fight through. The ruin's loot search (temple, then guards' area,
    /// then anywhere) is deliberately not reused here: it put the gate inside the temple.
    ///
    /// The spacing is relaxed in steps when nothing on the map satisfies it — 3/4, 1/2 and 1/4 of the
    /// configured distance, then none — and each relaxed pick is logged at Info. A map with no open
    /// ground at any step is an Error, and the ruin generates without a gate (as before).
    ///
    /// Where the spacing allows a whole ring of open ground, the gate takes the cell closest to the
    /// temple: it should stand just outside the fight, not out on the map's far edge with a long walk
    /// in. Cells at the same distance are picked at random, so ruins still differ.
    /// </summary>
    public class GenStep_RuinBrokenGate : GenStep
    {
        public ThingDef gate;

        /// <summary>Cells of open ground wanted between the gate and the temple's rectangle.</summary>
        public float minDistanceFromTemple = 22f;

        /// <summary>Cells of open ground wanted between the gate and the area the guards are spawned in.</summary>
        public float minDistanceFromGuardArea = 14f;

        /// <summary>Standable, connected cells wanted inside the gate's arrivalRadius, for the party to land on.</summary>
        public int minLandingCells = 24;

        /// <summary>Fractions of the configured spacing tried in order; the last one asks for no spacing at all.</summary>
        private static readonly float[] SpacingRelaxation = { 1f, 0.75f, 0.5f, 0.25f, 0f };

        public override int SeedPart => 1773640021;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (gate == null)
            {
                WotfLog.Error(WotfLog.ModuleRuin, "the broken-gate step has no gate def; the ruin generated without one");
                return;
            }

            CompProperties_TeleportGate props = gate.GetCompProperties<CompProperties_TeleportGate>();
            if (props == null)
            {
                WotfLog.Error(WotfLog.ModuleRuin, "the broken-gate step's gate (" + gate.defName
                    + ") has no CompProperties_TeleportGate; the ruin generated without a gate");
                return;
            }

            // Both are published by the steps that ran before this one (temple 420, guards 430, gate 441).
            TryGetRect(GenStep_RuinTemple.TempleRectVar, out CellRect temple);
            TryGetRect(GenStep_RuinMechanoids.GuardAreaVar, out CellRect guards);
            float radius = props.arrivalRadius;
            List<Candidate> candidates = BuildCandidates(map, temple, guards);

            foreach (float factor in SpacingRelaxation)
            {
                float fromTemple = minDistanceFromTemple * factor;
                float fromGuards = minDistanceFromGuardArea * factor;
                if (!TryFindCell(map, candidates, fromTemple, fromGuards, radius, temple, guards, out IntVec3 cell,
                        out CellRect footprint, out float templeGap, out float guardGap, out int landingCells))
                {
                    continue;
                }

                if (factor < 1f)
                {
                    WotfLog.Info(WotfLog.ModuleRuin, "no open ground kept the broken gate "
                        + minDistanceFromTemple.ToString("0.#") + "/" + minDistanceFromGuardArea.ToString("0.#")
                        + " cells away from the temple and the guards at tile " + map.Tile.tileId
                        + "; relaxed to " + (factor * 100f).ToString("0") + "%");
                }

                GenSpawn.Spawn(ThingMaker.MakeThing(gate), cell, map);
                WotfLog.Info(WotfLog.ModuleRuin, "the ruin's broken gate stands at " + cell + " (footprint "
                    + footprint + ") on map " + map.uniqueID + ": " + templeGap.ToString("0.#")
                    + " cells from the temple " + temple + ", " + guardGap.ToString("0.#")
                    + " cells from the guards' area " + guards + ", " + landingCells
                    + " landing cells within " + radius.ToString("0.#"));
                return;
            }

            WotfLog.Error(WotfLog.ModuleRuin, "the broken gate had nowhere to stand in the ruin at tile "
                + map.Tile.tileId + ": no cell kept " + minDistanceFromTemple.ToString("0.#")
                + "/" + minDistanceFromGuardArea.ToString("0.#") + " cells of spacing (relaxed to none) while also"
                + " offering " + minLandingCells + " landing cells within " + radius.ToString("0.#")
                + "; the ruin generated without one");
        }

        private static bool TryGetRect(string name, out CellRect rect)
        {
            if (MapGenerator.TryGetVar(name, out rect) && !rect.IsEmpty)
            {
                return true;
            }

            rect = CellRect.Empty;
            return false;
        }

        /// <summary>
        /// Every cell on the map with the two gaps the spacing rules ask about, ordered by how close the
        /// gate would stand to the temple — so the first cell that passes a spacing step is the nearest one.
        /// The map order is shuffled first, so cells at the same distance come out in random order.
        /// </summary>
        private List<Candidate> BuildCandidates(Map map, CellRect temple, CellRect guards)
        {
            Rot4 rot = gate.defaultPlacingRot;
            var candidates = new List<Candidate>();
            foreach (IntVec3 cell in map.AllCells.InRandomOrder())
            {
                CellRect rect = GenAdj.OccupiedRect(cell, rot, gate.Size);
                float gapTemple = temple.IsEmpty ? float.MaxValue : Gap(rect, temple);
                float gapGuards = guards.IsEmpty ? float.MaxValue : Gap(rect, guards);
                candidates.Add(new Candidate
                {
                    cell = cell,
                    rect = rect,
                    templeGap = gapTemple,
                    guardGap = gapGuards,
                    preference = !temple.IsEmpty ? gapTemple
                        : (!guards.IsEmpty ? gapGuards : cell.DistanceTo(map.Center)),
                });
            }

            candidates.Sort((a, b) => a.preference.CompareTo(b.preference));
            return candidates;
        }

        /// <summary>
        /// The first cell in preference order that stands clear of both rectangles, fits the gate and leaves
        /// enough connected standing room beside it: the cell closest to the temple that keeps the spacing.
        /// Distances come back for the log.
        /// </summary>
        private bool TryFindCell(Map map, List<Candidate> candidates, float fromTemple, float fromGuards,
            float radius, CellRect temple, CellRect guards, out IntVec3 cell, out CellRect footprint,
            out float templeGap, out float guardGap, out int landingCells)
        {
            foreach (Candidate candidate in candidates)
            {
                if (candidate.templeGap < fromTemple || candidate.guardGap < fromGuards)
                {
                    continue;
                }

                if (!GenSpawn.CanSpawnAt(gate, candidate.cell, map))
                {
                    continue;
                }

                int landing = CountLandingCells(map, candidate.rect, radius);
                if (landing < minLandingCells)
                {
                    continue;
                }

                cell = candidate.cell;
                footprint = candidate.rect;
                templeGap = temple.IsEmpty ? -1f : candidate.templeGap;
                guardGap = guards.IsEmpty ? -1f : candidate.guardGap;
                landingCells = landing;
                return true;
            }

            cell = IntVec3.Invalid;
            footprint = CellRect.Empty;
            templeGap = -1f;
            guardGap = -1f;
            landingCells = 0;
            return false;
        }

        /// <summary>One cell the gate could stand on, with the two gaps the spacing rules ask about.</summary>
        private struct Candidate
        {
            public IntVec3 cell;
            public CellRect rect;
            public float templeGap;
            public float guardGap;

            /// <summary>What the pick minimises: how close the gate stands to the temple.</summary>
            public float preference;
        }

        /// <summary>Gap between two rectangles: 0 when they touch or overlap, otherwise the corner-to-corner distance.</summary>
        private static float Gap(CellRect a, CellRect b)
        {
            int dx = Mathf.Max(0, Mathf.Max(a.minX - b.maxX, b.minX - a.maxX));
            int dz = Mathf.Max(0, Mathf.Max(a.minZ - b.maxZ, b.minZ - a.maxZ));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Standable cells inside the arrival radius that a person standing beside the gate can walk to
        /// without leaving that radius: the place the party actually lands on. Counted with a four-way
        /// flood fill so a sealed corner or an island inside the radius counts as nothing.
        /// </summary>
        private static int CountLandingCells(Map map, CellRect footprint, float radius)
        {
            IntVec3 center = footprint.CenterCell;
            var reached = new HashSet<IntVec3>();
            var queue = new Queue<IntVec3>();
            foreach (IntVec3 seed in footprint.ExpandedBy(1).Cells)
            {
                if (Reachable(seed, center, footprint, radius, map) && reached.Add(seed))
                {
                    queue.Enqueue(seed);
                }
            }

            while (queue.Count > 0)
            {
                IntVec3 here = queue.Dequeue();
                foreach (IntVec3 step in GenAdj.CardinalDirections)
                {
                    IntVec3 next = here + step;
                    if (Reachable(next, center, footprint, radius, map) && reached.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return reached.Count;
        }

        private static bool Reachable(IntVec3 cell, IntVec3 center, CellRect footprint, float radius, Map map)
        {
            return cell.InBounds(map)
                   && !footprint.Contains(cell)
                   && cell.DistanceTo(center) <= radius
                   && cell.Standable(map);
        }
    }
}
