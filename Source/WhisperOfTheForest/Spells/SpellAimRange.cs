namespace WhisperOfTheForest.Experimental.Spells
{
    /// <summary>
    /// The one aim-distance rule: how far from the caster a compiled spell may still be aimed.
    ///
    /// A limit of 0 or less means the whole map - only the map edge stops the aim. That is how a
    /// composite form declares an unlimited aim (<see cref="SpellRailgunSpec.range"/>); every other
    /// form passes the bounded <see cref="SpellTuningDef.castRange"/>, so they keep the 24.9 cells
    /// they always had. Keeping the decision here means the targeter and the cast service cannot
    /// drift into two different answers.
    ///
    /// The arithmetic is the same as <c>IntVec3.InHorDistOf</c> (inclusive squared horizontal
    /// distance), so a cell exactly on the boundary is still accepted.
    /// </summary>
    public static class SpellAimRange
    {
        /// <summary>Any limit at or below this means "no distance limit".</summary>
        public const float WholeMap = 0f;

        public static bool Within(float maxCells, int fromX, int fromZ, int toX, int toZ)
        {
            if (maxCells <= WholeMap)
            {
                return true;
            }

            float dx = toX - fromX;
            float dz = toZ - fromZ;
            return dx * dx + dz * dz <= maxCells * maxCells;
        }
    }
}
