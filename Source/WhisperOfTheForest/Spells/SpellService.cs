using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Experimental.Spells
{
    /// <summary>
    /// The one casting service shared by the book page and the native gizmos. It owns validation,
    /// the caster requirements, the cooldown, the player targeter and the entry point; the phases and
    /// the payload of a cast belong to <see cref="SpellCastingService"/>.
    /// </summary>
    public static class SpellService
    {
        // ---------------------------------------------------------------- grammar

        /// <summary>True when the chain is syntactically valid; reason is player-facing text.</summary>
        public static bool Validate(SpellRecipe recipe, out string reason)
        {
            return SpellCompiler.TryValidate(recipe, out reason);
        }

        // ---------------------------------------------------------------- caster requirements

        /// <summary>
        /// Documented requirement: a player-controlled, alive, spawned pawn with at least one Royalty
        /// psylink level. Everything else is refused with a visible reason, never silently.
        /// </summary>
        public static bool IsEligibleCaster(Pawn pawn, out string reason)
        {
            reason = null;

            if (pawn == null || pawn.Destroyed)
            {
                reason = SpellText.T("WotF_Spells.Reason.NoPawn");
                return false;
            }

            if (pawn.Dead)
            {
                reason = SpellText.T("WotF_Spells.Reason.PawnDead");
                return false;
            }

            if (!pawn.Spawned || pawn.Map == null)
            {
                reason = SpellText.T("WotF_Spells.Reason.PawnNotSpawned");
                return false;
            }

            if (pawn.Downed)
            {
                reason = SpellText.T("WotF_Spells.Reason.PawnDowned");
                return false;
            }

            if (!pawn.IsPlayerControlled)
            {
                reason = SpellText.T("WotF_Spells.Reason.NotPlayerControlled");
                return false;
            }

            if (PawnUtility.GetPsylinkLevel(pawn) < 1)
            {
                reason = SpellText.T("WotF_Spells.Reason.NoPsylink");
                return false;
            }

            if (SpellCastingService.IsCasting(pawn))
            {
                reason = SpellText.T("WotF_Spells.Reason.AlreadyCasting");
                return false;
            }

            return true;
        }

        public static bool CanCast(Pawn pawn, SpellRecipe recipe, out string reason)
        {
            if (!IsEligibleCaster(pawn, out reason))
            {
                return false;
            }

            if (!Validate(recipe, out reason))
            {
                return false;
            }

            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            if (component == null)
            {
                reason = SpellText.T("WotF_Spells.Reason.NoGameComponent");
                return false;
            }

            int cooldown = component.GetCooldownTicksRemaining(pawn);
            if (cooldown > 0)
            {
                reason = SpellText.TF("WotF_Spells.Reason.Cooldown", (cooldown / (float)GenTicks.TicksPerRealSecond).ToString("0.0"));
                return false;
            }

            reason = null;
            return true;
        }

        // ---------------------------------------------------------------- casting

        /// <summary>
        /// Accepts a cast and hands it to <see cref="SpellCastingService"/>, which owns the phases and
        /// the payload. The recipe is compiled into a frozen plan first; the plan carries its timings,
        /// so editing the book afterwards cannot change a cast that is already running.
        /// </summary>
        public static bool TryCast(Pawn pawn, SpellRecipe recipe, LocalTargetInfo target, out string reason)
        {
            if (!CanCast(pawn, recipe, out reason))
            {
                return false;
            }

            SpellCastPlan plan = SpellCompiler.Compile(recipe, out reason);
            if (plan == null)
            {
                return false;
            }

            return SpellCastingService.Begin(pawn, plan, target, out reason);
        }

        /// <summary>
        /// Player entry point shared by the book button and the cast gizmo: opens the native targeter
        /// for Touch/Projectile, and casts immediately for Self (the player never has to click itself).
        /// </summary>
        public static void BeginTargeting(Pawn pawn, SpellRecipe recipe)
        {
            string reason;
            if (recipe == null)
            {
                Reject(SpellText.T("WotF_Spells.Reason.NoSpellSelected"));
                return;
            }

            // Freeze the recipe now: what the player arranged when the targeter opened is what fires.
            SpellRecipe frozen = recipe.Copy();
            SpellCastPlan plan = SpellCompiler.Compile(frozen, out reason);
            if (plan == null)
            {
                Reject(reason);
                return;
            }

            if (!CanCast(pawn, frozen, out reason))
            {
                Reject(reason);
                return;
            }

            if (plan.form == SpellFormKind.Self || plan.form == SpellFormKind.Orbit)
            {
                if (!TryCast(pawn, frozen, new LocalTargetInfo(pawn), out reason))
                {
                    Reject(reason);
                }

                return;
            }

            Targeter targeter = Find.Targeter;
            if (targeter == null)
            {
                Reject(SpellText.T("WotF_Spells.Reason.NoTargeter"));
                return;
            }

            targeter.BeginTargeting(
                BuildTargetingParameters(pawn, plan),
                delegate(LocalTargetInfo target)
                {
                    string failure;
                    if (!TryCast(pawn, frozen, target, out failure))
                    {
                        Reject(failure);
                    }
                },
                pawn,
                null,
                null,
                false);
        }

        /// <summary>Visible refusal for anything the player tried that the service did not accept.</summary>
        public static void Reject(string reason)
        {
            Messages.Message(
                reason.NullOrEmpty() ? SpellText.T("WotF_Spells.Reason.Unknown") : reason,
                MessageTypeDefOf.RejectInput,
                false);
        }

        // ---------------------------------------------------------------- targeting

        /// <summary>
        /// Native targeter filter built from what the chain can really act on: pawns for Harm/Heal,
        /// buildings for Harm, plants for Grow/Harm, bare cells for Explosion/Grow. Range, line of
        /// sight and Touch adjacency are enforced by the validator, so the game draws its own
        /// accept/reject feedback.
        /// </summary>
        public static TargetingParameters BuildTargetingParameters(Pawn caster, SpellCastPlan plan)
        {
            bool harm = plan.HasEffect(SpellEffectKind.Harm);
            bool heal = plan.HasEffect(SpellEffectKind.Heal);
            bool grow = plan.HasEffect(SpellEffectKind.Grow);
            bool explosion = plan.HasEffect(SpellEffectKind.Explosion);
            bool railgun = plan.form == SpellFormKind.Railgun;

            // Anything that resolves on a body or a structure, and anything that needs a bare cell.
            // Payload effects count: a delayed or lingered Harm still needs a body at that spot.
            bool body = heal
                        || harm
                        || plan.HasEffect(SpellEffectKind.Ignite)
                        || plan.HasEffect(SpellEffectKind.Lightning)
                        || plan.HasEffect(SpellEffectKind.Freeze)
                        || plan.HasEffect(SpellEffectKind.ColdSnap)
                        || plan.HasEffect(SpellEffectKind.Knockback)
                        || plan.HasEffect(SpellEffectKind.Pull)
                        || plan.HasEffect(SpellEffectKind.Snare)
                        || plan.HasEffect(SpellEffectKind.Hex);
            bool structure = harm
                             || plan.HasEffect(SpellEffectKind.Ignite)
                             || plan.HasEffect(SpellEffectKind.Lightning);
            bool plant = grow || harm || plan.HasEffect(SpellEffectKind.Ignite);
            bool cell = explosion
                        || grow
                        || plan.HasEffect(SpellEffectKind.Magelight)
                        || plan.HasEffect(SpellEffectKind.Blink)
                        || plan.HasEffect(SpellEffectKind.Linger)
                        || plan.HasEffect(SpellEffectKind.Rune)
                        || plan.HasEffect(SpellEffectKind.Delay);

            var parameters = new TargetingParameters
            {
                canTargetPawns = railgun || body,
                canTargetAnimals = railgun || body,
                canTargetHumans = railgun || body,
                canTargetMechs = railgun || body,
                canTargetSubhumans = railgun || body,
                canTargetBuildings = railgun || structure,
                canTargetPlants = railgun || plant,
                canTargetItems = false,
                canTargetCorpses = false,
                canTargetFires = false,
                canTargetLocations = railgun || cell,
                canTargetSelf = false,
                neverTargetIncapacitated = false,
                neverTargetDoors = false,
                mustBeSelectable = false,
                mapObjectTargetsMustBeAutoAttackable = false
            };

            parameters.validator = delegate(TargetInfo pick) { return IsTargetable(caster, plan, pick); };
            return parameters;
        }

        /// <summary>
        /// Range, map and line of sight for one candidate target of a compiled plan. A composite form
        /// uses its own range and needs no line of sight: it hits through walls and mountains, so
        /// refusing an aim point on sight would contradict what the spell does.
        /// </summary>
        public static bool IsTargetable(Pawn caster, SpellCastPlan plan, TargetInfo pick)
        {
            if (plan != null && plan.form == SpellFormKind.Railgun)
            {
                if (caster == null || !caster.Spawned || caster.Map == null || !pick.IsValid)
                {
                    return false;
                }

                if (pick.Map != caster.Map || !GenGrid.InBounds(pick.Cell, caster.Map))
                {
                    return false;
                }

                // A composite form owns its own aim limit, so an unlimited (0 or less) railgun is
                // not dragged back to the 24.9 cells of the shared carrier AbilityDef.
                SpellRailgunSpec spec = plan.Railgun;
                float range = spec != null ? spec.range : SpellDefs.Tuning.castRange;
                IntVec3 from = caster.Position;
                return SpellAimRange.Within(range, from.x, from.z, pick.Cell.x, pick.Cell.z);
            }

            return IsTargetable(caster, plan?.form ?? SpellFormKind.Projectile, pick);
        }

        /// <summary>Range, map, line of sight and Touch adjacency for one candidate target.</summary>
        public static bool IsTargetable(Pawn caster, SpellFormKind form, TargetInfo pick)
        {
            if (caster == null || !caster.Spawned || caster.Map == null || !pick.IsValid)
            {
                return false;
            }

            Map map = caster.Map;
            if (pick.Map != map)
            {
                return false;
            }

            IntVec3 cell = pick.Cell;
            if (!GenGrid.InBounds(cell, map))
            {
                return false;
            }

            SpellTuningDef tuning = SpellDefs.Tuning;
            if (form == SpellFormKind.Touch)
            {
                return IsInTouchReach(caster.Position, cell) && HasLineOfSight(caster, cell, map);
            }

            IntVec3 origin = caster.Position;
            return SpellAimRange.Within(tuning.castRange, origin.x, origin.z, cell.x, cell.z)
                   && HasLineOfSight(caster, cell, map);
        }

        /// <summary>8-way neighbour or the caster's own cell; walls still block through LOS below.</summary>
        public static bool IsInTouchReach(IntVec3 from, IntVec3 to)
        {
            int dx = Mathf.Abs(from.x - to.x);
            int dz = Mathf.Abs(from.z - to.z);
            int max = Mathf.Max(dx, dz);
            return max <= 1 && (float)max <= SpellDefs.Tuning.touchRange;
        }

        /// <summary>
        /// Line of sight from the caster to that cell; the caster's own cell always counts as visible.
        /// Public because the casting service re-checks a touch target at release.
        /// </summary>
        public static bool HasLineOfSight(Pawn caster, IntVec3 cell, Map map)
        {
            return cell == caster.Position || GenSight.LineOfSight(caster.Position, cell, map, true);
        }

        // ---------------------------------------------------------------- book entry and ability grant

        /// <summary>Opens the book for this caster; the window itself refuses a second instance.</summary>
        public static void OpenSpellbook(Pawn caster)
        {
            if (caster == null)
            {
                return;
            }

            WindowStack stack = Find.WindowStack;
            if (stack == null)
            {
                return;
            }

            stack.Add(new Window_Spellbook(caster));
        }

        /// <summary>
        /// Gives the carrier ability to every pawn that qualifies right now. The ability is what puts
        /// the book button and the cast button into the pawn's native gizmo row, so no Harmony patch
        /// has to touch the pawn gizmo enumerators (both are iterators and cannot be appended to).
        /// </summary>
        public static void GrantCastAbilityToEligiblePawns()
        {
            AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail(SpellDefs.CastAbilityDefName);
            if (def == null)
            {
                SpellDiagnostics.ErrorOnce("ability def " + SpellDefs.CastAbilityDefName
                                           + " is missing; no pawn can open the spellbook.");
                return;
            }

            List<Map> maps = Find.Maps;
            if (maps == null)
            {
                return;
            }

            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                if (map?.mapPawns == null)
                {
                    continue;
                }

                IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
                for (int j = 0; j < pawns.Count; j++)
                {
                    Pawn pawn = pawns[j];
                    if (pawn == null || pawn.abilities == null || pawn.Dead || !pawn.IsPlayerControlled)
                    {
                        continue;
                    }

                    if (PawnUtility.GetPsylinkLevel(pawn) < 1)
                    {
                        continue;
                    }

                    if (pawn.abilities.GetAbility(def, true) == null)
                    {
                        pawn.abilities.GainAbility(def);
                    }
                }
            }
        }
    }
}
