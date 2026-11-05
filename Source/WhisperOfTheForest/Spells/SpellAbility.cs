using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// The native carrier for the spellbook: a real <see cref="Ability"/> whose gizmos are the book
    /// button and the "cast the selected spell" button.
    ///
    /// A native ability is used on purpose. Both <c>Pawn.GetGizmos</c> and
    /// <c>ThingWithComps.GetGizmos</c> are C# iterators in 1.7, so a Harmony postfix cannot append to
    /// them; the ability tracker already enumerates abilities for every player-controlled pawn, so
    /// granting this ability is the supported way to get native gizmos, a native targeter and a
    /// saved per-pawn state. <see cref="SpellService.GrantCastAbilityToEligiblePawns"/> keeps it on
    /// pawns that qualify (player-controlled, spawned, alive, psylink level 1+).
    /// </summary>
    [StaticConstructorOnStartup]
    public class SpellAbility : Ability
    {
        // Cached textures must live in a StaticConstructorOnStartup type: the game warns about (and
        // drops) static Unity asset references declared by a class without that attribute.
        private static Texture2D bookIcon;
        private static bool bookIconResolved;

        public SpellAbility()
        {
        }

        public SpellAbility(Pawn pawn) : base(pawn)
        {
        }

        public SpellAbility(Pawn pawn, AbilityDef def) : base(pawn, def)
        {
        }

        /// <summary>
        /// Same requirement as the service, so the ability's own UI state never claims a cast the
        /// runtime would refuse.
        /// </summary>
        public override AcceptanceReport CanCast
        {
            get
            {
                string reason;
                if (!SpellService.IsEligibleCaster(pawn, out reason))
                {
                    return reason;
                }

                GameComponent_Spellbook component = GameComponent_Spellbook.Current;
                SpellRecipe recipe = component?.GetActiveRecipe(pawn);
                if (recipe == null)
                {
                    return SpellText.T("WotF_Spells.Reason.NoSpellSelected");
                }

                return SpellService.CanCast(pawn, recipe, out reason) ? AcceptanceReport.WasAccepted : reason;
            }
        }

        /// <summary>
        /// Carrier only: this ability draws the book/cast gizmos and nothing else. The vanilla
        /// <c>Ability.AbilityTick</c> drives the verb tracker, warmup, casting, motes and the Def
        /// cooldown, and <c>Ability.WarmupTick</c> dereferences <c>Ability.verb</c> unconditionally
        /// (a22 Player.log 01:05:33: the AbilityDef had no instanciable verbClass, so verb was null
        /// and every tick threw a NullReferenceException inside WarmupTick). Nothing here warms up -
        /// <see cref="SpellService"/> casts synchronously and <see cref="GameComponent_Spellbook"/>
        /// owns the per-pawn cooldown - so the tick is deliberately empty and never calls the base
        /// implementation. The Def still declares a concrete verbClass so Ability's own verb
        /// machinery initialises without logging an error.
        /// </summary>
        public override void AbilityTick()
        {
        }

        public override IEnumerable<Command> GetGizmos()
        {
            yield return OpenBookCommand();

            Command cast = CastCommand();
            if (cast != null)
            {
                yield return cast;
            }
        }

        private Command OpenBookCommand()
        {
            Pawn caster = pawn;
            return new Command_Action
            {
                defaultLabel = SpellText.T("WotF_Spells.Gizmo.OpenBook"),
                defaultDesc = SpellText.T("WotF_Spells.Gizmo.OpenBookDesc"),
                icon = bookIconResolved ? bookIcon : ResolveIcon(),
                action = delegate { SpellService.OpenSpellbook(caster); }
            };
        }

        /// <summary>
        /// The cast button for the pawn's selected slot. Self spells fire on click (no target
        /// click needed); Touch and Projectile open the native targeter. Missing spell, cooldown and
        /// an ineligible caster all disable the button with a visible reason instead of hiding why.
        /// </summary>
        private Command CastCommand()
        {
            Pawn caster = pawn;
            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            int slot = component == null ? -1 : component.GetActiveSlot(caster);
            SpellRecipe recipe = component?.GetSlot(caster, slot);

            if (recipe == null)
            {
                // A spell kept in a slot the worn book does not open says so instead of looking empty.
                bool sealedSpell = component != null && component.StoredSpellName(caster, slot) != null;
                return new Command_Action
                {
                    defaultLabel = SpellText.T("WotF_Spells.Gizmo.NoSpell"),
                    defaultDesc = SpellText.T("WotF_Spells.Gizmo.NoSpellDesc"),
                    icon = bookIconResolved ? bookIcon : ResolveIcon(),
                    action = delegate { SpellService.OpenSpellbook(caster); },
                    Disabled = true,
                    disabledReason = sealedSpell
                        ? SpellText.TF("WotF_Spells.Reason.SlotSealed", slot + 1)
                        : SpellText.TF("WotF_Spells.Gizmo.NoSpellReason", slot + 1)
                };
            }

            string reason;
            SpellCastPlan plan = SpellCompiler.Compile(recipe, out reason);
            bool castable = plan != null && SpellService.CanCast(caster, recipe, out reason);

            Command command;
            if (plan != null && (plan.form == SpellFormKind.Self || plan.form == SpellFormKind.Orbit))
            {
                command = new Command_Action
                {
                    action = delegate { SpellService.BeginTargeting(caster, recipe); }
                };
            }
            else
            {
                // Native targeter from the shared parameter builder, so the gizmo and the book page
                // accept exactly the same targets. The click goes through the same cast entry point,
                // which re-checks eligibility, range and cooldown.
                command = new Command_Target
                {
                    targetingParams = plan != null
                        ? SpellService.BuildTargetingParameters(caster, plan)
                        : new TargetingParameters(),
                    action = delegate(LocalTargetInfo pick)
                    {
                        string failure;
                        if (!SpellService.TryCast(caster, recipe, pick, out failure))
                        {
                            SpellService.Reject(failure);
                        }
                    }
                };
            }

            command.defaultLabel = recipe.name.NullOrEmpty() ? SpellText.T("WotF_Spells.Gizmo.Unnamed") : recipe.name;
            command.defaultDesc = SpellText.TF("WotF_Spells.Gizmo.CastDesc", slot + 1);
            command.icon = IconFor(recipe);

            if (!castable)
            {
                command.Disabled = true;
                command.disabledReason = reason.NullOrEmpty() ? SpellText.T("WotF_Spells.Reason.Unknown") : reason;
            }

            return command;
        }

        private static Texture2D IconFor(SpellRecipe recipe)
        {
            if (recipe?.glyphs != null && recipe.glyphs.Count > 0)
            {
                string path = SpellDefs.IconPathFor(SpellDefs.Glyph(recipe.glyphs[0]));
                if (!path.NullOrEmpty())
                {
                    Texture2D formIcon = ContentFinder<Texture2D>.Get(path, false);
                    if (formIcon != null)
                    {
                        return formIcon;
                    }
                }
            }

            return bookIconResolved ? bookIcon : ResolveIcon();
        }

        private static Texture2D ResolveIcon()
        {
            bookIconResolved = true;
            // The experiment's own icon is always shipped; glyph art belongs to the interface task.
            bookIcon = ContentFinder<Texture2D>.Get("UI/TechLabIcon", false);
            return bookIcon;
        }
    }
}

