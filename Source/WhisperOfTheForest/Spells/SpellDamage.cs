using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace WhisperOfTheForest.Spells
{
    /// <summary>Declares combat-log flavor rules on non-glyph spell damage carriers such as DoT hediffs.</summary>
    public sealed class SpellCombatLogExtension : DefModExtension
    {
        public RulePackDef combatLogRules;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (combatLogRules == null)
            {
                yield return "combatLogRules is required";
            }
        }
    }

    /// <summary>
    /// Frozen spell source carried into <see cref="SpellDamage"/>. Root Def references scribe through
    /// their own <see cref="DefDatabase{T}"/>, so in-flight darts, fields, delays, tornadoes and
    /// explosions keep their combat-log flavor across save/load.
    /// </summary>
    public sealed class SpellDamageSource : IExposable
    {
        public RulePackDef logRules;
        public SpellGlyphDef glyphDef;
        public HediffDef hediffDef;

        public static SpellDamageSource FromGlyph(SpellGlyphDef glyph)
        {
            return glyph == null
                ? null
                : new SpellDamageSource
                {
                    logRules = glyph.combatLogRules,
                    glyphDef = glyph
                };
        }

        public static SpellDamageSource FromHediff(HediffDef hediff)
        {
            return hediff == null
                ? null
                : new SpellDamageSource
                {
                    logRules = hediff.GetModExtension<SpellCombatLogExtension>()?.combatLogRules,
                    hediffDef = hediff
                };
        }

        public SpellDamageSource Copy()
        {
            return new SpellDamageSource
            {
                logRules = logRules,
                glyphDef = glyphDef,
                hediffDef = hediffDef
            };
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref logRules, "logRules");
            Scribe_Defs.Look(ref glyphDef, "glyphDef");
            Scribe_Defs.Look(ref hediffDef, "hediffDef");
        }
    }

    /// <summary>
    /// Combat-log entry shared by every damaging spell effect. Unlike <see cref="BattleLogEntry_DamageTaken"/>
    /// (which omits <c>INITIATOR</c> grammar rules and attacker POV icons/clicks), this binds the
    /// caster (pawn or turret), the recipient, the spell Def and the effect's own <see cref="RulePackDef"/>.
    /// </summary>
    public sealed class BattleLogEntry_SpellDamage : LogEntry_DamageResult
    {
        private Pawn initiatorPawn;
        private ThingDef initiatorThing;
        private Pawn recipientPawn;
        private ThingDef recipientThing;
        private RulePackDef logRules;
        private SpellGlyphDef glyphDef;
        private HediffDef hediffDef;

        public BattleLogEntry_SpellDamage()
        {
        }

        public BattleLogEntry_SpellDamage(Thing initiator, Thing recipient, SpellDamageSource source)
        {
            if (initiator is Pawn casterPawn)
            {
                initiatorPawn = casterPawn;
            }
            else if (initiator != null)
            {
                initiatorThing = initiator.def;
            }

            if (recipient is Pawn targetPawn)
            {
                recipientPawn = targetPawn;
            }
            else if (recipient != null)
            {
                recipientThing = recipient.def;
            }

            if (source != null)
            {
                logRules = source.logRules;
                glyphDef = source.glyphDef;
                hediffDef = source.hediffDef;
            }
        }

        private string InitiatorName =>
            initiatorPawn != null ? initiatorPawn.LabelShort : initiatorThing != null ? initiatorThing.defName : "null";

        private string RecipientName =>
            recipientPawn != null ? recipientPawn.LabelShort : recipientThing != null ? recipientThing.defName : "null";

        public override bool Concerns(Thing t)
        {
            return t != null && (t == initiatorPawn || t == recipientPawn);
        }

        public override IEnumerable<Thing> GetConcerns()
        {
            if (initiatorPawn != null)
            {
                yield return initiatorPawn;
            }

            if (recipientPawn != null)
            {
                yield return recipientPawn;
            }
        }

        public override bool CanBeClickedFromPOV(Thing pov)
        {
            if (pov == initiatorPawn && recipientPawn != null)
            {
                return CameraJumper.CanJump(recipientPawn);
            }

            if (pov == recipientPawn && initiatorPawn != null)
            {
                return CameraJumper.CanJump(initiatorPawn);
            }

            return false;
        }

        public override void ClickedFromPOV(Thing pov)
        {
            if (pov == initiatorPawn && recipientPawn != null)
            {
                CameraJumper.TryJumpAndSelect(recipientPawn);
            }
            else if (pov == recipientPawn && initiatorPawn != null)
            {
                CameraJumper.TryJumpAndSelect(initiatorPawn);
            }
        }

        public override Texture2D IconFromPOV(Thing pov)
        {
            if (damagedParts.NullOrEmpty() || deflected)
            {
                return null;
            }

            if (pov == null || pov == recipientPawn)
            {
                return Blood;
            }

            if (pov == initiatorPawn)
            {
                return BloodTarget;
            }

            return null;
        }

        protected override BodyDef DamagedBody()
        {
            return recipientPawn?.RaceProps.body;
        }

        protected override GrammarRequest GenerateGrammarRequest()
        {
            GrammarRequest result = base.GenerateGrammarRequest();
            if (logRules != null)
            {
                result.Includes.Add(logRules);
            }

            if (initiatorPawn != null)
            {
                result.Rules.AddRange(GrammarUtility.RulesForPawn("INITIATOR", initiatorPawn, result.Constants, addRelationInfoSymbol: false));
            }
            else if (initiatorThing != null)
            {
                result.Rules.AddRange(GrammarUtility.RulesForDef("INITIATOR", initiatorThing));
            }
            else
            {
                result.Constants["INITIATOR_missing"] = "True";
            }

            if (recipientPawn != null)
            {
                result.Rules.AddRange(GrammarUtility.RulesForPawn("RECIPIENT", recipientPawn, result.Constants, addRelationInfoSymbol: false));
            }
            else if (recipientThing != null)
            {
                result.Rules.AddRange(GrammarUtility.RulesForDef("RECIPIENT", recipientThing));
            }
            else
            {
                result.Constants["RECIPIENT_missing"] = "True";
            }

            Def sourceDef = (Def)glyphDef ?? hediffDef;
            if (sourceDef != null)
            {
                result.Rules.AddRange(GrammarUtility.RulesForDef("SPELL", sourceDef));
            }

            return result;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (initiatorPawn != null && initiatorPawn.Discarded)
                {
                    initiatorPawn = null;
                }

                if (recipientPawn != null && recipientPawn.Discarded)
                {
                    recipientPawn = null;
                }
            }

            Scribe_References.Look(ref initiatorPawn, "initiatorPawn", saveDestroyedThings: true);
            Scribe_Defs.Look(ref initiatorThing, "initiatorThing");
            Scribe_References.Look(ref recipientPawn, "recipientPawn", saveDestroyedThings: true);
            Scribe_Defs.Look(ref recipientThing, "recipientThing");
            Scribe_Defs.Look(ref logRules, "logRules");
            Scribe_Defs.Look(ref glyphDef, "glyphDef");
            Scribe_Defs.Look(ref hediffDef, "hediffDef");
        }

        public override string ToString()
        {
            return (logRules != null ? logRules.defName : "BattleLogEntry_SpellDamage")
                   + ": " + InitiatorName + "->" + RecipientName;
        }
    }

    /// <summary>Native explosion carrier keeping the spell effect's combat-log source across ticks and saves.</summary>
    public sealed class Thing_SpellExplosion : Explosion
    {
        public SpellDamageSource logSource;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref logSource, "logSource");
        }
    }

    /// <summary>
    /// Routes <see cref="Thing_SpellExplosion"/> hits through <see cref="SpellDamage.Deal(Thing,DamageDef,float,float,Thing,SpellDamageSource,out DamageWorker.DamageResult,float,bool,BodyPartDepth,bool)"/>
    /// while leaving vanilla explosions and direct injury math untouched.
    /// </summary>
    public sealed class DamageWorker_SpellArcaneForce : DamageWorker_AddInjury
    {
        protected override void ExplosionDamageThing(
            Explosion explosion,
            Thing t,
            List<Thing> damagedThings,
            List<Thing> ignoredThings,
            IntVec3 cell)
        {
            if (!(explosion is Thing_SpellExplosion spellExplosion))
            {
                base.ExplosionDamageThing(explosion, t, damagedThings, ignoredThings, cell);
                return;
            }

            if (t.def.category == ThingCategory.Mote || t.def.category == ThingCategory.Ethereal || damagedThings.Contains(t))
            {
                return;
            }

            damagedThings.Add(t);
            if (ignoredThings != null && ignoredThings.Contains(t))
            {
                return;
            }

            float angle = t.Position != explosion.Position
                ? (t.Position - explosion.Position).AngleFlat
                : Rand.RangeInclusive(0, 359);
            BodyPartDepth depth = def.explosionAffectOutsidePartsOnly ? BodyPartDepth.Outside : BodyPartDepth.Undefined;

            SpellDamage.Deal(
                t,
                def,
                explosion.GetDamageAmountAt(cell),
                explosion.GetArmorPenetrationAt(cell),
                explosion.instigator,
                spellExplosion.logSource,
                out DamageResult damageResult,
                angle: angle,
                allowDamagePropagation: true,
                bodyDepth: depth,
                spawnFilth: true);

            if (t is Pawn pawn && damageResult != null && damageResult.wounded && pawn.stances != null)
            {
                pawn.stances.stagger.StaggerFor(95);
            }
        }
    }

    /// <summary>
    /// The unified entry point for every spell-caused hit and explosion: builds <see cref="DamageInfo"/>,
    /// records caster and spell source, applies damage, and links <see cref="BattleLog"/> entries.
    /// </summary>
    public static class SpellDamage
    {
        public static DamageDef ArcaneForce => DefDatabase<DamageDef>.GetNamed(SpellDefs.ArcaneForceDamageDefName);

        public static bool Deal(
            Thing target,
            float amount,
            float armorPenetration,
            Thing caster,
            SpellDamageSource source,
            float angle = -1f,
            bool allowDamagePropagation = true,
            BodyPartDepth bodyDepth = BodyPartDepth.Undefined,
            bool spawnFilth = false)
        {
            return Deal(
                target,
                ArcaneForce,
                amount,
                armorPenetration,
                caster,
                source,
                out _,
                angle,
                allowDamagePropagation,
                bodyDepth,
                spawnFilth);
        }

        public static bool Deal(
            Thing target,
            DamageDef damageDef,
            float amount,
            float armorPenetration,
            Thing caster,
            SpellDamageSource source,
            float angle = -1f,
            bool allowDamagePropagation = true,
            BodyPartDepth bodyDepth = BodyPartDepth.Undefined,
            bool spawnFilth = false)
        {
            return Deal(
                target,
                damageDef,
                amount,
                armorPenetration,
                caster,
                source,
                out _,
                angle,
                allowDamagePropagation,
                bodyDepth,
                spawnFilth);
        }

        public static bool Deal(
            Thing target,
            DamageDef damageDef,
            float amount,
            float armorPenetration,
            Thing caster,
            SpellDamageSource source,
            out DamageWorker.DamageResult result,
            float angle = -1f,
            bool allowDamagePropagation = true,
            BodyPartDepth bodyDepth = BodyPartDepth.Undefined,
            bool spawnFilth = false)
        {
            result = null;
            if (target == null)
            {
                return false;
            }

            if (damageDef == null)
            {
                SpellDiagnostics.Error("spell damage def is missing; the hit was skipped.");
                return false;
            }

            var dinfo = new DamageInfo(
                damageDef,
                amount,
                armorPenetration: armorPenetration,
                angle: angle,
                instigator: caster,
                intendedTarget: target,
                instigatorGuilty: true,
                spawnFilth: spawnFilth);
            if (!allowDamagePropagation)
            {
                dinfo.SetAllowDamagePropagation(false);
            }

            if (bodyDepth != BodyPartDepth.Undefined)
            {
                dinfo.SetBodyRegion(BodyPartHeight.Undefined, bodyDepth);
            }

            BattleLogEntry_SpellDamage logEntry = null;
            if (damageDef.harmsHealth && !target.Destroyed && target is Pawn && source?.logRules != null && Find.BattleLog != null)
            {
                logEntry = new BattleLogEntry_SpellDamage(caster, target, source);
                Find.BattleLog.Add(logEntry);
            }

            result = target.TakeDamage(dinfo);
            if (logEntry != null)
            {
                if (result.wounded || result.deflected || !result.parts.NullOrEmpty())
                {
                    result.AssociateWithLog(logEntry);
                }
                else
                {
                    Find.BattleLog.RemoveEntry(logEntry);
                }
            }

            return true;
        }

        public static bool Explode(
            IntVec3 center,
            Map map,
            float radius,
            int damAmount,
            float armorPenetration,
            Thing caster,
            SpellDamageSource source,
            List<Thing> ignoredThings = null)
        {
            if (map == null || !center.InBounds(map))
            {
                return false;
            }

            DamageDef damType = ArcaneForce;
            ThingDef explosionDef = DefDatabase<ThingDef>.GetNamedSilentFail(SpellEffectDefs.ThingSpellExplosion);
            if (damType == null || explosionDef == null)
            {
                SpellDiagnostics.Error("spell explosion def is missing; the blast was skipped.");
                return false;
            }

            var explosion = (Thing_SpellExplosion)GenSpawn.Spawn(explosionDef, center, map);
            explosion.radius = radius;
            explosion.damType = damType;
            explosion.instigator = caster;
            explosion.damAmount = Mathf.Max(1, damAmount);
            explosion.armorPenetration = armorPenetration;
            explosion.doVisualEffects = false;
            explosion.damageFalloff = true;
            explosion.logSource = source?.Copy();
            explosion.StartExplosion(null, ignoredThings);
            return true;
        }
    }
}
