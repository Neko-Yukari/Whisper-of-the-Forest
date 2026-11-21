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

        protected override string HeaderBadgeText => SpellText.T("WotF_Turret_HeaderBadge");

        protected override bool ValidateCatalogGlyph(SpellGlyphDef glyph, out string reason)
        {
            if (glyph != null)
            {
                if (glyph.kind == SpellGlyphKind.Super)
                {
                    reason = SpellText.T("WotF_Spells.Reason.SuperNoTurret");
                    return false;
                }

                if (glyph.kind == SpellGlyphKind.Form && SpellCompiler.FormOf(glyph) != SpellFormKind.Projectile)
                {
                    reason = SpellText.T("WotF_Turret_ProjectileOnly");
                    return false;
                }
            }

            return base.ValidateCatalogGlyph(glyph, out reason);
        }

        protected override bool ValidateChain(SpellRecipe recipe, out string reason)
        {
            return CompSpellTurret.CanCompileProjectile(recipe, out reason);
        }
    }
}
