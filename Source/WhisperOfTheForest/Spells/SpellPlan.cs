using System.Collections.Generic;
using Verse;
using WhisperOfTheForest.Missiles;
using WhisperOfTheForest.Vfx;

namespace WhisperOfTheForest.Spells
{
    /// <summary>Which target the spell needs before anything runs.</summary>
    public enum SpellFormKind
    {
        /// <summary>Runs on the caster and the caster's own cell; no target selection.</summary>
        Self,

        /// <summary>Runs on an adjacent, reachable target only; never through a wall.</summary>
        Touch,

        /// <summary>Flies as the existing native homing projectile and runs where it really hits.</summary>
        Projectile,

        /// <summary>
        /// A single Super rune with its own complete payload; it carries no effect steps.
        /// </summary>
        Special,

        /// <summary>
        /// Orbs circling the caster. They need no target selection and carry the spell's effects
        /// themselves; the orbit numbers live on <see cref="SpellCastPlan.orbit"/>.
        /// </summary>
        Orbit
    }

    /// <summary>
    /// The frozen payload that Linger, Rune and Delay carry. It is deliberately a plain list of the
    /// same steps a plan holds, so the fields resolve through the one executor and nothing about
    /// the effect implementations is duplicated for the delayed path.
    /// </summary>
    public class SpellPayload : IExposable
    {
        public List<SpellEffect> steps = new List<SpellEffect>();
        public SpellThemeDef theme;

        public SpellPayload Copy()
        {
            return new SpellPayload
            {
                steps = SpellExecutor.CopySteps(steps),
                theme = theme
            };
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref steps, "steps", LookMode.Deep);
            Scribe_Defs.Look(ref theme, "theme");
            SpellEffect.CheckLoaded(steps, GetType().Name);
            if (steps == null)
            {
                steps = new List<SpellEffect>();
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit && theme == null)
            {
                theme = SpellDefs.Tuning?.defaultTheme;
            }
        }
    }

    /// <summary>
    /// The frozen result of one recipe: a Form with its own numbers copied from the form Def (range,
    /// the projectile, orbit or railgun block, each with the form's augments applied) and the Effects
    /// with every augment already folded in. A spell is one straight line, so this is the whole body.
    ///
    /// A cast freezes this at the moment it starts (and, for a projectile, it travels inside the
    /// dart's payload), so editing a slot, switching bookmarks or reloading can never rewrite a spell
    /// that is already in the air or already resolved.
    /// </summary>
    public class SpellCastPlan : IExposable
    {
        /// <summary>Recipe name at the time of casting; display/log text only.</summary>
        public string recipeName;

        /// <summary>Glyph defNames in order, kept so a save/log can explain what was cast.</summary>
        public List<string> glyphs = new List<string>();

        /// <summary>
        /// Phase lengths of this cast, copied from the recipe when it was compiled. Zero in any phase
        /// means that phase is already over - the one rule that makes an instant spell take the same
        /// path as a channelled one.
        /// </summary>
        public SpellTiming timing;

        /// <summary>Cost of this cast, copied from the recipe when it was compiled.</summary>
        public SpellCost cost;

        /// <summary>
        /// The categories this cast may be aimed at, frozen into the recipe when the spell was
        /// created. Every targeter - player, AI and turret - reads only this list.
        /// </summary>
        public List<SpellTargetClass> targets = new List<SpellTargetClass>();

        public SpellFormKind form;

        /// <summary>Projectile and Touch: aim or reach in cells, copied from the form Def.</summary>
        public float range;

        /// <summary>Projectile: the form's dart settings after Split and Accelerate/Decelerate; null otherwise.</summary>
        public ArcaneMissileSettings projectile;

        /// <summary>Orbit: the form's orb numbers after its augments; null otherwise.</summary>
        public SpellOrbitSpec orbit;

        /// <summary>
        /// Frozen numbers of a composite form (<see cref="SpellFormKind.Special"/>); null for every
        /// other form. A composite plan carries no <see cref="steps"/> of its own.
        /// </summary>
        public SpellRailgunSpec railgun;
        public SpellTornadoSpec tornado;
        public ISpellSpecialSpec Special => (ISpellSpecialSpec)railgun ?? tornado;

        /// <summary>Pierce: how many further hits the spell may resolve after the first.</summary>
        public int pierceStacks;

        public List<SpellEffect> steps = new List<SpellEffect>();

        /// <summary>Deep copy; the caller and the callee never share a list or a step object.</summary>
        public SpellCastPlan Copy()
        {
            return new SpellCastPlan
            {
                recipeName = recipeName,
                glyphs = new List<string>(glyphs),
                timing = timing?.Copy(),
                cost = cost?.Copy(),
                targets = new List<SpellTargetClass>(targets),
                form = form,
                range = range,
                projectile = projectile?.Copy(),
                orbit = orbit?.Copy(),
                railgun = railgun?.Copy(),
                tornado = tornado?.Copy(),
                pierceStacks = pierceStacks,
                steps = SpellExecutor.CopySteps(steps)
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref recipeName, "recipeName");
            Scribe_Collections.Look(ref glyphs, "glyphs", LookMode.Value);
            Scribe_Deep.Look(ref timing, "timing");
            Scribe_Deep.Look(ref cost, "cost");
            Scribe_Collections.Look(ref targets, "targets", LookMode.Value);
            Scribe_Values.Look(ref form, "form");
            Scribe_Values.Look(ref range, "range");
            Scribe_Deep.Look(ref projectile, "projectile");
            Scribe_Deep.Look(ref orbit, "orbit");
            Scribe_Deep.Look(ref railgun, "railgun");
            Scribe_Deep.Look(ref tornado, "tornado");
            Scribe_Values.Look(ref pierceStacks, "pierceStacks");
            Scribe_Collections.Look(ref steps, "steps", LookMode.Deep);
            SpellEffect.CheckLoaded(steps, GetType().Name);

            // Scribing only ever clears these while loading; a null list would break every lookup.
            if (glyphs == null)
            {
                glyphs = new List<string>();
            }

            if (steps == null)
            {
                steps = new List<SpellEffect>();
            }

            if (targets == null)
            {
                targets = new List<SpellTargetClass>();
            }
        }
    }
}
