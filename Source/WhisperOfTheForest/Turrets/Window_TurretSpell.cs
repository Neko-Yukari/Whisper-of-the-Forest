using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Turrets
{
    public sealed class Window_TurretSpell : Window_Spellbook
    {
        public Window_TurretSpell(CompSpellTurret turret)
            : base(turret.TryRequestInscription,
                SpellText.T("WotF_Turret_NewSpell"), SpellText.T("WotF_Turret_RequestWrite"),
                recipe => SpellCost.Derive(recipe.glyphs).mana * turret.Props.manaCostFactor)
        {
        }
    }
}
