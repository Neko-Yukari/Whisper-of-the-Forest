using System.Collections.Generic;
using Verse;
using WhisperOfTheForest.Missiles;

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
        /// The composite form: a single rune that is already a whole spell. It charges for a number
        /// of ticks and then resolves a full-penetration corridor from the caster's position at the
        /// moment it fires. It takes no effect, augment or trigger rune.
        /// </summary>
        Railgun,

        /// <summary>
        /// Orbs circling the caster. They need no target selection and carry the spell's effects
        /// themselves; the orbit numbers live on <see cref="SpellCastPlan.orbit"/>.
        /// </summary>
        Orbit
    }

    /// <summary>
    /// What one effect slot of a compiled chain does. The last three are delivery wrappers from the
    /// spell table's "change when and where" group: they hold the remaining effects as a payload and
    /// run it later, at the impact point, or when something steps on the seal.
    /// </summary>
    public enum SpellEffectKind
    {
        Harm,
        Heal,
        Explosion,
        Grow,
        Ignite,
        Lightning,
        Freeze,
        ColdSnap,
        Knockback,
        Pull,
        Snare,
        Hex,
        Magelight,
        Blink,
        Linger,
        Rune,
        Delay,
        Hack
    }

    /// <summary>
    /// The frozen payload that Linger, Rune and Delay carry. It is deliberately a plain list of the
    /// same steps a plan holds, so the fields resolve through the one executor and nothing about
    /// the effect implementations is duplicated for the delayed path.
    /// </summary>
    public class SpellPayload : IExposable
    {
        public List<SpellPlanStep> steps = new List<SpellPlanStep>();

        public SpellPayload Copy()
        {
            var copy = new SpellPayload { steps = new List<SpellPlanStep>(steps?.Count ?? 0) };
            if (steps != null)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    copy.steps.Add(steps[i]?.Copy());
                }
            }

            return copy;
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref steps, "steps", LookMode.Deep);
            if (steps == null)
            {
                steps = new List<SpellPlanStep>();
            }
        }
    }

    /// <summary>
    /// One resolved effect of a chain. The numbers are already combined with every augment that was
    /// bound to this effect, so executing the plan needs no glyph lookup and cannot change later.
    /// </summary>
    public class SpellPlanStep : IExposable
    {
        public SpellEffectKind effect;

        /// <summary>Damage, healing or explosion damage after Amplify. Negative means "not set yet".</summary>
        public float amount = -1f;

        /// <summary>
        /// Working radius in cells: the effect's own radius plus AOE. An explosion and a field always
        /// use it; every other effect only when <see cref="aoe"/> is true.
        /// </summary>
        public float radius = -1f;

        /// <summary>Number of Amplify augments that were folded into <see cref="amount"/>.</summary>
        public int amplifyStacks;

        /// <summary>Number of AOE augments that were folded into <see cref="radius"/>.</summary>
        public int aoeStacks;

        public bool aoe;

        /// <summary>
        /// Combined Amplify/Dampen multiplier, after the tuning cap. Kept so effects whose strength is
        /// not a damage number (glow radius, blink distance, tend quality) scale the same way.
        /// </summary>
        public float magnitudeFactor = 1f;

        /// <summary>Number of Dampen augments folded into <see cref="magnitudeFactor"/>.</summary>
        public int dampenStacks;

        /// <summary>Duration this step keeps in effect; 0 means instant.</summary>
        public int durationTicks;

        /// <summary>Duration the glyph declared before Extend/Shorten; 0 when it has none.</summary>
        public int baseDurationTicks;

        /// <summary>Harm only: resolve as a periodic wound instead of one hit.</summary>
        public bool dot;

        /// <summary>Pulse spacing for a periodic wound, a Linger beat or the like.</summary>
        public int pulseIntervalTicks;

        /// <summary>Armour penetration: the effect's own value plus Break Armor.</summary>
        public float penetration;

        /// <summary>Explosion/ignite: keep the harmless variant (no buildings damaged, no spreading fire).</summary>
        public bool sensitive;

        /// <summary>Freeze: damage multiplier against an already slowed target (Cold Snap).</summary>
        public float vulnerableFactor;

        /// <summary>Lightning: EMP damage delivered before the burn.</summary>
        public float empAmount;

        /// <summary>Lightning: extra damage per charge stack already on the target.</summary>
        public float perStackFactor;

        /// <summary>Ignite: vanilla fire size for the spreading variant.</summary>
        public float fireSize;

        /// <summary>Ignite (Sensitive): damage per pulse of the non-spreading burn.</summary>
        public float burnAmount;

        /// <summary>Magelight: light radius.</summary>
        public float glowRadius;

        /// <summary>Magelight: light colour, as an HTML hex string.</summary>
        public string glowColor;

        /// <summary>
        /// The glyph's own magnitude for effects that are not a damage number (tend quality, soil
        /// fertility multiplier, ...). Amplify/Dampen reach it through <see cref="magnitudeFactor"/>.
        /// </summary>
        public float factor;

        /// <summary>Orbit/Linger wrappers only: the frozen payload this step runs later or elsewhere.</summary>
        public SpellPayload payload;

        /// <summary>An independent copy; a payload copy never shares a step object with its source.</summary>
        public SpellPlanStep Copy()
        {
            return new SpellPlanStep
            {
                effect = effect,
                amount = amount,
                radius = radius,
                amplifyStacks = amplifyStacks,
                aoeStacks = aoeStacks,
                aoe = aoe,
                magnitudeFactor = magnitudeFactor,
                dampenStacks = dampenStacks,
                durationTicks = durationTicks,
                baseDurationTicks = baseDurationTicks,
                dot = dot,
                pulseIntervalTicks = pulseIntervalTicks,
                penetration = penetration,
                sensitive = sensitive,
                vulnerableFactor = vulnerableFactor,
                empAmount = empAmount,
                perStackFactor = perStackFactor,
                fireSize = fireSize,
                burnAmount = burnAmount,
                glowRadius = glowRadius,
                glowColor = glowColor,
                factor = factor,
                payload = payload?.Copy()
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref effect, "effect", SpellEffectKind.Harm);
            Scribe_Values.Look(ref amount, "amount", -1f);
            Scribe_Values.Look(ref radius, "radius", -1f);
            Scribe_Values.Look(ref amplifyStacks, "amplifyStacks", 0);
            Scribe_Values.Look(ref aoeStacks, "aoeStacks", 0);
            Scribe_Values.Look(ref aoe, "aoe", false);
            Scribe_Values.Look(ref magnitudeFactor, "magnitudeFactor", 1f);
            Scribe_Values.Look(ref dampenStacks, "dampenStacks", 0);
            Scribe_Values.Look(ref durationTicks, "durationTicks", 0);
            Scribe_Values.Look(ref baseDurationTicks, "baseDurationTicks", 0);
            Scribe_Values.Look(ref dot, "dot", false);
            Scribe_Values.Look(ref pulseIntervalTicks, "pulseIntervalTicks", 0);
            Scribe_Values.Look(ref penetration, "penetration", 0f);
            Scribe_Values.Look(ref sensitive, "sensitive", false);
            Scribe_Values.Look(ref vulnerableFactor, "vulnerableFactor", 0f);
            Scribe_Values.Look(ref empAmount, "empAmount", 0f);
            Scribe_Values.Look(ref perStackFactor, "perStackFactor", 0f);
            Scribe_Values.Look(ref fireSize, "fireSize", 0f);
            Scribe_Values.Look(ref burnAmount, "burnAmount", 0f);
            Scribe_Values.Look(ref glowRadius, "glowRadius", 0f);
            Scribe_Values.Look(ref glowColor, "glowColor");
            Scribe_Values.Look(ref factor, "factor", 0f);
            Scribe_Deep.Look(ref payload, "payload");
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
        /// Frozen numbers of a composite form (<see cref="SpellFormKind.Railgun"/>); null for every
        /// other form. A composite plan carries no <see cref="steps"/> of its own.
        /// </summary>
        public SpellRailgunSpec railgun;

        /// <summary>Pierce: how many further hits the spell may resolve after the first.</summary>
        public int pierceStacks;

        public List<SpellPlanStep> steps = new List<SpellPlanStep>();

        public bool HasEffect(SpellEffectKind kind)
        {
            return HasEffectIn(steps, kind);
        }

        /// <summary>True for a step list, including effects hidden inside a Linger/Rune/Delay payload.</summary>
        public static bool HasEffectIn(List<SpellPlanStep> list, SpellEffectKind kind)
        {
            if (list == null)
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                SpellPlanStep step = list[i];
                if (step == null)
                {
                    continue;
                }

                if (step.effect == kind || (step.payload != null && HasEffectIn(step.payload.steps, kind)))
                {
                    return true;
                }
            }

            return false;
        }

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
            Scribe_Values.Look(ref pierceStacks, "pierceStacks");
            Scribe_Collections.Look(ref steps, "steps", LookMode.Deep);

            // Scribing only ever clears these while loading; a null list would break every lookup.
            if (glyphs == null)
            {
                glyphs = new List<string>();
            }

            if (steps == null)
            {
                steps = new List<SpellPlanStep>();
            }

            if (targets == null)
            {
                targets = new List<SpellTargetClass>();
            }
        }
    }
}
