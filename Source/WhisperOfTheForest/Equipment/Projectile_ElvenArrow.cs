using RimWorld;
using Verse;

namespace WhisperOfTheForest.Equipment
{
    /// <summary>
    /// The elven longbow's arrow. Vanilla arrows are <see cref="Bullet"/>s, and their damage is applied
    /// inside <see cref="Bullet.Impact"/> (RimWorld/Bullet.cs:22), so the enchantment reaction runs
    /// after the base call: the hit has already resolved and a dead target stays dead
    /// (技术/02-魔法实现.md:133). The bow is reached through the projectile's own equipment field,
    /// which Verb_LaunchProjectile hands over when the arrow is launched
    /// (Verse/Verb_LaunchProjectile.cs:93, Verse/Projectile.cs:194).
    /// </summary>
    public class Projectile_ElvenArrow : Bullet
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Thing equipmentSource = equipment;
            Thing shooter = launcher;
            base.Impact(hitThing, blockedByShield);

            if (blockedByShield || hitThing == null || hitThing.Destroyed)
            {
                return;
            }

            if (hitThing is Pawn victim && victim.Dead)
            {
                return;
            }

            CompEnchantedEquipment enchant = (equipmentSource as ThingWithComps)?.GetComp<CompEnchantedEquipment>();
            if (enchant == null || !enchant.HasSpell)
            {
                return;
            }

            enchant.TriggerOnHit(shooter as Pawn, hitThing);
        }
    }
}
