namespace WhisperOfTheForest.Experimental.Abilities
{
    /// <summary>
    /// XML side of the instant-maturity psycast. No parameters: the effect is all-or-nothing.
    /// </summary>
    public class CompProperties_AbilityWotFPlantGrowth : CompProperties_WotFAbilityEffect
    {
        public CompProperties_AbilityWotFPlantGrowth()
        {
            compClass = typeof(CompAbilityEffect_WotFPlantGrowth);
        }
    }
}
