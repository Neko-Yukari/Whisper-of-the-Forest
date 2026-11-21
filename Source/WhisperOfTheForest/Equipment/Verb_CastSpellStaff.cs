using RimWorld;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Equipment
{
    /// <summary>
    /// The staff or wand's attack: a real ranged verb so the drafted attack order, the native
    /// targeter and the AI's static-attack job all reach it through the ordinary weapon path, but
    /// no dart is fired here. <see cref="TryCastShot"/> hands the chosen spellbook bookmark to
    /// <see cref="SpellService.TryCast"/>, which is the same entry point the book gizmo uses;
    /// windup, channel, recovery, cooldown and the payload stay with the casting system.
    ///
    /// Phase timing is not duplicated: <c>warmupTime</c> is 0 in the Def, so the vanilla verb has
    /// nothing to warm up and the staff's own windup is the casting service's.
    ///
    /// Reach belongs to the spell scaled by the staff's <see cref="SpellStaffModifiers.rangeFactor"/>:
    /// <see cref="EffectiveRange"/>, <see cref="CanHitTarget"/> and <see cref="CanHitTargetFrom"/>
    /// all compile through <see cref="SelectedPlan"/> with the staff's modifiers so the aim ring,
    /// stationing and cast reach always agree.
    /// </summary>
    public class Verb_CastSpellStaff : Verb
    {
        private bool ownsVerbProps;

        public override float EffectiveRange
        {
            get
            {
                SpellCastPlan plan = SelectedPlan();
                if (plan == null)
                {
                    return 0f;
                }

                float range;
                if (plan.form != SpellFormKind.Special)
                {
                    // The form's own range (already scaled by staff.rangeFactor); Self and Orbit have none.
                    range = plan.range;
                }
                else
                {
                    ISpellSpecialSpec spec = plan.Special;
                    if (spec.Range > 0f)
                    {
                        range = spec.Range;
                    }
                    else
                    {
                        // A composite form's 0 means the whole map. Keep a finite number here: the
                        // base out-of-range test squares it, so infinities break the comparison.
                        Map map = CasterPawn?.Map;
                        range = map == null ? 0f : map.Size.x + map.Size.z;
                    }
                }

                // CastPositionFinder multiplies EffectiveRange by verbProps.range when it picks a
                // shooting position, so keep verbProps.range in step with the selected spell and staff.
                SyncVerbPropsRange(range);
                return range;
            }
        }

        private void SyncVerbPropsRange(float range)
        {
            if (verbProps == null)
            {
                return;
            }

            if (!ownsVerbProps)
            {
                verbProps = verbProps.MemberwiseClone();
                ownsVerbProps = true;
            }

            verbProps.range = range;
        }

        /// <summary>
        /// A staff with no saved spell in its chosen bookmark, or a two-handed staff blocked by a
        /// worn shield belt, cannot attack: vanilla then falls back to the pawn's melee verb.
        /// </summary>
        public override bool Available()
        {
            if (!base.Available() || !CasterIsPawn)
            {
                return false;
            }

            CompSpellStaff staff = EquipmentSource?.GetComp<CompSpellStaff>();
            if (staff == null || staff.IsBlockedByShieldBelt(CasterPawn))
            {
                return false;
            }

            return SelectedPlan() != null;
        }

        public override TargetingParameters targetParams
        {
            get
            {
                Pawn pawn = CasterPawn;
                SpellCastPlan plan = SelectedPlan();
                return pawn == null || plan == null
                    ? verbProps.targetParams
                    : SpellService.BuildTargetingParameters(pawn, plan);
            }
        }

        public override bool CanHitTarget(LocalTargetInfo targ)
        {
            Pawn pawn = CasterPawn;
            if (pawn == null || !pawn.Spawned || pawn.Map == null || !targ.IsValid)
            {
                return false;
            }

            CompSpellStaff staff = EquipmentSource?.GetComp<CompSpellStaff>();
            if (staff == null || staff.IsBlockedByShieldBelt(pawn))
            {
                return false;
            }

            SpellCastPlan plan = SelectedPlan();
            if (plan == null)
            {
                return false;
            }

            TargetInfo pick = targ.HasThing
                ? new TargetInfo(targ.Thing)
                : new TargetInfo(targ.Cell, pawn.Map);
            return SpellService.IsTargetable(pawn, plan, pick);
        }

        /// <summary>
        /// AI stationing evaluates candidate cells through this overload, so the spell's reach
        /// and line of sight must be measured from the candidate root. The cast itself still
        /// rechecks from the caster's real position before it spends anything.
        /// </summary>
        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            Pawn pawn = CasterPawn;
            if (pawn == null || !pawn.Spawned || pawn.Map == null || !targ.IsValid)
            {
                return false;
            }

            CompSpellStaff staff = EquipmentSource?.GetComp<CompSpellStaff>();
            if (staff == null || staff.IsBlockedByShieldBelt(pawn))
            {
                return false;
            }

            SpellCastPlan plan = SelectedPlan();
            if (plan == null)
            {
                return false;
            }

            TargetInfo pick = targ.HasThing
                ? new TargetInfo(targ.Thing)
                : new TargetInfo(targ.Cell, pawn.Map);
            return SpellService.IsTargetableFrom(pawn, plan, root.IsValid ? root : pawn.Position, pick);
        }

        public override void OrderForceTarget(LocalTargetInfo target)
        {
            Pawn pawn = CasterPawn;
            CompSpellStaff staff = EquipmentSource?.GetComp<CompSpellStaff>();
            if (pawn != null && staff != null && !staff.CanCastThrough(pawn, out string reason))
            {
                SpellService.Reject(reason);
                return;
            }

            // While dual-wielding (Primary weapon + offhand wand), vanilla JobDefOf.AttackStatic
            // calls Pawn.TryGetAttackVerb which always returns the Primary weapon and ignores
            // job.verbToUse. Dispatch manual offhand clicks through JobDefOf.UseVerbOnThing so
            // JobDriver_CastVerbOnce invokes this offhand verb directly.
            if (pawn != null && EquipmentSource != null
                && pawn.equipment?.Primary != null && pawn.equipment.Primary != EquipmentSource)
            {
                Job job = JobMaker.MakeJob(JobDefOf.UseVerbOnThing, target);
                job.verbToUse = this;
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                return;
            }

            base.OrderForceTarget(target);
        }

        protected override bool TryCastShot()
        {
            Pawn pawn = CasterPawn;
            CompSpellStaff staff = EquipmentSource?.GetComp<CompSpellStaff>();
            if (pawn == null || staff == null)
            {
                return false;
            }

            if (!staff.CanCastThrough(pawn, out string shieldReason))
            {
                if (pawn.IsPlayerControlled)
                {
                    SpellService.Reject(shieldReason);
                }

                return false;
            }

            SpellRecipe recipe = staff.SelectedRecipe;
            if (recipe == null || SelectedPlan() == null)
            {
                return false;
            }

            string reason;
            if (!SpellService.TryCast(pawn, recipe, currentTarget, out reason, staff))
            {
                WotfLog.Debug(StaffDefs.LogModule, "staff attack refused for '" + recipe.name + "': " + reason);
                if (pawn.IsPlayerControlled)
                {
                    SpellService.Reject(reason);
                }

                return false;
            }

            WotfLog.Info(StaffDefs.LogModule, "staff cast slot " + (staff.Slot + 1) + " '"
                                             + recipe.name + "' at " + currentTarget.Cell);
            return true;
        }

        /// <summary>
        /// The bookmark's recipe compiled with this staff's modifiers into the exact plan the cast
        /// will run. Compiled on demand rather than cached: the slot stores an index, so an edit to
        /// that book page must be visible to the very next attack.
        /// </summary>
        private SpellCastPlan SelectedPlan()
        {
            CompSpellStaff staff = EquipmentSource?.GetComp<CompSpellStaff>();
            SpellRecipe recipe = staff?.SelectedRecipe;
            return recipe == null ? null : AutoAttackPlan(recipe, staff.Modifiers);
        }

        /// <summary>
        /// The plan a staff may fire as a weapon attack, or null. Self, Orbit and Super spells are
        /// player-triggered skills, never weapon auto-attacks.
        /// </summary>
        public static SpellCastPlan AutoAttackPlan(SpellRecipe recipe, SpellStaffModifiers staff = null)
        {
            SpellCastPlan plan = SpellCompiler.Compile(recipe, out _, staff);
            return plan == null || plan.form == SpellFormKind.Self || plan.form == SpellFormKind.Orbit
                || SpellDefs.Glyph(recipe.glyphs[0]).kind == SpellGlyphKind.Super ? null : plan;
        }
    }
}
