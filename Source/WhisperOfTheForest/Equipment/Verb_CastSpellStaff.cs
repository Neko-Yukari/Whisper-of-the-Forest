using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Equipment
{
    /// <summary>
    /// The staff's attack: a real ranged verb so the drafted attack order, the native targeter and
    /// the AI's static-attack job all reach it through the ordinary weapon path, but no dart is
    /// fired here. <see cref="TryCastShot"/> hands the chosen spellbook bookmark to
    /// <see cref="SpellService.TryCast"/>, which is the same entry point the book gizmo uses; windup,
    /// channel, recovery, cooldown and the payload stay with the casting system.
    ///
    /// Phase timing is not duplicated: <c>warmupTime</c> is 0 in the Def, so the vanilla verb has
    /// nothing to warm up and the staff's own windup is the casting service's.
    ///
    /// Reach belongs to the spell, not to the weapon: <see cref="EffectiveRange"/> and
    /// <see cref="CanHitTarget"/> read the compiled plan of the chosen bookmark, so switching the
    /// slot really changes how far the attack reaches. The Def's <c>range</c> is only the fallback
    /// used while no bookmark is chosen; <c>VerbProperties.DrawRadiusRing</c> and the targeter both
    /// ask the verb for its effective range.
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
                if (plan.form != SpellFormKind.Railgun)
                {
                    // The form's own range; Self and Orbit have none and so give the staff no reach.
                    range = plan.range;
                }
                else
                {
                    SpellRailgunSpec spec = plan.railgun;
                    if (spec.range > 0f)
                    {
                        range = spec.range;
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
                // shooting position, so a staff whose spell reaches farther than the weapon Def
                // would still station at the Def's range. Give this verb its own VerbProperties and
                // keep its range in step with the selected spell.
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
        /// A staff with no saved spell in its chosen bookmark cannot attack at all: vanilla then
        /// falls back to the pawn's melee verb, which is the intended "no book, no cast" behaviour.
        /// </summary>
        public override bool Available()
        {
            return base.Available() && CasterIsPawn && SelectedPlan() != null;
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

        protected override bool TryCastShot()
        {
            Pawn pawn = CasterPawn;
            CompSpellStaff staff = EquipmentSource?.GetComp<CompSpellStaff>();
            if (pawn == null || staff == null)
            {
                return false;
            }

            SpellRecipe recipe = staff.SelectedRecipe;
            if (recipe == null)
            {
                return false;
            }

            string reason;
            if (!SpellService.TryCast(pawn, recipe, currentTarget, out reason, staff.Modifiers))
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
        /// The bookmark's recipe compiled into the exact plan the cast will run. Compiled on demand
        /// rather than cached: the slot stores an index, so an edit to that book page must be visible
        /// to the very next attack.
        /// </summary>
        private SpellCastPlan SelectedPlan()
        {
            SpellRecipe recipe = EquipmentSource?.GetComp<CompSpellStaff>()?.SelectedRecipe;
            return recipe == null ? null : SpellCompiler.Compile(recipe, out _);
        }
    }
}
