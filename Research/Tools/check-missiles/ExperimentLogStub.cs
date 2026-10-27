using Verse;

namespace WhisperOfTheForest.Experimental
{
    /// <summary>
    /// Compile-time stand-in for the integration-owned experiment log. The real type is provided by
    /// the integration worker with exactly this signature; this copy only exists so the missile
    /// worker can compile-check their own files before integration.
    /// </summary>
    public static class ExperimentLog
    {
        public static void Record(string category, Pawn actor, string detail)
        {
        }
    }
}
