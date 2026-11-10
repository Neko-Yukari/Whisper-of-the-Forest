using Verse;

namespace WhisperOfTheForest.Missiles
{
    /// <summary>
    /// Everything a payload may read about a hit. Built once per missile at the exact impact point,
    /// so a payload never needs to reach into the missile, the map component or the visual layer.
    /// </summary>
    public struct ArcaneMissileImpactContext
    {
        public Map Map;
        public Thing Caster;

        /// <summary>Thing actually struck. Null for a ground impact; may be a building or wall.</summary>
        public Thing Hit;

        /// <summary>Thing chosen when the volley was fired, if it was a thing target.</summary>
        public Thing OriginalTarget;

        /// <summary>Cell chosen when the volley was fired (the last known cell for a moving target).</summary>
        public IntVec3 OriginalTargetCell;

        /// <summary>Cell the missile detonated in.</summary>
        public IntVec3 ImpactCell;

        /// <summary>Exact detonation point in world units (horizontal plane; y is flight height).</summary>
        public float ImpactX;
        public float ImpactZ;

        /// <summary>Normalised horizontal heading of the missile at the moment of impact.</summary>
        public float HeadingX;
        public float HeadingZ;

        /// <summary>The hitting dart's own settings copy, so a payload can fire the same dart on.</summary>
        public ArcaneMissileSettings Settings;
    }

    /// <summary>
    /// What a missile delivers. The missile layer flies, collides and calls <see cref="Resolve"/>
    /// exactly once per missile; whoever fired the volley owns the payload type. That keeps the
    /// dependency one way: the spell layer implements this, the missile layer never names a spell.
    /// </summary>
    public interface IArcaneMissilePayload : IExposable
    {
        void Resolve(ArcaneMissileImpactContext context);

        /// <summary>An independent copy, so every dart of a volley owns its own payload.</summary>
        IArcaneMissilePayload Copy();
    }
}
