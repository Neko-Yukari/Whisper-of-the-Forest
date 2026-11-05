using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Experimental.Spells
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
        /// Orbs circling the caster. They need no target selection and carry the segment's effects
        /// themselves; the orbit numbers live on <see cref="SpellSegment"/>.
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
        Delay
    }

    /// <summary>
    /// The frozen payload that Linger, Rune and Delay carry. It is deliberately a plain list of the
    /// same steps a segment holds, so the fields resolve through the one executor and nothing about
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

        /// <summary>Working radius in cells after AOE. Only used when <see cref="aoe"/> is true.</summary>
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

        /// <summary>Extra armour penetration from Break Armor, on top of the tuning base.</summary>
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
    /// One segment of a longer spell: a Form, its Effects with every augment already folded in, the
    /// projectile numbers that belong to this segment, and whether a trigger rune ends it.
    ///
    /// A trigger rune cuts one chain into ordered segments. The first segment is delivered by the
    /// caster; every later segment starts where the previous one really hit, so each segment owns its
    /// own form, numbers and "continue after a hit" flag. Nothing is evaluated backwards: a segment
    /// only ever points at the next one.
    /// </summary>
    public class SpellSegment : IExposable
    {
        public SpellFormKind form = SpellFormKind.Projectile;

        public List<SpellPlanStep> steps = new List<SpellPlanStep>();

        /// <summary>Missiles this segment launches after Split.</summary>
        public int missileCount = 1;

        /// <summary>Missile speed in cells per second after Accelerate/Decelerate.</summary>
        public float missileSpeed = 16f;

        /// <summary>True when a trigger rune ends this segment and a real hit continues the chain.</summary>
        public bool triggerOnHit;

        /// <summary>Orbit: how far from the caster the orbs circle.</summary>
        public float orbitDistance;

        /// <summary>Orbit: turn rate in degrees per second.</summary>
        public float orbitSpeedDegrees;

        /// <summary>Orbit: how close a pawn must come to be touched.</summary>
        public float orbitHitRadius;

        /// <summary>Orbit: how long one orb stays.</summary>
        public int orbitLifetimeTicks;

        /// <summary>Pierce: how many further hits this segment may resolve after the first.</summary>
        public int pierceStacks;

        /// <summary>
        /// Frozen numbers of a composite form (<see cref="SpellFormKind.Railgun"/>); null for every
        /// other form. A composite segment carries no <see cref="steps"/> of its own.
        /// </summary>
        public SpellRailgunSpec railgun;

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

        public float LargestAmount(SpellEffectKind kind)
        {
            float best = 0f;
            if (steps == null)
            {
                return best;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] != null && steps[i].effect == kind && steps[i].amount > best)
                {
                    best = steps[i].amount;
                }
            }

            return best;
        }

        /// <summary>Widest working radius this segment carries for one effect; 0 when it has none.</summary>
        public float LargestRadius(SpellEffectKind kind)
        {
            float best = 0f;
            if (steps == null)
            {
                return best;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] != null && steps[i].effect == kind && steps[i].radius > best)
                {
                    best = steps[i].radius;
                }
            }

            return best;
        }

        /// <summary>Deep copy; the caller and the callee never share a list or a step object.</summary>
        public SpellSegment Copy()
        {
            var copy = new SpellSegment
            {
                form = form,
                missileCount = missileCount,
                missileSpeed = missileSpeed,
                triggerOnHit = triggerOnHit,
                orbitDistance = orbitDistance,
                orbitSpeedDegrees = orbitSpeedDegrees,
                orbitHitRadius = orbitHitRadius,
                orbitLifetimeTicks = orbitLifetimeTicks,
                pierceStacks = pierceStacks,
                railgun = railgun?.Copy(),
                steps = new List<SpellPlanStep>(steps?.Count ?? 0)
            };

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
            Scribe_Values.Look(ref form, "form", SpellFormKind.Projectile);
            Scribe_Values.Look(ref missileCount, "missileCount", 1);
            Scribe_Values.Look(ref missileSpeed, "missileSpeed", 16f);
            Scribe_Values.Look(ref triggerOnHit, "triggerOnHit", false);
            Scribe_Values.Look(ref orbitDistance, "orbitDistance", 0f);
            Scribe_Values.Look(ref orbitSpeedDegrees, "orbitSpeedDegrees", 0f);
            Scribe_Values.Look(ref orbitHitRadius, "orbitHitRadius", 0f);
            Scribe_Values.Look(ref orbitLifetimeTicks, "orbitLifetimeTicks", 0);
            Scribe_Values.Look(ref pierceStacks, "pierceStacks", 0);
            Scribe_Deep.Look(ref railgun, "railgun");
            Scribe_Collections.Look(ref steps, "steps", LookMode.Deep);

            // Scribing only ever clears these while loading; a null list would break every lookup.
            if (steps == null)
            {
                steps = new List<SpellPlanStep>();
            }
        }
    }

    /// <summary>
    /// The frozen result of one recipe: the ordered segments (one per trigger rune), plus the
    /// first segment mirrored into the pre-trigger fields.
    ///
    /// A cast freezes this at the moment it starts (and, for a projectile, it travels inside the
    /// missile's own saved settings copy), so editing a slot, switching bookmarks or reloading can
    /// never rewrite a spell that is already in the air or already resolved.
    ///
    /// <see cref="form"/>, <see cref="steps"/>, <see cref="missileCount"/> and <see cref="missileSpeed"/>
    /// are kept as aliases of <see cref="segments"/>[0]: saves written before trigger runes existed
    /// only know those fields, and callers that only care about the first segment keep working.
    /// </summary>
    public class SpellCastPlan : IExposable
    {
        /// <summary>Recipe name at the time of casting; display/log text only.</summary>
        public string recipeName;

        /// <summary>Mirror of the first segment; the authority is <see cref="segments"/>.</summary>
        public SpellFormKind form = SpellFormKind.Projectile;

        /// <summary>Glyph defNames in order, kept so a save/log can explain what was cast.</summary>
        public List<string> glyphs = new List<string>();

        /// <summary>Mirror of the first segment's effects.</summary>
        public List<SpellPlanStep> steps = new List<SpellPlanStep>();

        /// <summary>Mirror of the first segment's volley size after Split.</summary>
        public int missileCount = 1;

        /// <summary>Mirror of the first segment's missile speed after Accelerate/Decelerate.</summary>
        public float missileSpeed = 16f;

        /// <summary>One entry per segment, in cast order. Never empty after <see cref="Normalize"/>.</summary>
        public List<SpellSegment> segments = new List<SpellSegment>();

        /// <summary>
        /// Phase lengths of this cast, copied from the recipe when it was compiled. Zero in any phase
        /// means that phase is already over - the one rule that makes an instant spell take the same
        /// path as a channelled one.
        /// </summary>
        public SpellTiming timing;

        public int SegmentCount => segments == null ? 0 : segments.Count;

        /// <summary>The composite-form numbers of the first segment, or null for an ordinary spell.</summary>
        public SpellRailgunSpec Railgun => SegmentAt(0)?.railgun;

        /// <summary>The segment at that index, or null when the pointer ran past the plan.</summary>
        public SpellSegment SegmentAt(int index)
        {
            if (segments == null || index < 0 || index >= segments.Count)
            {
                return null;
            }

            return segments[index];
        }

        /// <summary>True when any segment carries that effect, so a targeter offers the right targets.</summary>
        public bool HasEffect(SpellEffectKind kind)
        {
            if (segments != null && segments.Count > 0)
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    if (segments[i] != null && segments[i].HasEffect(kind))
                    {
                        return true;
                    }
                }

                return false;
            }

            return StepsHasEffect(steps, kind);
        }

        public float LargestAmount(SpellEffectKind kind)
        {
            float best = 0f;
            if (segments != null && segments.Count > 0)
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    if (segments[i] == null)
                    {
                        continue;
                    }

                    float amount = segments[i].LargestAmount(kind);
                    if (amount > best)
                    {
                        best = amount;
                    }
                }

                return best;
            }

            return StepsLargestAmount(steps, kind);
        }

        /// <summary>
        /// Makes <see cref="segments"/> and the mirrored first-segment fields agree.
        ///
        /// A save written before trigger runes has no segments: it is the one-segment spell it always
        /// was, with <see cref="SpellSegment.triggerOnHit"/> false, so it still flies and still ends on
        /// its real hit. A plan that already has segments only refreshes the mirrors, which keeps the
        /// legacy fields trustworthy for older callers instead of turning them into a second truth.
        /// </summary>
        public void Normalize()
        {
            if (segments == null)
            {
                segments = new List<SpellSegment>();
            }

            if (segments.Count == 0)
            {
                segments.Add(new SpellSegment
                {
                    form = form,
                    steps = steps ?? new List<SpellPlanStep>(),
                    missileCount = missileCount,
                    missileSpeed = missileSpeed,
                    triggerOnHit = false
                });
            }

            if (segments[0] == null)
            {
                segments[0] = new SpellSegment();
            }

            SpellSegment first = segments[0];
            if (first.steps == null)
            {
                first.steps = new List<SpellPlanStep>();
            }

            form = first.form;
            steps = first.steps;
            missileCount = first.missileCount;
            missileSpeed = first.missileSpeed;
        }

        /// <summary>Deep copy; the caller and the callee never share a list or a step object.</summary>
        public SpellCastPlan Copy()
        {
            var copy = new SpellCastPlan
            {
                recipeName = recipeName,
                form = form,
                missileCount = missileCount,
                missileSpeed = missileSpeed,
                glyphs = glyphs == null ? new List<string>() : new List<string>(glyphs),
                timing = timing?.Copy(),
                steps = new List<SpellPlanStep>(steps?.Count ?? 0),
                segments = new List<SpellSegment>(segments?.Count ?? 0)
            };

            if (steps != null)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    copy.steps.Add(steps[i]?.Copy());
                }
            }

            if (segments != null)
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    copy.segments.Add(segments[i]?.Copy());
                }
            }

            copy.Normalize();
            return copy;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref recipeName, "recipeName");
            Scribe_Values.Look(ref form, "form", SpellFormKind.Projectile);
            Scribe_Values.Look(ref missileCount, "missileCount", 1);
            Scribe_Values.Look(ref missileSpeed, "missileSpeed", 16f);
            Scribe_Collections.Look(ref glyphs, "glyphs", LookMode.Value);
            Scribe_Collections.Look(ref steps, "steps", LookMode.Deep);
            Scribe_Collections.Look(ref segments, "segments", LookMode.Deep);
            Scribe_Deep.Look(ref timing, "timing");

            // A save written by an older build must still fly instead of throwing on a null list.
            // Scribing only ever clears these while loading, so a plain null check is enough and
            // keeps this file free of a game-mode dependency.
            if (glyphs == null)
            {
                glyphs = new List<string>();
            }

            if (steps == null)
            {
                steps = new List<SpellPlanStep>();
            }

            if (segments == null)
            {
                segments = new List<SpellSegment>();
            }

            // A plan scribed before timings existed (a dart in flight in an older save) has none; the
            // glyph list is its only source, so it is derived once here, at the save boundary.
            if (timing == null && Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                timing = SpellTiming.Derive(glyphs);
            }

            Normalize();
        }

        private static bool StepsHasEffect(List<SpellPlanStep> list, SpellEffectKind kind)
        {
            return SpellSegment.HasEffectIn(list, kind);
        }

        private static float StepsLargestAmount(List<SpellPlanStep> list, SpellEffectKind kind)
        {
            float best = 0f;
            if (list == null)
            {
                return best;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].effect == kind && list[i].amount > best)
                {
                    best = list[i].amount;
                }
            }

            return best;
        }
    }
}
