using System;

namespace WhisperOfTheForest.Experimental.Spells
{
    /// <summary>
    /// The cell math of a triggered projectile segment, kept free of Verse and Unity types so it can
    /// be executed outside the game (Moddev/tools/check-spells/segments-harness).
    ///
    /// "Behind the target" means along the incoming motion, through the target: the direction runs
    /// from the impact point to the centre of what was hit, the dart is spawned on the far side of
    /// that thing, and it keeps going the same way. It never turns back towards the caster and never
    /// homes onto the thing that was already hit.
    ///
    /// A hit thing that is gone (destroyed, despawned) or a hit that landed exactly on its centre has
    /// no usable direction, so the incoming heading is used instead - the continuation still leaves
    /// along the flight line rather than through the caster.
    /// </summary>
    public static class SpellTriggerGeometry
    {
        /// <summary>Distance beyond the hit thing's centre along the direction, in cells.</summary>
        public const float SpawnOffsetCells = 0.75f;

        /// <summary>Aim distance in front of the spawn point, shortened to the map edge.</summary>
        public const float LookAheadCells = 20f;

        /// <summary>Shorter than this is not a direction; the heading takes over.</summary>
        private const float MinDirectionLength = 0.0001f;

        /// <summary>
        /// Direction of the next projectile segment: through the hit thing (impact -> hit centre), or
        /// the incoming heading when there is no live hit or the two points coincide.
        /// Returns false only when neither gives a usable direction.
        /// </summary>
        public static bool TryDirection(
            bool hasLiveHit,
            float hitX,
            float hitZ,
            float impactX,
            float impactZ,
            float headingX,
            float headingZ,
            out float dirX,
            out float dirZ)
        {
            if (hasLiveHit)
            {
                float throughX = hitX - impactX;
                float throughZ = hitZ - impactZ;
                float length = (float)Math.Sqrt(throughX * throughX + throughZ * throughZ);
                if (length > MinDirectionLength)
                {
                    dirX = throughX / length;
                    dirZ = throughZ / length;
                    return true;
                }
            }

            return TryNormalize(headingX, headingZ, out dirX, out dirZ);
        }

        /// <summary>
        /// Spawn point of the next segment: <see cref="SpawnOffsetCells"/> past the live hit thing's
        /// centre (or past the impact point when nothing live was hit) along
        /// <paramref name="dirX"/>/<paramref name="dirZ"/>.
        /// Returns false when the cell that point falls in is outside the map: an off-map spawn is not
        /// a trigger, and the caller must log it instead of firing.
        /// </summary>
        public static bool TrySpawnPoint(
            bool hasLiveHit,
            float hitX,
            float hitZ,
            float impactX,
            float impactZ,
            float dirX,
            float dirZ,
            int mapSizeX,
            int mapSizeZ,
            out float spawnX,
            out float spawnZ)
        {
            float anchorX = hasLiveHit ? hitX : impactX;
            float anchorZ = hasLiveHit ? hitZ : impactZ;
            spawnX = anchorX + dirX * SpawnOffsetCells;
            spawnZ = anchorZ + dirZ * SpawnOffsetCells;
            return InMap(spawnX, spawnZ, mapSizeX, mapSizeZ);
        }

        /// <summary>
        /// Furthest in-map cell at most <see cref="LookAheadCells"/> from the spawn point along the
        /// direction. Distances are shortened one cell at a time, so a hand-over next to the map edge
        /// still gets a target instead of dropping the segment.
        /// </summary>
        public static bool TryLookAhead(
            float fromX,
            float fromZ,
            float dirX,
            float dirZ,
            int mapSizeX,
            int mapSizeZ,
            out int cellX,
            out int cellZ)
        {
            for (float distance = LookAheadCells; distance >= 1f; distance -= 1f)
            {
                float x = fromX + dirX * distance;
                float z = fromZ + dirZ * distance;
                if (InMap(x, z, mapSizeX, mapSizeZ))
                {
                    cellX = (int)Math.Floor(x);
                    cellZ = (int)Math.Floor(z);
                    return true;
                }
            }

            // The caller must check the return value: these coordinates are not a cell.
            cellX = -1;
            cellZ = -1;
            return false;
        }

        private static bool TryNormalize(float x, float z, out float dirX, out float dirZ)
        {
            float length = (float)Math.Sqrt(x * x + z * z);
            if (length <= MinDirectionLength)
            {
                dirX = 0f;
                dirZ = 0f;
                return false;
            }

            dirX = x / length;
            dirZ = z / length;
            return true;
        }

        private static bool InMap(float x, float z, int mapSizeX, int mapSizeZ)
        {
            if (mapSizeX <= 0 || mapSizeZ <= 0)
            {
                return false;
            }

            int cellX = (int)Math.Floor(x);
            int cellZ = (int)Math.Floor(z);
            return cellX >= 0 && cellZ >= 0 && cellX < mapSizeX && cellZ < mapSizeZ;
        }
    }
}
