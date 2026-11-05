using System;

namespace WhisperOfTheForest.Experimental.Spells
{
    /// <summary>
    /// The corridor math of the composite form, kept free of Verse and Unity types so it can be
    /// executed outside the game (Moddev/tools/check-spells/segments-harness).
    ///
    /// The corridor is a straight line in the XZ plane from the caster's cell centre towards the
    /// locked aim point, at most <c>range</c> cells long and shortened to the map edge. A target is
    /// inside it when its cell centre is within half the hit width of that line and its projection
    /// falls between the two ends. Nothing here can be blocked: there is no obstacle term, because
    /// the spell penetrates everything it crosses.
    /// </summary>
    public static class RailgunGeometry
    {
        /// <summary>Shorter than this is not a direction.</summary>
        private const float MinDirectionLength = 0.0001f;

        /// <summary>Unit direction from one point to another; false when the points coincide.</summary>
        public static bool TryDirection(float fromX, float fromZ, float toX, float toZ, out float dirX, out float dirZ)
        {
            float dx = toX - fromX;
            float dz = toZ - fromZ;
            float length = (float)Math.Sqrt(dx * dx + dz * dz);
            if (length <= MinDirectionLength)
            {
                dirX = 0f;
                dirZ = 0f;
                return false;
            }

            dirX = dx / length;
            dirZ = dz / length;
            return true;
        }

        /// <summary>
        /// Usable corridor length: <paramref name="maxLength"/> shortened to the map rectangle, so the
        /// beam ends at the edge instead of aiming outside the map. Returns false when the ray leaves
        /// the map immediately.
        /// </summary>
        public static bool TryLength(
            float originX,
            float originZ,
            float dirX,
            float dirZ,
            float maxLength,
            int sizeX,
            int sizeZ,
            out float length)
        {
            length = 0f;
            if (maxLength <= 0f || sizeX <= 0 || sizeZ <= 0)
            {
                return false;
            }

            float enter = 0f;
            float exit = maxLength;
            if (!ClipAxis(originX, dirX, sizeX, ref enter, ref exit)
                || !ClipAxis(originZ, dirZ, sizeZ, ref enter, ref exit))
            {
                return false;
            }

            if (exit <= 0f || enter > exit)
            {
                return false;
            }

            length = exit;
            return true;
        }

        /// <summary>
        /// Whether one point is inside the corridor. <paramref name="along"/> is the distance along the
        /// direction, so the caller can report where a target was crossed.
        /// </summary>
        public static bool WithinCorridor(
            float originX,
            float originZ,
            float dirX,
            float dirZ,
            float pointX,
            float pointZ,
            float halfWidth,
            float length,
            out float along)
        {
            float px = pointX - originX;
            float pz = pointZ - originZ;
            along = px * dirX + pz * dirZ;
            if (along < 0f || along > length)
            {
                return false;
            }

            // With a unit direction the 2D cross product is the perpendicular distance.
            float perpendicular = Math.Abs(px * dirZ - pz * dirX);
            return perpendicular <= halfWidth;
        }

        /// <summary>Splits the map rectangle into the part of the ray [enter, exit] that stays inside.</summary>
        private static bool ClipAxis(float origin, float direction, int size, ref float enter, ref float exit)
        {
            if (Math.Abs(direction) <= MinDirectionLength)
            {
                return origin >= 0f && origin <= size;
            }

            float first = (0f - origin) / direction;
            float second = (size - origin) / direction;
            if (first > second)
            {
                float swap = first;
                first = second;
                second = swap;
            }

            if (first > enter)
            {
                enter = first;
            }

            if (second < exit)
            {
                exit = second;
            }

            return enter <= exit;
        }
    }
}
