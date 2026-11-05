using Verse;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// The numbers an orbit form owns: orbs that circle the caster and apply the arranged effects to
    /// the pawns they touch. Copied into the compiled plan, where the form's augments change the copy.
    /// Every value comes from the form's XML; there is no code default behind them.
    /// </summary>
    public class SpellOrbitSpec : IExposable
    {
        /// <summary>Orbs placed per cast before Split.</summary>
        public int count;

        /// <summary>Distance from the caster to an orb, in cells.</summary>
        public float distance;

        /// <summary>Turn rate in degrees per second.</summary>
        public float angularSpeedDegrees;

        /// <summary>How close a pawn must pass an orb to be touched, in cells.</summary>
        public float hitRadius;

        /// <summary>How long one orb stays.</summary>
        public int lifetimeTicks;

        public void ExposeData()
        {
            Scribe_Values.Look(ref count, "count", 0);
            Scribe_Values.Look(ref distance, "distance", 0f);
            Scribe_Values.Look(ref angularSpeedDegrees, "angularSpeedDegrees", 0f);
            Scribe_Values.Look(ref hitRadius, "hitRadius", 0f);
            Scribe_Values.Look(ref lifetimeTicks, "lifetimeTicks", 0);
        }

        public SpellOrbitSpec Copy()
        {
            return (SpellOrbitSpec)MemberwiseClone();
        }
    }
}
