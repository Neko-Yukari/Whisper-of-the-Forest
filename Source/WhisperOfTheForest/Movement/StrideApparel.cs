using RimWorld;
using Verse;

namespace WhisperOfTheForest.Movement
{
    /// <summary>
    /// Reads worn apparel directly so the movement patch keeps no equipment cache.
    /// </summary>
    public static class StrideApparel
    {
        public const string TerrainStrideDefName = "WotF_TerrainStride";

        public static bool IsWearingTerrainStride(Pawn pawn)
        {
            return IsWearing(pawn, TerrainStrideDefName);
        }

        public static bool IsWearing(Pawn pawn, string defName)
        {
            if (pawn == null || pawn.apparel == null)
            {
                return false;
            }

            var worn = pawn.apparel.WornApparel;
            if (worn == null)
            {
                return false;
            }

            for (int i = 0; i < worn.Count; i++)
            {
                Apparel apparel = worn[i];
                if (apparel != null && apparel.def != null && apparel.def.defName == defName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
