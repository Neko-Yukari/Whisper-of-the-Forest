using Verse;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Turrets
{
    public sealed class TurretSpellCandidate
    {
        public Pawn Owner { get; }
        public int Slot { get; }
        public SpellRecipe Recipe { get; }
        public string UnavailableReason { get; }

        internal TurretSpellCandidate(Pawn owner, int slot, SpellRecipe recipe, string unavailableReason)
        {
            Owner = owner;
            Slot = slot;
            Recipe = recipe;
            UnavailableReason = unavailableReason;
        }
    }
}
