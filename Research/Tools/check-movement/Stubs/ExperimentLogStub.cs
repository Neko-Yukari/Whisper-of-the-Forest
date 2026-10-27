using Verse;

namespace WhisperOfTheForest.Experimental
{
    /// <summary>
    /// Compile-only stand-in for the integration-owned shared experiment log.
    /// The real type ships with the integrated mod; see Experiments/movement.md.
    /// </summary>
    public static class ExperimentLog
    {
        public static void Record(string category, Pawn actor, string detail)
        {
        }
    }
}
