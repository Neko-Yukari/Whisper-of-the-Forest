using System;
using System.Collections.Generic;
using WhisperOfTheForest.Equipment;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Turrets;
using static WhisperOfTheForest.Tests.Glyphs;

namespace WhisperOfTheForest.Tests
{
    /// <summary>
    /// Seam: a compiled plan's form decides which carrier may hold it. The turret fires projectiles
    /// only, the staff auto-attacks with projectile and touch spells, and equipment carries touch,
    /// self and orbit spells. Each carrier asks the same compiler.
    /// </summary>
    internal static class SpellCarrierTests
    {
        internal static IEnumerable<(string, Action)> All => new (string, Action)[]
        {
            ("turret: takes a projectile spell", () => Turret(true, null, Projectile, Harm)),
            ("turret: refuses a touch spell", () => Turret(false, "WotF_Turret_ProjectileOnly", Touch, Harm)),
            ("turret: refuses a super rune", () => Turret(false, "WotF_Spells.Reason.SuperNoTurret", Super)),
            ("turret: refuses what does not compile", () => Turret(false, "WotF_Spells.Reason.NoEffect", Projectile)),
            ("staff: auto-attacks with a projectile spell", () => Staff(true, Projectile, Harm)),
            ("staff: auto-attacks with a touch spell", () => Staff(true, Touch, Harm)),
            ("staff: self spell is a skill, not an attack", () => Staff(false, Self, Harm)),
            ("staff: orbit spell is a skill, not an attack", () => Staff(false, Orbit, Harm)),
            ("staff: super rune is a skill, not an attack", () => Staff(false, Super)),
            ("equipment: carries a touch spell", () => Equipment(true, null, Touch, Harm)),
            ("equipment: carries a self spell", () => Equipment(true, null, Self, Harm)),
            ("equipment: carries an orbit spell", () => Equipment(true, null, Orbit, Harm)),
            ("equipment: refuses a projectile spell", () => Equipment(false, "WotF_Ritual_EquipmentForm", Projectile, Harm)),
        };

        private static void Turret(bool expected, string reasonKey, params string[] glyphs)
        {
            bool accepted = CompSpellTurret.CanCompileProjectile(Recipe(glyphs), out string reason);
            Check.Equal(expected, accepted, "turret [" + string.Join(",", glyphs) + "] reason=" + reason);
            Check.Equal(reasonKey, reason, "reason");
        }

        private static void Staff(bool expected, params string[] glyphs)
        {
            Check.Equal(expected, Verb_CastSpellStaff.AutoAttackPlan(Recipe(glyphs)) != null, "staff [" + string.Join(",", glyphs) + "]");
        }

        private static void Equipment(bool expected, string reasonKey, params string[] glyphs)
        {
            bool accepted = CompEnchantedEquipment.AcceptsRecipe(Recipe(glyphs), out string reason);
            Check.Equal(expected, accepted, "equipment [" + string.Join(",", glyphs) + "] reason=" + reason);
            Check.Equal(reasonKey, reason, "reason");
        }
    }
}
