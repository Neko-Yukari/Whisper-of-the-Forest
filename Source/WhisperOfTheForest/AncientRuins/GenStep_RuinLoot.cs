using RimWorld;
using Verse;

namespace WhisperOfTheForest.AncientRuins
{
    /// <summary>
    /// The ruin's own loot: one ThingSetMakerDef spread on the ground, and optionally the relic casket.
    /// Both prefer the temple footprint so the loot sits in the ruin instead of in the open. When the
    /// temple is not there, they fall back to the area the guard step defends
    /// (SiteGenStepUtility.TryFindRootToSpawnAroundRectOfInterest: "RectOfInterest", or a room of at
    /// least 225 cells near the map centre), so the casket still sits with its guardians instead of in
    /// a random corner. Everything is data: which ruin gets a casket, and which table feeds the floor,
    /// is decided in the GenStepDef.
    /// </summary>
    public class GenStep_RuinLoot : GenStep
    {
        public ThingSetMakerDef loot;

        public ThingDef relicCasket;

        public override int SeedPart => 1307619982;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (loot != null)
            {
                foreach (Thing thing in loot.root.Generate(default(ThingSetMakerParams)))
                {
                    if (!TryFindCell(map, cell => cell.Standable(map), out IntVec3 cell))
                    {
                        WotfLog.Warning(WotfLog.ModuleRuin, "the map had no free cell for " + thing.def.defName
                            + "; that piece of ruin loot was dropped");
                        thing.Destroy();
                        continue;
                    }

                    GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near, out Thing _);
                }
            }

            if (relicCasket == null)
            {
                return;
            }

            if (!TryFindCell(map, cell => cell.Standable(map) && GenSpawn.CanSpawnAt(relicCasket, cell, map),
                    out IntVec3 casketCell))
            {
                WotfLog.Error(WotfLog.ModuleRuin, "the relic casket had nowhere to stand in the ruin at tile "
                    + map.Tile + "; the core ruin generated without its casket");
                return;
            }

            GenSpawn.Spawn(ThingMaker.MakeThing(relicCasket), casketCell, map);
        }

        /// <summary>Temple interior first, then the guards' area, then anywhere on the map.</summary>
        private static bool TryFindCell(Map map, System.Predicate<IntVec3> validator, out IntVec3 cell)
        {
            if (MapGenerator.TryGetVar(GenStep_RuinTemple.TempleRectVar, out CellRect temple)
                && !temple.IsEmpty
                && TryFindCellIn(temple, validator, out cell))
            {
                return true;
            }

            if (TryGetGuardArea(map, out CellRect guardArea) && TryFindCellIn(guardArea, validator, out cell))
            {
                if (temple.IsEmpty)
                {
                    WotfLog.Info(WotfLog.ModuleRuin, "no temple to put the ruin loot in; it went to the guard area "
                        + guardArea);
                }

                return true;
            }

            return CellFinderLoose.TryGetRandomCellWith(validator, map, 300, out cell);
        }

        private static bool TryFindCellIn(CellRect rect, System.Predicate<IntVec3> validator, out IntVec3 cell)
        {
            foreach (IntVec3 candidate in rect.Cells.InRandomOrder())
            {
                if (!validator(candidate))
                {
                    continue;
                }

                cell = candidate;
                return true;
            }

            cell = IntVec3.Invalid;
            return false;
        }

        /// <summary>Where the guard step gathers when it has no temple to defend.</summary>
        private static bool TryGetGuardArea(Map map, out CellRect rect)
        {
            if (SiteGenStepUtility.TryFindRootToSpawnAroundRectOfInterest(out rect, out IntVec3 near, map))
            {
                if (!rect.IsEmpty)
                {
                    return true;
                }

                if (near.IsValid)
                {
                    rect = CellRect.CenteredOn(near, 5, 5).ClipInsideMap(map);
                    return true;
                }
            }

            rect = CellRect.Empty;
            return false;
        }
    }
}
