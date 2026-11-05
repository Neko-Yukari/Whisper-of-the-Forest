using Verse;

namespace WhisperOfTheForest.Missiles
{
    /// <summary>
    /// Read-only snapshot of one live missile for the in-game lab. Settings is a fresh instance copy
    /// (MemberwiseClone: Def references are shared and must be treated as read-only), so the caller
    /// cannot mutate the missile's settings object.
    /// </summary>
    public struct ArcaneMissileLabFrame
    {
        public float X;
        public float Z;
        public float HeadingX;
        public float HeadingZ;
        public float TargetX;
        public float TargetZ;
        public int AgeTicks;

        /// <summary>thingIDNumber of the live target, or 0 when there is none.</summary>
        public int TargetThingId;

        public IntVec3 TargetCell;
        public ArcaneMissileSettings Settings;
    }
}
