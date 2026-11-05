using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Testing
{
    /// <summary>
    /// A rectangular piece of the current map that belongs to exactly one test: it is cleared of
    /// plants, filth, items and buildings when created and restored (terrain and foundations) when
    /// disposed. The disposable lab map is still mutated while a test runs, so the whole regression
    /// run must stay on the quicktest/lab profile map.
    ///
    /// After a save/load the object references are gone but the rectangle is not: <see cref="Reattach"/>
    /// rebuilds an arena from the saved numbers and finds the things a test spawned by their
    /// <c>thingIDNumber</c>, which the save preserves.
    /// </summary>
    public sealed class TestArena
    {
        private const int PreferredWidth = 46;
        private const int PreferredHeight = 10;

        private struct CellGround
        {
            public TerrainDef terrain;
            public TerrainDef foundation;
        }

        private readonly Map map;
        private readonly Dictionary<IntVec3, CellGround> ground = new Dictionary<IntVec3, CellGround>();
        private readonly List<int> things = new List<int>();

        private CellRect rect;

        private TestArena(Map map)
        {
            this.map = map;
        }

        public Map Map => map;

        public CellRect Rect
        {
            get { return rect; }
        }

        public int MinX => rect.minX;

        public int MaxX => rect.maxX;

        public int MinZ => rect.minZ;

        public int MaxZ => rect.maxZ;

        public IntVec3 Cell(int indexX, int indexZ)
        {
            return new IntVec3(rect.minX + indexX, 0, rect.minZ + indexZ);
        }

        /// <summary>Prepares a fresh rectangle for this test and records everything it destroys.</summary>
        public static TestArena Create(Map map, string testName)
        {
            var arena = new TestArena(map);
            arena.rect = FindFreeRect(map);
            if (arena.rect.IsEmpty)
            {
                throw new System.InvalidOperationException(
                    "arena: no free " + PreferredWidth + "x" + PreferredHeight + " strip on this map for " + testName);
            }

            arena.MovePawnsOut();
            arena.CaptureGround();
            arena.ClearRect();
            arena.Pave();
            return arena;
        }

        /// <summary>Rebuilds an arena after a save/load from the numbers kept by the runner.</summary>
        public static TestArena Reattach(Map map, IntVec3 origin, int width, int height)
        {
            var arena = new TestArena(map)
            {
                rect = new CellRect(origin.x, origin.z, width, height)
            };
            return arena;
        }

        public IntVec3 Origin => new IntVec3(rect.minX, 0, rect.minZ);

        public int Width => rect.Width;

        public int Height => rect.Height;

        /// <summary>True when the cell belongs to this arena; used to find things again after a load.</summary>
        public bool ContainsCell(IntVec3 cell)
        {
            return cell.InBounds(map) && rect.Contains(cell);
        }

        /// <summary>Registers a thing so cleanup destroys it and a reloaded test can find it again.</summary>
        public T Track<T>(T thing) where T : Thing
        {
            if (thing != null && thing.thingIDNumber != 0 && !things.Contains(thing.thingIDNumber))
            {
                things.Add(thing.thingIDNumber);
            }

            return thing;
        }

        /// <summary>Finds a tracked thing by id, or null when it was destroyed or is on another map.</summary>
        public Thing Find(int thingId)
        {
            List<Thing> spawned = map.listerThings.AllThings;
            for (int i = 0; i < spawned.Count; i++)
            {
                Thing thing = spawned[i];
                if (thing != null && !thing.Destroyed && thing.thingIDNumber == thingId)
                {
                    return thing;
                }
            }

            return null;
        }

        public IEnumerable<int> TrackedIds => things;

        /// <summary>Destroys what the test spawned and puts the ground back exactly as it was.</summary>
        public void Dispose()
        {
            // Pawns first: destroying a standing pawn leaves no corpse behind.
            foreach (int id in new List<int>(things))
            {
                Thing thing = Find(id);
                if (thing == null)
                {
                    continue;
                }

                if (thing is Pawn pawn)
                {
                    pawn.apparel?.DestroyAll();
                    pawn.equipment?.DestroyAllEquipment(DestroyMode.Vanish);
                }

                if (!thing.Destroyed)
                {
                    thing.Destroy(DestroyMode.Vanish);
                }
            }

            things.Clear();

            foreach (KeyValuePair<IntVec3, CellGround> pair in ground)
            {
                if (!pair.Key.InBounds(map))
                {
                    continue;
                }

                map.terrainGrid.SetTerrain(pair.Key, pair.Value.terrain);
                if (pair.Value.foundation == null)
                {
                    // Vanilla RemoveFoundation dereferences the current foundation, so calling it on a
                    // cell that never had one throws. Only a foundation the test added is removed.
                    if (map.terrainGrid.FoundationAt(pair.Key) != null)
                    {
                        map.terrainGrid.RemoveFoundation(pair.Key, false);
                    }
                }
                else
                {
                    map.terrainGrid.SetFoundation(pair.Key, pair.Value.foundation);
                }
            }

            ground.Clear();
            map.pathing?.RecalculateAllPerceivedPathCosts();
            map.reachability?.ClearCache();
        }

        public void SetTerrain(IntVec3 cell, TerrainDef terrain)
        {
            map.terrainGrid.SetTerrain(cell, terrain);
            map.pathing?.RecalculateAllPerceivedPathCosts();
            map.reachability?.ClearCache();
        }

        /// <summary>
        /// A free strip near the map edge, so a full-length shot towards the edge crosses only this
        /// arena. Mountains and water are skipped; plants, filth and buildings are allowed in the
        /// probe because the rect is cleared right afterwards.
        /// </summary>
        private static CellRect FindFreeRect(Map map)
        {
            int height = System.Math.Min(PreferredHeight, map.Size.z - 8);
            int width = System.Math.Min(PreferredWidth, map.Size.x - 8);
            int z = (map.Size.z - height) / 2;

            for (int attempt = 0; attempt < 2; attempt++)
            {
                bool east = attempt == 0;
                int step = 2;
                for (int dx = 4; dx <= System.Math.Min(40, map.Size.x / 3); dx += step)
                {
                    int x = east ? map.Size.x - dx - width : dx;
                    var candidate = new CellRect(x, z, width, height);
                    if (StripClear(map, candidate))
                    {
                        return candidate;
                    }
                }
            }

            // Fall back to the immediate surroundings of the map centre.
            for (int dz = -12; dz <= 12; dz += 4)
            {
                for (int dx = -20; dx <= 20; dx += 4)
                {
                    var candidate = new CellRect(map.Center.x + dx, map.Center.z + dz - height / 2, width, height);
                    if (StripClear(map, candidate))
                    {
                        return candidate;
                    }
                }
            }

            return CellRect.Empty;
        }

        private static bool StripClear(Map map, CellRect candidate)
        {
            if (candidate.minX < 1 || candidate.minZ < 1 || candidate.maxX > map.Size.x - 2 || candidate.maxZ > map.Size.z - 2)
            {
                return false;
            }

            foreach (IntVec3 cell in candidate.Cells)
            {
                TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
                if (terrain == null || terrain.passability == Traversability.Impassable)
                {
                    return false;
                }
            }

            return true;
        }

        private void CaptureGround()
        {
            foreach (IntVec3 cell in rect.Cells)
            {
                ground[cell] = new CellGround
                {
                    terrain = map.terrainGrid.TerrainAt(cell),
                    foundation = map.terrainGrid.FoundationAt(cell)
                };
            }
        }

        /// <summary>
        /// Everything standing inside the rectangle goes; pawns are moved out and survive. Map
        /// features the game never destroys (a steam geyser) stay where they are.
        /// </summary>
        private void ClearRect()
        {
            foreach (IntVec3 cell in rect.Cells)
            {
                List<Thing> here = new List<Thing>(map.thingGrid.ThingsListAt(cell));
                for (int i = 0; i < here.Count; i++)
                {
                    Thing thing = here[i];
                    if (thing is Pawn || thing.Destroyed || !thing.def.destroyable)
                    {
                        continue;
                    }

                    thing.Destroy(DestroyMode.Vanish);
                }
            }

            map.pathing?.RecalculateAllPerceivedPathCosts();
            map.reachability?.ClearCache();
        }

        private void MovePawnsOut()
        {
            var inside = new List<Pawn>();
            foreach (IntVec3 cell in rect.Cells)
            {
                foreach (Thing thing in map.thingGrid.ThingsListAt(cell))
                {
                    if (thing is Pawn pawn && !inside.Contains(pawn))
                    {
                        inside.Add(pawn);
                    }
                }
            }

            for (int i = 0; i < inside.Count; i++)
            {
                IntVec3 destination = FindOutsideCell(inside[i]);
                if (!destination.IsValid)
                {
                    continue;
                }

                inside[i].Position = destination;
                inside[i].Notify_Teleported(true, true);
            }
        }

        private IntVec3 FindOutsideCell(Pawn pawn)
        {
            // GenRadial logs a real Log.Error when asked for a radius past its pattern, and half a
            // 250-cell map is already past that limit. Stay inside the pattern, then fall back to a
            // plain map scan for the outer band.
            float radius = System.Math.Min(map.Size.x / 2f, GenRadial.MaxRadialPatternRadius - 1f);
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(map.Center, radius, true))
            {
                if (IsFreeStandingCell(cell))
                {
                    return cell;
                }
            }

            foreach (IntVec3 cell in map.AllCells)
            {
                if (IsFreeStandingCell(cell))
                {
                    return cell;
                }
            }

            return IntVec3.Invalid;
        }

        private bool IsFreeStandingCell(IntVec3 cell)
        {
            return cell.InBounds(map) && !rect.Contains(cell) && cell.Standable(map) && !cell.Fogged(map)
                   && cell.GetFirstPawn(map) == null;
        }

        /// <summary>
        /// Plain concrete under the whole arena: a test that cares about ground lays its own lanes on
        /// top, and a test that does not gets a flat, fast, predictable floor.
        /// </summary>
        private void Pave()
        {
            foreach (IntVec3 cell in rect.Cells)
            {
                if (map.terrainGrid.FoundationAt(cell) != null)
                {
                    map.terrainGrid.RemoveFoundation(cell, false);
                }

                map.terrainGrid.SetTerrain(cell, TerrainDefOf.Concrete);
            }

            map.pathing?.RecalculateAllPerceivedPathCosts();
            map.reachability?.ClearCache();
        }

        public string Describe()
        {
            var builder = new StringBuilder(32);
            builder.Append("rect=").Append(rect.minX).Append(',').Append(rect.minZ)
                .Append(' ').Append(rect.Width).Append('x').Append(rect.Height);
            return builder.ToString();
        }
    }
}
