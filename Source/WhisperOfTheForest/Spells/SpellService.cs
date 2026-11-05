using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Race;

namespace WhisperOfTheForest.Spells
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
        /// Documented requirement: a player-controlled, alive, spawned pawn carrying the forest
        /// bloodline gene and wearing a spellbook. Everything else is refused with a visible reason,
        /// never silently.
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

            if (Gene_ForestBloodline.Get(pawn) == null)
            {
                reason = SpellText.T("WotF_Spells.Reason.NoBloodline");
                return false;
            }

            if (WornSpellbook(pawn) == null)
            {
                reason = SpellText.T("WotF_Spells.Reason.NoSpellbook");
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

            return CanAfford(pawn, recipe.cost ?? SpellCost.Derive(recipe.glyphs), out reason);
        }

        // ---------------------------------------------------------------- spellbook and resources

        /// <summary>The spellbook the pawn wears, or null.</summary>
        public static Apparel WornSpellbook(Pawn pawn)
        {
            List<Apparel> worn = pawn?.apparel?.WornApparel;
            if (worn == null)
            {
                return null;
            }

            for (int i = 0; i < worn.Count; i++)
            {
                if (worn[i].def.HasModExtension<SpellbookExtension>())
                {
                    return worn[i];
                }
            }

            return null;
        }

        /// <summary>How many of the pawn's spell slots its worn book opens; 0 without a book.</summary>
        public static int OpenSlotCount(Pawn pawn)
        {
            Apparel book = WornSpellbook(pawn);
            return book == null ? 0 : book.def.GetModExtension<SpellbookExtension>().slotCount;
        }

        /// <summary>
        /// Whether the pawn can pay this cost right now: enough mana, and the neural heat fits under
        /// the vanilla limit (unless the player switched that limit off, exactly like a psycast).
        /// </summary>
        public static bool CanAfford(Pawn pawn, SpellCost cost, out string reason)
        {
            reason = null;
            Gene_ForestBloodline bloodline = Gene_ForestBloodline.Get(pawn);
            if (cost.mana > 0f && (bloodline == null || !bloodline.HasMana(cost.mana)))
            {
                reason = SpellText.TF("WotF_Spells.Reason.NotEnoughMana", cost.mana.ToString("0.#"),
                    bloodline == null ? "0" : bloodline.CurrentMana.ToString("0.#"));
                return false;
            }

            if (cost.entropy > 0f)
            {
                if (pawn.psychicEntropy == null)
                {
                    reason = SpellText.T("WotF_Spells.Reason.NoNeuralHeat");
                    return false;
                }

                if (pawn.psychicEntropy.WouldOverflowEntropy(cost.entropy))
                {
                    reason = SpellText.TF("WotF_Spells.Reason.NeuralHeatOverflow", cost.entropy.ToString("0.#"));
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Spends the cost of a released spell and grants its experience. The caller checked
        /// <see cref="CanAfford"/> in the same tick, so both payments succeed or neither is tried.
        /// </summary>
        public static void PayFor(Pawn pawn, SpellCost cost)
        {
            Gene_ForestBloodline bloodline = Gene_ForestBloodline.Get(pawn);
            if (cost.mana > 0f)
            {
                bloodline.TrySpendMana(cost.mana);
            }

            if (cost.entropy > 0f)
            {
                pawn.psychicEntropy.TryAddEntropy(cost.entropy, pawn);
            }

            if (cost.experience > 0f)
            {
                bloodline.GainExperience(cost.experience);
            }
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

                // A composite form owns its own aim limit; 0 or less is the whole map.
                IntVec3 from = caster.Position;
                return SpellAimRange.Within(plan.railgun.range, from.x, from.z, pick.Cell.x, pick.Cell.z);
            }

            return plan != null && IsTargetable(caster, plan.form, plan.range, pick);
        }

        /// <summary>
        /// Range, map, line of sight and Touch reach for one candidate target. The range is the form's
        /// own; Self and Orbit have none, so they accept only the caster's own cell.
        /// </summary>
        public static bool IsTargetable(Pawn caster, SpellFormKind form, float range, TargetInfo pick)
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

            if (form == SpellFormKind.Self || form == SpellFormKind.Orbit)
            {
                return cell == caster.Position;
            }

            if (form == SpellFormKind.Touch)
            {
                return IsInTouchReach(caster.Position, cell, range) && HasLineOfSight(caster, cell, map);
            }

            IntVec3 origin = caster.Position;
            return SpellAimRange.Within(range, origin.x, origin.z, cell.x, cell.z)
                   && HasLineOfSight(caster, cell, map);
        }

        /// <summary>
        /// Touch reach counted in king moves, so a reach of 1.45 means the 8 neighbours and the
        /// caster's own cell; walls still block through LOS.
        /// </summary>
        public static bool IsInTouchReach(IntVec3 from, IntVec3 to, float reach)
        {
            int dx = Mathf.Abs(from.x - to.x);
            int dz = Mathf.Abs(from.z - to.z);
            return Mathf.Max(dx, dz) <= reach;
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

            if (SpellDefs.Tuning == null)
            {
                Messages.Message(SpellText.T("WotF_Spells.Reason.TuningMissing"), MessageTypeDefOf.RejectInput, false);
                return;
            }

            // The spells are the pawn's, but only a worn book opens them.
            if (WornSpellbook(caster) == null)
            {
                Reject(SpellText.T("WotF_Spells.Reason.NoSpellbook"));
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

            if (SpellDefs.Tuning == null)
            {
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

                    if (Gene_ForestBloodline.Get(pawn) == null)
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
