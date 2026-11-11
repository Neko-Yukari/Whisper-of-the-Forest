using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Missiles;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// Startup wiring for the spellbook: normalises the carrier AbilityDef and reports a broken
    /// catalogue once, at load, instead of failing at the first cast.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class SpellBootstrap
    {
        static SpellBootstrap()
        {
            NormalizeCarrierDef();
            VerifyCatalogue();
        }

        /// <summary>
        /// <c>Ability.Initialize()</c> asks <c>GenCollection.Any(def.comps)</c>, which dereferences the
        /// list without a null check, and it runs both when an ability is created and again when one
        /// is loaded. A comp-less ability Def (this carrier needs no ability comp: it draws its own
        /// gizmos and the cooldown lives in <see cref="GameComponent_Spellbook"/>) would therefore
        /// throw. Filling in the empty list here is the smallest honest fix and changes nothing else.
        /// </summary>
        private static void NormalizeCarrierDef()
        {
            AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail(SpellDefs.CastAbilityDefName);
            if (def == null)
            {
                SpellDiagnostics.Error("ability def " + SpellDefs.CastAbilityDefName
                                       + " is missing; the spellbook cannot appear on any pawn.");
                return;
            }

            if (def.comps == null)
            {
                def.comps = new List<AbilityCompProperties>();
            }

            if (def.abilityClass == null || !typeof(Ability).IsAssignableFrom(def.abilityClass))
            {
                SpellDiagnostics.Error("ability def " + def.defName + " has an unusable abilityClass ("
                                       + (def.abilityClass?.FullName ?? "null") + ").");
            }
        }

        /// <summary>The whole catalogue is checked once so a missing part is visible immediately.</summary>
        private static void VerifyCatalogue()
        {
            SpellTuningDef tuning = DefDatabase<SpellTuningDef>.GetNamedSilentFail(SpellDefs.TuningDefName);
            if (tuning == null)
            {
                SpellDiagnostics.Error("tuning def " + SpellDefs.TuningDefName
                                       + " is missing; spells are disabled.");
            }

            string[] required =
            {
                SpellDefs.FormSelf, SpellDefs.FormTouch, SpellDefs.FormProjectile, SpellDefs.FormOrbit,
                SpellDefs.EffectHarm, SpellDefs.EffectHeal, SpellDefs.EffectExplosion, SpellDefs.EffectGrow,
                SpellDefs.EffectLightning, SpellDefs.EffectFreeze,
                SpellDefs.EffectColdSnap, SpellDefs.EffectKnockback, SpellDefs.EffectPull,
                SpellDefs.EffectSnare, SpellDefs.EffectHex, SpellDefs.EffectMagelight,
                SpellDefs.EffectBlink, SpellDefs.EffectLinger, SpellDefs.EffectRune, SpellDefs.EffectDelay,
                SpellDefs.AugmentAmplify, SpellDefs.AugmentAoe, SpellDefs.AugmentAccelerate,
                SpellDefs.AugmentDecelerate, SpellDefs.AugmentSplit, SpellDefs.AugmentDampen,
                SpellDefs.AugmentExtend, SpellDefs.AugmentShorten, SpellDefs.AugmentSensitive,
                SpellDefs.AugmentBreakArmor, SpellDefs.AugmentPierce
            };

            for (int i = 0; i < required.Length; i++)
            {
                if (DefDatabase<SpellGlyphDef>.GetNamedSilentFail(required[i]) == null)
                {
                    SpellDiagnostics.Error("spell glyph " + required[i] + " is missing from the catalogue.");
                }
            }

            string[] hediffs =
            {
                SpellEffectDefs.HediffClotting, SpellEffectDefs.HediffFreeze, SpellEffectDefs.HediffEnsnare,
                SpellEffectDefs.HediffHex, SpellEffectDefs.HediffLightningCharge,
                SpellEffectDefs.HediffScorch
            };

            for (int i = 0; i < hediffs.Length; i++)
            {
                if (DefDatabase<HediffDef>.GetNamedSilentFail(hediffs[i]) == null)
                {
                    SpellDiagnostics.Error("spell hediff " + hediffs[i] + " is missing; that effect cannot resolve.");
                }
            }

            string[] things =
            {
                SpellEffectDefs.ThingMagelight, SpellEffectDefs.ThingSpellField, SpellEffectDefs.ThingSpellOrbit
            };

            for (int i = 0; i < things.Length; i++)
            {
                if (DefDatabase<ThingDef>.GetNamedSilentFail(things[i]) == null)
                {
                    SpellDiagnostics.Error("spell thing " + things[i] + " is missing; that effect cannot place anything.");
                }
            }

            if (DefDatabase<DamageDef>.GetNamedSilentFail(SpellDefs.ArcaneForceDamageDefName) == null)
            {
                SpellDiagnostics.Error("damage def " + SpellDefs.ArcaneForceDamageDefName
                                       + " is missing; every damaging spell skips its hit.");
            }
        }
    }
}
