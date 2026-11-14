using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.AncientRuins
{
    /// <summary>
    /// The ruin's main building: vanilla's ancient temple, placed on a map the player travels to.
    /// It reuses <see cref="GenStep_ScatterShrines"/> wholesale — placement, the ancientTemple symbol,
    /// its loot table and its cryptosleep pods — and only removes the two guards that assume "this map
    /// is a colony or an event map": the 75% skip for non-starting maps (GenStep_ScatterShrines.cs:21-28)
    /// and the requirement that natural rock touch the footprint (:30-50). Our maps are neither, and a
    /// quarter chance of no ruin at all is not acceptable for a site the player spent a rite on.
    ///
    /// The temple does not need Ideology: SymbolResolver_AncientTemple (RimWorld.BaseGen) is Core, and
    /// SymbolResolver_Interior_AncientTemple guards its two Ideology extras with ModsConfig.IdeologyActive.
    /// </summary>
    public class GenStep_RuinTemple : GenStep_ScatterShrines
    {
        /// <summary>Where the temple landed, so the guard and loot steps can use the same rectangle.</summary>
        public const string TempleRectVar = "WotF_RuinTempleRect";

        /// <summary>Footprint of the temple. The two ruin types differ here and nowhere else in this step.</summary>
        public IntRange templeSize = new IntRange(15, 20);

        private IntVec2 footprint;

        protected override bool ShouldSkipMap(Map map)
        {
            return false;
        }

        /// <summary>
        /// Near the middle of the map first, for the look, then every cell of the map once in random
        /// order. Vanilla's centre search samples a few hundred random cells (RCellFinder.cs:965-1014,
        /// growing the radius around map.Center); a 19~24 footprint on a rocky or wet map often fits
        /// only somewhere that search never samples, and a ruin without its temple is not the ruin the
        /// rite promised. The acceptance conditions are the same in both passes (CanScatterAt).
        /// </summary>
        protected override bool TryFindScatterCell(Map map, out IntVec3 result)
        {
            footprint = new IntVec2(templeSize.RandomInRange, templeSize.RandomInRange);

            if (RCellFinder.TryFindRandomCellNearTheCenterOfTheMapWith(cell => CanScatterAt(cell, map), map, out result))
            {
                return true;
            }

            // Any cell whose footprint is fully on the map has its centre at least half a footprint plus
            // one cell away from every edge, so the scan below never hands CanScatterAt a clipped rect.
            int margin = Mathf.Max(footprint.x, footprint.z) / 2 + 1;
            var candidates = new List<IntVec3>();
            foreach (IntVec3 cell in map.AllCells.InRandomOrder())
            {
                if (cell.x < margin || cell.z < margin || cell.x >= map.Size.x - margin || cell.z >= map.Size.z - margin)
                {
                    continue;
                }

                // Cheap reject first: one terrain affordance lookup instead of a footprint-sized scan.
                if (!cell.SupportsStructureType(map, TerrainAffordanceDefOf.Heavy))
                {
                    continue;
                }

                candidates.Add(cell);
            }

            foreach (IntVec3 cell in candidates)
            {
                if (!CanScatterAt(cell, map))
                {
                    continue;
                }

                result = cell;
                return true;
            }

            result = IntVec3.Invalid;
            return false;
        }

        protected override CellRect EffectiveRectAt(IntVec3 c)
        {
            return CellRect.CenteredOn(c, footprint.x, footprint.z);
        }

        /// <summary>
        /// The checks from GenStep_ScatterRuinsSimple that still apply, minus the rock condition: the
        /// footprint has to fit, sit on heavy terrain and stay inside the map, and it must not overlap
        /// anything already placed (CanPlaceAncientBuildingInRange reads UsedRects).
        /// </summary>
        protected override bool CanScatterAt(IntVec3 c, Map map)
        {
            if (footprint.x <= 0 || footprint.z <= 0)
            {
                return false;
            }

            CellRect rect = EffectiveRectAt(c).ClipInsideMap(map);
            if (rect.Width != footprint.x || rect.Height != footprint.z)
            {
                return false;
            }

            // Cheapest first: the UsedRects overlap test is a few rectangle comparisons, the terrain loop
            // is one affordance lookup per footprint cell, and the whole-map scan below may call this
            // tens of thousands of times.
            if (!CanPlaceAncientBuildingInRange(rect, map))
            {
                return false;
            }

            foreach (IntVec3 cell in rect.Cells)
            {
                if (!cell.SupportsStructureType(map, TerrainAffordanceDefOf.Heavy))
                {
                    return false;
                }
            }

            return true;
        }

        protected override void ScatterAt(IntVec3 loc, Map map, GenStepParams parms, int stackCount = 1)
        {
            CellRect rect = EffectiveRectAt(loc).ClipInsideMap(map);
            base.ScatterAt(loc, map, parms, stackCount);

            // Only publish the rectangle when the temple is really there: the vanilla resolver gives up
            // silently when the footprint already holds a cryptosleep casket or fails its own checks.
            if (rect.Cells.Any(cell => cell.GetEdifice(map)?.def == ThingDefOf.Wall))
            {
                MapGenerator.SetVar(TempleRectVar, rect);
                return;
            }

            WotfLog.Warning(WotfLog.ModuleRuin, "the ancient temple did not generate at " + loc
                + "; the map gets the ruin without its main building");
        }
    }
}
