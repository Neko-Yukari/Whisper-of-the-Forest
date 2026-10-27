using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Experimental.Missiles
{
    /// <summary>Sample hit strategy: direct impact damage on the thing that was struck.</summary>
    public class ArcaneMissileImpactWorker_Damage : ArcaneMissileImpactWorker
    {
        public override void Resolve(ArcaneMissileImpactContext context)
        {
            Thing hit = context.Hit;
            if (hit == null || hit.Destroyed || context.Map == null || context.Settings == null)
            {
                return;
            }

            hit.TakeDamage(new DamageInfo(
                context.DamageDef,
                context.Settings.damageAmount,
                armorPenetration: context.Settings.armorPenetration,
                angle: -1f,
                instigator: context.Caster,
                intendedTarget: hit,
                instigatorGuilty: true,
                spawnFilth: false));
        }
    }

    /// <summary>Sample hit strategy: heal existing injuries on the struck pawn, nothing on ground hits.</summary>
    public class ArcaneMissileImpactWorker_Heal : ArcaneMissileImpactWorker
    {
        public override void Resolve(ArcaneMissileImpactContext context)
        {
            if (!(context.Hit is Pawn pawn) || pawn.health == null || context.Settings == null)
            {
                return;
            }

            float remaining = context.Settings.healAmount;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;

            // Backwards: if the game removes a fully healed hediff during the call (Hediff.ShouldRemove
            // is evaluated outside Heal in 1.7), a forward index would skip the next wound.
            for (int i = hediffs.Count - 1; i >= 0 && remaining > 0.001f; i--)
            {
                if (!(hediffs[i] is Hediff_Injury injury) || HediffUtility.IsPermanent(injury))
                {
                    continue;
                }

                float before = injury.Severity;
                injury.Heal(remaining);
                remaining -= MathF.Max(0f, before - injury.Severity);
            }
        }
    }

    /// <summary>Sample hit strategy: vanilla explosion at the impact cell, visual effects owned by the visual layer.</summary>
    public class ArcaneMissileImpactWorker_Explode : ArcaneMissileImpactWorker
    {
        public override void Resolve(ArcaneMissileImpactContext context)
        {
            if (context.Map == null || context.Settings == null)
            {
                return;
            }

            GenExplosion.DoExplosion(
                context.ImpactCell,
                context.Map,
                context.Settings.explosionRadius,
                context.DamageDef,
                context.Caster,
                damAmount: (int)context.Settings.damageAmount,
                armorPenetration: context.Settings.armorPenetration,
                doVisualEffects: false,
                damageFalloff: true);
        }
    }
}
