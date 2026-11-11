namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// Def names the new effect implementations share. The numbers behind them live in XML; this
    /// class only names the Defs so runtime code never carries a second copy of a value.
    /// </summary>
    public static class SpellEffectDefs
    {
        // Lasting states. Each HediffDef owns its own stat/ capacity numbers and a Disappears comp;
        // the spell only chooses the duration.
        public const string HediffClotting = "WotF_Hediff_Clotting";
        public const string HediffFreeze = "WotF_Hediff_Freeze";
        public const string HediffEnsnare = "WotF_Hediff_Ensnare";
        public const string HediffHex = "WotF_Hediff_Hex";
        public const string HediffLightningCharge = "WotF_Hediff_LightningCharge";
        public const string HediffScorch = "WotF_Hediff_Scorch";

        // Temporary map objects.
        public const string ThingMagelight = "WotF_Magelight";
        public const string ThingSpellField = "WotF_SpellField";
        public const string ThingSpellOrbit = "WotF_SpellOrbit";

        /// <summary>The vanilla flying carrier used by knockback and pull.</summary>
        public const string ThingPawnFlyer = "PawnFlyer";
    }
}
