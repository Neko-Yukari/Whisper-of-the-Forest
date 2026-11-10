using Verse;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Turrets
{
    /// <summary>
    /// The turret's attack verb. It has no cast phases: the building's tick drives warmup and
    /// cooldown, and this verb only asks the turret comp to pay for and launch its frozen dart.
    /// </summary>
    public class Verb_CastTurretSpell : Verb
    {
        protected override bool TryCastShot()
        {
            CompSpellTurret turret = caster.TryGetComp<CompSpellTurret>();
            if (turret == null)
            {
                SpellDiagnostics.ErrorOnce("turret verb used on " + caster.LabelShort
                                           + " without CompSpellTurret.");
                return false;
            }

            if (turret.TryCast(currentTarget, out string reason))
            {
                return true;
            }

            SpellDiagnostics.Debug("spell turret refused shot: " + reason);
            return false;
        }

        public override bool Available()
        {
            if (!base.Available())
            {
                return false;
            }

            CompSpellTurret turret = caster.TryGetComp<CompSpellTurret>();
            return turret != null && turret.CanAfford;
        }
    }
}
