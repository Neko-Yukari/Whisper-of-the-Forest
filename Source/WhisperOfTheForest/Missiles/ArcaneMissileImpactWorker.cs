using Verse;

namespace WhisperOfTheForest.Experimental.Missiles
{
    /// <summary>
    /// Everything a hit strategy may read. Built once per missile at the exact impact point, so a
    /// worker never needs to reach into the missile, the map component or the visual layer.
    /// </summary>
    public struct ArcaneMissileImpactContext
    {
        public Map Map;
        public Pawn Caster;

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

        public ArcaneMissileSettings Settings;

        /// <summary>Damage type the volley was fired with (never null; falls back to a core def).</summary>
        public DamageDef DamageDef => Settings != null ? Settings.ResolvedDamageDef : ArcaneMissileDefs.ArcaneForce;
    }

    /// <summary>
    /// One hit strategy. Workers must be stateless: a single instance per Def is shared by every
    /// missile, and <see cref="Resolve"/> runs once per missile before it is removed.
    /// </summary>
    public abstract class ArcaneMissileImpactWorker
    {
        public abstract void Resolve(ArcaneMissileImpactContext context);
    }
}
