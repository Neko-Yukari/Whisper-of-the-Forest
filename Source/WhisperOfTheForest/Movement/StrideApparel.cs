using RimWorld;
using Verse;

namespace WhisperOfTheForest.Experimental.Movement
{
    /// <summary>
    /// Def names plus wearer checks for the two movement experiment apparels.
    /// Both patches read the worn apparel every call, so removing an item restores
    /// vanilla behaviour on the next pather tick without any cleanup state.
    /// </summary>
    public static class StrideApparel
    {
        public const string TerrainStrideDefName = "WotF_TerrainStride";
        public const string BlinkStrideDefName = "WotF_BlinkStride";

        public static bool IsWearingTerrainStride(Pawn pawn)
        {
            return IsWearing(pawn, TerrainStrideDefName);
        }

        public static bool IsWearingBlinkStride(Pawn pawn)
        {
            return IsWearing(pawn, BlinkStrideDefName);
        }

        public static bool IsWearingAnyStride(Pawn pawn)
        {
            return IsWearingTerrainStride(pawn) || IsWearingBlinkStride(pawn);
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
