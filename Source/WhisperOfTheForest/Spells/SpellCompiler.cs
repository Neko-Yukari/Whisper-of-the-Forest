using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Experimental.Spells
{
    /// <summary>
    /// The single grammar of the spellbook: syntax validation, incremental "can I append this rune?"
    /// and compilation of a recipe into an immutable <see cref="SpellCastPlan"/>. The book page, the
    /// save path and the runtime all call it, so arranging, saving and casting can never accept
    /// different chains.
    ///
    /// Documented rules (Experiments/spellbook.md §8):
    /// the chain starts with a Form; Effects follow inside the segment; an Augment binds to the
    /// nearest non-Augment on its left and only when that rune lists it in
    /// <see cref="SpellGlyphDef.compatibleAugments"/>; a Trigger rune ends a segment and must be
    /// followed by a Form; the last segment needs at least one Effect.
    /// </summary>
    public static class SpellCompiler
    {
        // Documented fallbacks, only used when a hand-written Def omits a number. The shipped
        // Spellbook.xml always states them explicitly.
        private const float DefaultHarmAmount = 9f;
        private const float DefaultHealAmount = 12f;
        private const float DefaultExplosionAmount = 12f;
        private const float DefaultAmplifyFactor = 1.5f;
        private const float DefaultDampenFactor = 0.6f;
        private const float DefaultAoeRadiusBonus = 1.2f;
        private const float DefaultExtendFactor = 2f;
        private const float DefaultShortenFactor = 0.5f;
        private const float DefaultBreakArmorBonus = 0.35f;
        private const float DefaultAccelerateFactor = 1.8f;
        private const float DefaultDecelerateFactor = 0.55f;
        private const int DefaultSplitMissiles = 2;

        /// <summary>
        /// Whether one more rune may be appended to a prefix that is still being arranged.
        ///
        /// This is the same rule set <see cref="TryValidate"/> applies rune by rune, but it accepts a
        /// prefix that is not finished yet (a trailing Trigger, a segment without an Effect), so the
        /// book can grey out a rune the moment it would make the chain unfinishable instead of only
        /// complaining after the fact. A null/unknown glyph in the prefix is refused with the same
        /// text a save would show.
        /// </summary>
        public static bool CanAppend(IReadOnlyList<string> glyphs, SpellGlyphDef glyph, out string reason)
        {
            reason = null;
            if (glyph == null)
            {
                reason = SpellText.TF("WotF_Spells.Reason.UnknownGlyph", "?");
                return false;
            }

            int count = glyphs?.Count ?? 0;
            SpellTuningDef tuning = SpellDefs.Tuning;
            if (count >= tuning.maxGlyphs)
            {
                reason = SpellText.TF("WotF_Spells.Reason.TooLong", tuning.maxGlyphs);
                return false;
            }

            var defs = new List<SpellGlyphDef>(count + 1);
            for (int i = 0; i < count; i++)
            {
                SpellGlyphDef existing = SpellDefs.Glyph(glyphs[i]);
                if (existing == null)
                {
                    reason = SpellText.TF("WotF_Spells.Reason.UnknownGlyph",
                        glyphs[i].NullOrEmpty() ? "?" : glyphs[i]);
                    return false;
                }

                defs.Add(existing);
            }

            if (defs.Count == 0)
            {
                // An empty bar only accepts the rune that opens a segment.
                if (glyph.kind == SpellGlyphKind.Form)
                {
                    return true;
                }

                reason = SpellText.T(glyph.kind == SpellGlyphKind.Trigger
                    ? "WotF_Spells.Reason.TriggerNotFirst"
                    : "WotF_Spells.Reason.FormMustBeFirst");
                return false;
            }

            SpellGlyphDef last = defs[defs.Count - 1];
            if (last.kind == SpellGlyphKind.Trigger)
            {
                // A trigger must be followed immediately by the form of the next segment.
                if (glyph.kind == SpellGlyphKind.Form)
                {
                    // A composite form is a whole spell. Joining it to an earlier segment would mean
                    // "cast the railgun where the dart hit", which this round does not support.
                    if (glyph.IsCompleteForm)
                    {
                        reason = SpellText.T("WotF_Spells.Reason.CompleteFormNotAfterTrigger");
                        return false;
                    }

                    return true;
                }

                reason = SpellText.T("WotF_Spells.Reason.TriggerNeedsForm");
                return false;
            }

            switch (glyph.kind)
            {
                case SpellGlyphKind.Form:
                    // A segment has exactly one form, and only a trigger may open the next one.
                    reason = SpellText.T("WotF_Spells.Reason.FormOnlyAfterTrigger");
                    return false;

                case SpellGlyphKind.Trigger:
                    // Only a projectile can be interrupted by a real hit: self and touch resolve at
                    // once, so "on hit" would never happen for them.
                    SpellGlyphDef segmentForm = LastForm(defs);
                    if (segmentForm != null && segmentForm.defName == SpellDefs.FormProjectile)
                    {
                        return true;
                    }

                    reason = SpellText.T(segmentForm == null
                        ? "WotF_Spells.Reason.FormMustBeFirst"
                        : "WotF_Spells.Reason.TriggerOnlyAfterProjectile");
                    return false;

                case SpellGlyphKind.Effect:
                    // Effects stay inside the current segment, after its form. A composite form
                    // already carries its own effect, so nothing may be appended to it.
                    SpellGlyphDef effectAnchor = LastForm(defs);
                    if (effectAnchor != null && effectAnchor.IsCompleteForm)
                    {
                        reason = SpellText.T("WotF_Spells.Reason.CompleteFormNoEffect");
                        return false;
                    }

                    return true;

                case SpellGlyphKind.Augment:
                    // Compatibility is data: the nearest non-augment rune on the left decides.
                    SpellGlyphDef anchor = LastNonAugment(defs);
                    if (anchor != null && anchor.AcceptsAugment(glyph.defName))
                    {
                        return true;
                    }

                    reason = SpellText.T("WotF_Spells.Reason.AugmentNotAccepted");
                    return false;

                default:
                    reason = SpellText.T("WotF_Spells.Reason.Unknown");
                    return false;
            }
        }

        /// <summary>Syntax only: no pawn, no map, no cooldown. Reasons are player-facing text.</summary>
        public static bool TryValidate(SpellRecipe recipe, out string reason)
        {
            reason = null;
            SpellTuningDef tuning = SpellDefs.Tuning;

            if (!TryResolve(recipe, tuning, out List<SpellGlyphDef> defs, out reason))
            {
                return false;
            }

            // One rule set: validating a finished chain is the same check the book runs while
            // arranging it, applied to every prefix in order.
            var prefix = new List<string>(defs.Count);
            for (int i = 0; i < defs.Count; i++)
            {
                if (!CanAppend(prefix, defs[i], out reason))
                {
                    return false;
                }

                prefix.Add(defs[i].defName);
            }

            // Completeness, which a prefix check deliberately allows: a trigger needs its form.
            if (defs[defs.Count - 1].kind == SpellGlyphKind.Trigger)
            {
                reason = SpellText.T("WotF_Spells.Reason.TriggerNotLast");
                return false;
            }

            // A timing rune with nothing to its right would place an empty field/seal/timer. The last
            // effect of the chain is the one that has to hold a payload; an augment after the rune
            // still leaves that payload empty.
            SpellGlyphDef lastEffect = null;
            for (int i = defs.Count - 1; i >= 0; i--)
            {
                if (defs[i].kind == SpellGlyphKind.Effect)
                {
                    lastEffect = defs[i];
                    break;
                }
            }

            if (lastEffect != null && IsTimingEffect(EffectOf(lastEffect)))
            {
                reason = SpellText.T("WotF_Spells.Reason.TimingNeedsEffect");
                return false;
            }

            // A middle segment may be empty only because a trigger rune ends it; the last one has to
            // do something, otherwise the chain ends with a payload nobody ever sees.
            int lastFormIndex = LastFormIndex(defs);
            int effectsInLastSegment = 0;
            for (int i = lastFormIndex + 1; i < defs.Count; i++)
            {
                if (defs[i].kind == SpellGlyphKind.Effect)
                {
                    effectsInLastSegment++;
                }
            }

            if (effectsInLastSegment == 0 && !IsCompleteFormIndex(defs, lastFormIndex))
            {
                reason = SpellText.T(CountForms(defs) > 1
                    ? "WotF_Spells.Reason.LastSegmentNeedsEffect"
                    : "WotF_Spells.Reason.NoEffect");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates and compiles in one step. Returns null (with a player-facing reason) when the
        /// chain is not castable; the returned plan is a fresh deep copy that the caller may keep.
        /// </summary>
        public static SpellCastPlan Compile(SpellRecipe recipe, out string reason)
        {
            reason = null;
            if (!TryValidate(recipe, out reason))
            {
                return null;
            }

            SpellTuningDef tuning = SpellDefs.Tuning;
            if (!TryResolve(recipe, tuning, out List<SpellGlyphDef> defs, out reason))
            {
                return null;
            }

            var plan = new SpellCastPlan
            {
                recipeName = recipe.name,
                glyphs = new List<string>(recipe.glyphs),
                // The book froze the phases when the spell was created; an in-memory recipe (lab, test
                // or tooling) is the one case without a frozen value, so the chain supplies it once.
                timing = (recipe.timing ?? SpellTiming.Derive(recipe.glyphs)).Copy()
            };

            // Every trigger rune closes the segment before it; the rest is the final segment.
            int start = 0;
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i].kind != SpellGlyphKind.Trigger)
                {
                    continue;
                }

                plan.segments.Add(CompileSegment(defs, start, i, tuning, true));
                start = i + 1;
            }

            plan.segments.Add(CompileSegment(defs, start, defs.Count, tuning, false));

            // The legacy fields become the first segment, so every pre-trigger caller keeps working.
            plan.Normalize();
            return plan;
        }

        /// <summary>
        /// Compiles the runes of one segment (the form at <paramref name="start"/> and everything up
        /// to, but not including, the trigger that ends it). Augments only ever look inside this
        /// range, so nothing can bind across a trigger rune.
        ///
        /// A Linger/Rune/Delay rune closes this segment's list of steps and turns every rune to its
        /// right into that step's payload. The payload is compiled by the same method one level down,
        /// so a delayed Harm is the same step object as an immediate Harm, only deferred.
        /// </summary>
        private static SpellSegment CompileSegment(
            List<SpellGlyphDef> defs,
            int start,
            int end,
            SpellTuningDef tuning,
            bool triggerOnHit)
        {
            var segment = new SpellSegment
            {
                form = FormOf(defs[start]),
                missileCount = tuning.missileCount,
                missileSpeed = tuning.missileSpeed,
                triggerOnHit = triggerOnHit
            };

            // A composite form is the whole segment: its numbers are frozen here, and the grammar has
            // already refused any effect or augment after it.
            if (defs[start].IsCompleteForm)
            {
                segment.railgun = defs[start].railgun.Copy();
                return segment;
            }

            if (segment.form == SpellFormKind.Orbit)
            {
                SpellGlyphDef form = defs[start];
                segment.orbitDistance = form.radius;
                segment.orbitSpeedDegrees = form.angularSpeedDegrees;
                segment.orbitHitRadius = form.width;
                segment.orbitLifetimeTicks = form.durationTicks;
            }

            float speedFactor = 1f;

            // Augments immediately after the form bind to the form: that run is what changes the
            // volley, the orbit or the pierce count. Everything from the first Effect on is compiled
            // with its own run of augments.
            int i = start + 1;
            while (i < end && defs[i].kind == SpellGlyphKind.Augment)
            {
                ApplyFormAugment(segment, defs[i], tuning, ref speedFactor);
                i++;
            }

            BuildSteps(defs, i, end, tuning, segment.steps);

            segment.missileSpeed = Clamp(tuning.missileSpeed * speedFactor, 2f, 60f);
            segment.missileCount = System.Math.Max(1, segment.missileCount);
            return segment;
        }

        /// <summary>One augment bound to the segment's Form (not to an effect).</summary>
        private static void ApplyFormAugment(
            SpellSegment segment,
            SpellGlyphDef glyph,
            SpellTuningDef tuning,
            ref float speedFactor)
        {
            float factor = glyph.factor > 0f ? glyph.factor : 1f;
            switch (glyph.defName)
            {
                case SpellDefs.AugmentAccelerate:
                    speedFactor *= glyph.factor > 0f ? glyph.factor : DefaultAccelerateFactor;
                    if (segment.form == SpellFormKind.Orbit)
                    {
                        segment.orbitSpeedDegrees *= factor;
                    }

                    break;

                case SpellDefs.AugmentDecelerate:
                    speedFactor *= glyph.factor > 0f ? glyph.factor : DefaultDecelerateFactor;
                    if (segment.form == SpellFormKind.Orbit)
                    {
                        segment.orbitSpeedDegrees *= factor;
                    }

                    break;

                case SpellDefs.AugmentSplit:
                    segment.missileCount += glyph.missiles > 0 ? glyph.missiles : DefaultSplitMissiles;
                    segment.missileCount = System.Math.Min(segment.missileCount, tuning.maxMissiles);
                    break;

                case SpellDefs.AugmentAoe:
                    if (segment.form == SpellFormKind.Orbit)
                    {
                        segment.orbitHitRadius = System.Math.Min(
                            segment.orbitHitRadius + (glyph.radius > 0f ? glyph.radius : DefaultAoeRadiusBonus),
                            tuning.radiusCap);
                    }

                    break;

                case SpellDefs.AugmentExtend:
                    if (segment.form == SpellFormKind.Orbit)
                    {
                        segment.orbitLifetimeTicks = ScaleTicks(segment.orbitLifetimeTicks,
                            glyph.factor > 0f ? glyph.factor : DefaultExtendFactor);
                    }

                    break;

                case SpellDefs.AugmentShorten:
                    if (segment.form == SpellFormKind.Orbit)
                    {
                        segment.orbitLifetimeTicks = ScaleTicks(segment.orbitLifetimeTicks,
                            glyph.factor > 0f ? glyph.factor : DefaultShortenFactor);
                    }

                    break;

                case SpellDefs.AugmentPierce:
                    segment.pierceStacks++;
                    break;
            }
        }

        /// <summary>
        /// Compiles a run of Effect runes (and the augments bound to each) into <paramref name="into"/>.
        /// A timing rune takes everything after it as its payload and ends the run: that is the
        /// "change when and where" rule from the spell table, expressed with the same step type.
        /// </summary>
        private static void BuildSteps(
            List<SpellGlyphDef> defs,
            int start,
            int end,
            SpellTuningDef tuning,
            List<SpellPlanStep> into)
        {
            int i = start;
            while (i < end)
            {
                SpellGlyphDef glyph = defs[i];
                if (glyph.kind != SpellGlyphKind.Effect)
                {
                    i++;
                    continue;
                }

                SpellPlanStep step = BuildStep(glyph, tuning);
                int j = i + 1;
                while (j < end && defs[j].kind == SpellGlyphKind.Augment)
                {
                    // Augments bind to the nearest Form/Effect on their left, so exactly the run of
                    // augments before the next Form/Effect belongs to this step.
                    ApplyEffectAugment(step, defs[j], tuning);
                    j++;
                }

                into.Add(step);

                if (IsTimingEffect(step.effect))
                {
                    step.payload = new SpellPayload();
                    BuildSteps(defs, j, end, tuning, step.payload.steps);
                    return;
                }

                i = j;
            }
        }

        private static SpellPlanStep BuildStep(SpellGlyphDef glyph, SpellTuningDef tuning)
        {
            SpellEffectKind effect = EffectOf(glyph);
            var step = new SpellPlanStep
            {
                effect = effect,
                amount = BaseAmount(glyph),
                baseDurationTicks = glyph.durationTicks,
                pulseIntervalTicks = glyph.pulseIntervalTicks,
                factor = glyph.factor,
                vulnerableFactor = glyph.vulnerableFactor,
                empAmount = glyph.empAmount,
                perStackFactor = glyph.perStackFactor,
                fireSize = glyph.fireSize,
                burnAmount = glyph.burnAmount,
                glowRadius = glyph.glowRadius,
                glowColor = glyph.glowColor
            };

            if (effect == SpellEffectKind.Explosion)
            {
                step.radius = glyph.radius > 0f ? glyph.radius : tuning.aoeBaseRadius;
            }

            if (HasOwnDuration(effect))
            {
                step.durationTicks = glyph.durationTicks;
            }

            return step;
        }

        /// <summary>One augment bound to an Effect step.</summary>
        private static void ApplyEffectAugment(SpellPlanStep step, SpellGlyphDef glyph, SpellTuningDef tuning)
        {
            switch (glyph.defName)
            {
                case SpellDefs.AugmentAmplify:
                {
                    step.amplifyStacks++;
                    float factor = glyph.factor > 0f ? glyph.factor : DefaultAmplifyFactor;
                    step.magnitudeFactor *= factor;
                    ScaleAmount(step, factor, tuning);
                    if (step.effect == SpellEffectKind.Explosion && glyph.radius > 0f)
                    {
                        // Amplify also widens an explosion a little; AOE is the pure radius augment.
                        step.radius = (step.radius > 0f ? step.radius : tuning.aoeBaseRadius) + glyph.radius;
                    }

                    break;
                }

                case SpellDefs.AugmentDampen:
                {
                    step.dampenStacks++;
                    float factor = glyph.factor > 0f ? glyph.factor : DefaultDampenFactor;
                    step.magnitudeFactor *= factor;
                    ScaleAmount(step, factor, tuning);
                    break;
                }

                case SpellDefs.AugmentAoe:
                    step.aoeStacks++;
                    step.aoe = true;
                    step.radius = (step.radius > 0f ? step.radius : tuning.aoeBaseRadius)
                                  + (glyph.radius > 0f ? glyph.radius : DefaultAoeRadiusBonus);
                    step.radius = System.Math.Min(step.radius, tuning.radiusCap);
                    break;

                case SpellDefs.AugmentExtend:
                    step.durationTicks = ScaledDuration(step, glyph.factor > 0f ? glyph.factor : DefaultExtendFactor);
                    if (step.effect == SpellEffectKind.Harm)
                    {
                        // Harm + Extend is the spell table's sustained wound, not a bigger hit.
                        step.dot = step.durationTicks > 0;
                    }

                    break;

                case SpellDefs.AugmentShorten:
                    if (step.durationTicks > 0)
                    {
                        step.durationTicks = ScaleTicks(step.durationTicks,
                            glyph.factor > 0f ? glyph.factor : DefaultShortenFactor);
                    }

                    break;

                case SpellDefs.AugmentSensitive:
                    step.sensitive = true;
                    break;

                case SpellDefs.AugmentBreakArmor:
                    step.penetration += glyph.factor > 0f ? glyph.factor : DefaultBreakArmorBonus;
                    break;

                case SpellDefs.AugmentAccelerate:
                case SpellDefs.AugmentDecelerate:
                    if (step.effect == SpellEffectKind.Linger && step.pulseIntervalTicks > 0)
                    {
                        // Accelerate divides the beat (faster pulses); Decelerate's factor is below
                        // one, so dividing by it lengthens the interval as the spell table says.
                        float factor = glyph.factor > 0f ? glyph.factor : 1f;
                        step.pulseIntervalTicks = ScaleTicks(step.pulseIntervalTicks, 1f / factor);
                    }

                    break;
            }
        }

        private static void ScaleAmount(SpellPlanStep step, float factor, SpellTuningDef tuning)
        {
            if (step.amount > 0f)
            {
                step.amount = System.Math.Min(step.amount * factor, tuning.amountCap);
            }
        }

        private static int ScaledDuration(SpellPlanStep step, float factor)
        {
            int basis = step.durationTicks > 0 ? step.durationTicks : step.baseDurationTicks;
            return basis > 0 ? ScaleTicks(basis, factor) : 0;
        }

        private static int ScaleTicks(int ticks, float factor)
        {
            if (ticks <= 0)
            {
                return 0;
            }

            return System.Math.Max(1, (int)System.Math.Round(ticks * factor));
        }

        /// <summary>True when this step holds the rest of the chain instead of acting at once.</summary>
        public static bool IsTimingEffect(SpellEffectKind effect)
        {
            return effect == SpellEffectKind.Linger
                   || effect == SpellEffectKind.Rune
                   || effect == SpellEffectKind.Delay;
        }

        /// <summary>Effects that carry a lifetime of their own even without Extend.</summary>
        private static bool HasOwnDuration(SpellEffectKind effect)
        {
            switch (effect)
            {
                case SpellEffectKind.Heal:
                case SpellEffectKind.Grow:
                case SpellEffectKind.Ignite:
                case SpellEffectKind.Freeze:
                case SpellEffectKind.Snare:
                case SpellEffectKind.Hex:
                case SpellEffectKind.Magelight:
                case SpellEffectKind.Linger:
                case SpellEffectKind.Rune:
                case SpellEffectKind.Delay:
                    return true;
                default:
                    return false;
            }
        }


        /// <summary>Form glyph to behaviour. Only the documented runes are forms.</summary>
        public static SpellFormKind FormOf(SpellGlyphDef formGlyph)
        {
            if (formGlyph == null)
            {
                return SpellFormKind.Projectile;
            }

            if (formGlyph.IsCompleteForm)
            {
                return SpellFormKind.Railgun;
            }

            if (formGlyph.defName == SpellDefs.FormSelf)
            {
                return SpellFormKind.Self;
            }

            if (formGlyph.defName == SpellDefs.FormTouch)
            {
                return SpellFormKind.Touch;
            }

            if (formGlyph.defName == SpellDefs.FormOrbit)
            {
                return SpellFormKind.Orbit;
            }

            return SpellFormKind.Projectile;
        }

        public static SpellEffectKind EffectOf(SpellGlyphDef effectGlyph)
        {
            switch (effectGlyph.defName)
            {
                case SpellDefs.EffectHeal:
                    return SpellEffectKind.Heal;
                case SpellDefs.EffectExplosion:
                    return SpellEffectKind.Explosion;
                case SpellDefs.EffectGrow:
                    return SpellEffectKind.Grow;
                case SpellDefs.EffectIgnite:
                    return SpellEffectKind.Ignite;
                case SpellDefs.EffectLightning:
                    return SpellEffectKind.Lightning;
                case SpellDefs.EffectFreeze:
                    return SpellEffectKind.Freeze;
                case SpellDefs.EffectColdSnap:
                    return SpellEffectKind.ColdSnap;
                case SpellDefs.EffectKnockback:
                    return SpellEffectKind.Knockback;
                case SpellDefs.EffectPull:
                    return SpellEffectKind.Pull;
                case SpellDefs.EffectSnare:
                    return SpellEffectKind.Snare;
                case SpellDefs.EffectHex:
                    return SpellEffectKind.Hex;
                case SpellDefs.EffectMagelight:
                    return SpellEffectKind.Magelight;
                case SpellDefs.EffectBlink:
                    return SpellEffectKind.Blink;
                case SpellDefs.EffectLinger:
                    return SpellEffectKind.Linger;
                case SpellDefs.EffectRune:
                    return SpellEffectKind.Rune;
                case SpellDefs.EffectDelay:
                    return SpellEffectKind.Delay;
                default:
                    return SpellEffectKind.Harm;
            }
        }

        private static bool TryResolve(
            SpellRecipe recipe,
            SpellTuningDef tuning,
            out List<SpellGlyphDef> defs,
            out string reason)
        {
            defs = new List<SpellGlyphDef>();
            reason = null;

            if (recipe == null || recipe.glyphs == null || recipe.glyphs.Count == 0)
            {
                reason = SpellText.T("WotF_Spells.Reason.NoGlyphs");
                return false;
            }

            if (recipe.glyphs.Count > tuning.maxGlyphs)
            {
                reason = SpellText.TF("WotF_Spells.Reason.TooLong", tuning.maxGlyphs);
                return false;
            }

            for (int i = 0; i < recipe.glyphs.Count; i++)
            {
                SpellGlyphDef glyph = SpellDefs.Glyph(recipe.glyphs[i]);
                if (glyph == null)
                {
                    reason = SpellText.TF("WotF_Spells.Reason.UnknownGlyph",
                        recipe.glyphs[i].NullOrEmpty() ? "?" : recipe.glyphs[i]);
                    return false;
                }

                defs.Add(glyph);
            }

            return true;
        }

        /// <summary>Nearest rune on the left that is not an Augment; null when there is none.</summary>
        private static SpellGlyphDef LastNonAugment(List<SpellGlyphDef> defs)
        {
            for (int i = defs.Count - 1; i >= 0; i--)
            {
                if (defs[i].kind != SpellGlyphKind.Augment)
                {
                    return defs[i];
                }
            }

            return null;
        }

        private static SpellGlyphDef LastForm(List<SpellGlyphDef> defs)
        {
            int index = LastFormIndex(defs);
            return index >= 0 ? defs[index] : null;
        }

        private static int LastFormIndex(List<SpellGlyphDef> defs)
        {
            for (int i = defs.Count - 1; i >= 0; i--)
            {
                if (defs[i].kind == SpellGlyphKind.Form)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int CountForms(List<SpellGlyphDef> defs)
        {
            int count = 0;
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i].kind == SpellGlyphKind.Form)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>True when the form at that index is a composite form (a whole spell by itself).</summary>
        private static bool IsCompleteFormIndex(List<SpellGlyphDef> defs, int index)
        {
            return index >= 0 && index < defs.Count && defs[index].IsCompleteForm;
        }

        private static float BaseAmount(SpellGlyphDef glyph)
        {
            if (glyph.amount > 0f)
            {
                return glyph.amount;
            }

            // Only the four original effects keep a documented fallback; every rune added with the
            // full spell table states its magnitude in XML and reports a config error otherwise.
            switch (EffectOf(glyph))
            {
                case SpellEffectKind.Heal:
                    return DefaultHealAmount;
                case SpellEffectKind.Explosion:
                    return DefaultExplosionAmount;
                case SpellEffectKind.Grow:
                    return -1f;
                case SpellEffectKind.Harm:
                    return DefaultHarmAmount;
                default:
                    return -1f;
            }
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
