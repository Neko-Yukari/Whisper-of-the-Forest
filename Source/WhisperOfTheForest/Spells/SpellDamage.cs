using RimWorld;
using Verse;

namespace WhisperOfTheForest.Spells
{
    /// <summary>The one place a spell deals direct damage, so every damaging effect hits the same way.</summary>
    public static class SpellDamage
    {
        /// <summary>
        /// The spell damage type. Missing means null: <c>GetNamed</c> logs it, the load check in
        /// <see cref="SpellBootstrap"/> reports it, and every damaging effect skips its hit instead of
        /// substituting a core damage type.
        /// </summary>
        public static DamageDef ArcaneForce => DefDatabase<DamageDef>.GetNamed(SpellDefs.ArcaneForceDamageDefName);

        /// <summary>Deals arcane damage to one thing; false when the damage type is missing.</summary>
        public static bool Deal(Thing target, float amount, float armorPenetration, Pawn caster)
        {
            DamageDef def = ArcaneForce;
            if (def == null)
            {
                SpellDiagnostics.ErrorOnce("spell damage def " + SpellDefs.ArcaneForceDamageDefName + " is missing; the hit was skipped.");
                return false;
            }

            target.TakeDamage(new DamageInfo(
                def,
                amount,
                armorPenetration: armorPenetration,
                angle: -1f,
                instigator: caster,
                intendedTarget: target,
                instigatorGuilty: true,
                spawnFilth: false));
            return true;
        }
    }
}
