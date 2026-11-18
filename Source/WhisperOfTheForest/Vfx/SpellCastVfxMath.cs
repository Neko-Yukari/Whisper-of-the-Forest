using System;
using UnityEngine;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>
    /// Pure stateless math for Dragon's Dogma 2 style spell-casting convergence and 2.5D pawn silhouette
    /// depth masking. Shared cleanly between runtime rendering and offline seam tests.
    /// </summary>
    public static class SpellCastVfxMath
    {
        private const float MinCollapseWindowSeconds = 0.05f;

        /// <summary>
        /// Normalized convergence progress in [0, 1]. Reaches 1.0f at (totalPrepSeconds - leadCollapseSeconds)
        /// so the swirling vortex collapses tightest right before release flash.
        /// </summary>
        public static float ComputeConvergence(float elapsedSeconds, float totalPrepSeconds, float leadCollapseSeconds)
        {
            if (totalPrepSeconds <= 0f)
            {
                return 1f;
            }

            float collapseWindow = Math.Max(MinCollapseWindowSeconds, totalPrepSeconds - Math.Max(0f, leadCollapseSeconds));
            float raw = elapsedSeconds / collapseWindow;
            if (raw <= 0f)
            {
                return 0f;
            }

            return raw >= 1f ? 1f : raw;
        }

        /// <summary>
        /// Evaluates world altitude Y relative to the caster's Z position so vertices north of the caster
        /// sit below Pawn Sprite depth (occluded by head/hair/back) and vertices south sit in front of the pawn.
        /// </summary>
        public static float EvaluatePawnMaskAltitude(
            float worldZ,
            float casterZ,
            float zTransitionHalf,
            float frontAltitudeY,
            float backAltitudeY)
        {
            float half = Math.Max(0.001f, zTransitionHalf);
            float t = (worldZ - (casterZ - half)) / (2f * half);
            if (t <= 0f)
            {
                return frontAltitudeY;
            }

            if (t >= 1f)
            {
                return backAltitudeY;
            }

            float smooth = t * t * (3f - 2f * t);
            return frontAltitudeY + (backAltitudeY - frontAltitudeY) * smooth;
        }

        public static Vector3 ClampFocalPointBeforeWall(
            Verse.Map map,
            Vector3 casterPos,
            float dirX,
            float dirZ,
            float forwardOffsetCells,
            float wallClearanceMargin,
            float altitudeY)
        {
            float distance = forwardOffsetCells;
            if (map != null)
            {
                var targetCell = new Verse.IntVec3(
                    Mathf.FloorToInt(casterPos.x + dirX * forwardOffsetCells),
                    0,
                    Mathf.FloorToInt(casterPos.z + dirZ * forwardOffsetCells));
                if (Verse.GenGrid.InBounds(targetCell, map))
                {
                    Verse.Building edifice = map.edificeGrid[targetCell];
                    if (edifice != null && edifice.def.Fillage == Verse.FillCategory.Full)
                    {
                        distance = Mathf.Max(0f, wallClearanceMargin);
                    }
                }
            }

            return new Vector3(casterPos.x + dirX * distance, altitudeY, casterPos.z + dirZ * distance);
        }
    }
}
