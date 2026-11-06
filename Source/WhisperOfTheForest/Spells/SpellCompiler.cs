using System.Collections.Generic;
using Verse;
using WhisperOfTheForest.Missiles;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// The single grammar of the spellbook: syntax validation, incremental "can I append this rune?"
    /// and compilation of a recipe into an immutable <see cref="SpellCastPlan"/>. The book page, the
    /// save path and the runtime all call it, so arranging, saving and casting can never accept
    /// different chains.
    ///
    /// Rules:
    /// the chain starts with its only Form; Effects follow; an Augment binds to the nearest
    /// non-Augment on its left and only when that rune lists it in
    /// <see cref="SpellGlyphDef.compatibleAugments"/>, and two augments on the same rune may not
    /// exclude each other (<see cref="SpellGlyphDef.excludesAugments"/>); the chain needs at least one Effect unless
    /// its form is a whole spell on its own.
    /// </summary>
    public static class SpellCompiler
    {
        /// <summary>
        /// Whether one more rune may be appended to a prefix that is still being arranged.
        ///
        /// This is the same rule set <see cref="TryValidate"/> applies rune by rune, but it accepts a
        /// prefix that is not finished yet (a form without an Effect), so the
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

            if (!GameComponent_Spellbook.IsUnlocked(glyph))
            {
                reason = SpellText.TF("WotF_Spells.Reason.GlyphLocked", glyph.LabelCap);
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
                // An empty bar only accepts the form.
                if (glyph.kind == SpellGlyphKind.Form)
                {
                    return true;
                }

                reason = SpellText.T("WotF_Spells.Reason.FormMustBeFirst");
                return false;
            }

            switch (glyph.kind)
            {
                case SpellGlyphKind.Form:
                    reason = SpellText.T("WotF_Spells.Reason.OneForm");
                    return false;

                case SpellGlyphKind.Effect:
                    // A composite form already carries its own effect, so nothing may be appended to it.
                    if (defs[0].IsCompleteForm)
                    {
                        reason = SpellText.T("WotF_Spells.Reason.CompleteFormNoEffect");
                        return false;
                    }

                    return true;

                case SpellGlyphKind.Augment:
                    // Compatibility is data: the nearest non-augment rune on the left decides.
                    SpellGlyphDef anchor = LastNonAugment(defs);
                    if (anchor == null || !anchor.AcceptsAugment(glyph.defName))
                    {
                        reason = SpellText.T("WotF_Spells.Reason.AugmentNotAccepted");
                        return false;
                    }

                    // The augments already bound to the same rune are the run right before this one;
                    // either side declaring the other as exclusive refuses the pair.
                    for (int i = defs.Count - 1; i >= 0 && defs[i].kind == SpellGlyphKind.Augment; i--)
                    {
                        if (glyph.ExcludesAugment(defs[i].defName) || defs[i].ExcludesAugment(glyph.defName))
                        {
                            reason = SpellText.TF("WotF_Spells.Reason.AugmentExcluded", glyph.LabelCap, defs[i].LabelCap);
                            return false;
                        }
                    }

                    return true;

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

            if (lastEffect == null && !defs[0].IsCompleteForm)
            {
                reason = SpellText.T("WotF_Spells.Reason.NoEffect");
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
                timing = (recipe.timing ?? SpellTiming.Derive(recipe.glyphs)).Copy(),
                cost = (recipe.cost ?? SpellCost.Derive(recipe.glyphs)).Copy()
            };

            CompileBody(plan, defs, tuning);
            return plan;
        }

        /// <summary>
        /// Compiles the chain: the form's own numbers are copied from its Def and changed by the run
        /// of augments right after it, then the effects follow.
        ///
        /// A Linger/Rune/Delay rune closes the list of steps and turns every rune to its right into
        /// that step's payload. The payload is compiled by the same method one level down, so a delayed
        /// Harm is the same step object as an immediate Harm, only deferred.
        /// </summary>
        private static void CompileBody(SpellCastPlan plan, List<SpellGlyphDef> defs, SpellTuningDef tuning)
        {
            SpellGlyphDef form = defs[0];
            plan.form = FormOf(form);
            plan.range = form.range;
            plan.projectile = form.projectile?.Copy();
            plan.orbit = form.orbit?.Copy();

            // A composite form is the whole spell: its numbers are frozen here, and the grammar has
            // already refused any effect or augment after it.
            if (form.IsCompleteForm)
            {
                plan.railgun = form.railgun.Copy();
                return;
            }

            // Augments immediately after the form bind to the form: that run is what changes the
            // volley, the orbit or the pierce count. Everything from the first Effect on is compiled
            // with its own run of augments.
            int i = 1;
            while (i < defs.Count && defs[i].kind == SpellGlyphKind.Augment)
            {
                ApplyFormAugment(plan, defs[i], tuning);
                i++;
            }

            BuildSteps(defs, i, defs.Count, plan.steps);
        }

        /// <summary>
        /// One augment bound to the Form. Which augments a form accepts is its Def's list; this only
        /// says what each one changes in the form's own block.
        /// </summary>
        private static void ApplyFormAugment(SpellCastPlan plan, SpellGlyphDef glyph, SpellTuningDef tuning)
        {
            ArcaneMissileSettings projectile = plan.projectile;
            SpellOrbitSpec orbit = plan.orbit;
            switch (glyph.defName)
            {
                case SpellDefs.AugmentAccelerate:
                case SpellDefs.AugmentDecelerate:
                    if (projectile != null)
                    {
                        projectile.speedCellsPerSecond *= glyph.factor;
                    }

                    if (orbit != null)
                    {
                        orbit.angularSpeedDegrees *= glyph.factor;
                    }

                    break;

                case SpellDefs.AugmentSplit:
                    if (projectile != null)
                    {
                        projectile.missileCount = System.Math.Min(projectile.missileCount + glyph.missiles, tuning.maxMissiles);
                    }

                    if (orbit != null)
                    {
                        orbit.count = System.Math.Min(orbit.count + glyph.missiles, tuning.maxMissiles);
                    }

                    break;

                case SpellDefs.AugmentAoe:
                    if (orbit != null)
                    {
                        orbit.hitRadius += glyph.radius;
                    }

                    break;

                case SpellDefs.AugmentExtend:
                case SpellDefs.AugmentShorten:
                    if (orbit != null)
                    {
                        orbit.lifetimeTicks = ScaleTicks(orbit.lifetimeTicks, glyph.factor);
                    }

                    break;

                case SpellDefs.AugmentPierce:
                    plan.pierceStacks++;
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

                SpellPlanStep step = BuildStep(glyph);
                int j = i + 1;
                while (j < end && defs[j].kind == SpellGlyphKind.Augment)
                {
                    // Augments bind to the nearest Form/Effect on their left, so exactly the run of
                    // augments before the next Form/Effect belongs to this step.
                    ApplyEffectAugment(step, defs[j]);
                    j++;
                }

                into.Add(step);

                if (IsTimingEffect(step.effect))
                {
                    step.payload = new SpellPayload();
                    BuildSteps(defs, j, end, step.payload.steps);
                    return;
                }

                i = j;
            }
        }

        private static SpellPlanStep BuildStep(SpellGlyphDef glyph)
        {
            SpellEffectKind effect = EffectOf(glyph);
            var step = new SpellPlanStep
            {
                effect = effect,
                amount = BaseAmount(glyph),
                radius = glyph.radius,
                penetration = glyph.armorPenetration,
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

            if (HasOwnDuration(effect))
            {
                step.durationTicks = glyph.durationTicks;
            }

            return step;
        }

        /// <summary>One augment bound to an Effect step.</summary>
        private static void ApplyEffectAugment(SpellPlanStep step, SpellGlyphDef glyph)
        {
            switch (glyph.defName)
            {
                case SpellDefs.AugmentAmplify:
                {
                    step.amplifyStacks++;
                    step.magnitudeFactor *= glyph.factor;
                    ScaleAmount(step, glyph.factor);
                    if (step.effect == SpellEffectKind.Explosion)
                    {
                        // Amplify also widens an explosion a little; AOE is the pure radius augment.
                        step.radius += glyph.radius;
                    }

                    break;
                }

                case SpellDefs.AugmentDampen:
                {
                    step.dampenStacks++;
                    step.magnitudeFactor *= glyph.factor;
                    ScaleAmount(step, glyph.factor);
                    break;
                }

                case SpellDefs.AugmentAoe:
                    step.aoeStacks++;
                    step.aoe = true;
                    step.radius += glyph.radius;
                    break;

                case SpellDefs.AugmentExtend:
                    step.durationTicks = ScaledDuration(step, glyph.factor);
                    if (step.effect == SpellEffectKind.Harm)
                    {
                        // Harm + Extend is the spell table's sustained wound, not a bigger hit.
                        step.dot = step.durationTicks > 0;
                    }

                    break;

                case SpellDefs.AugmentShorten:
                    if (step.durationTicks > 0)
                    {
                        step.durationTicks = ScaleTicks(step.durationTicks, glyph.factor);
                    }

                    break;

                case SpellDefs.AugmentSensitive:
                    step.sensitive = true;
                    break;

                case SpellDefs.AugmentBreakArmor:
                    step.penetration += glyph.factor;
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

        private static void ScaleAmount(SpellPlanStep step, float factor)
        {
            if (step.amount > 0f)
            {
                step.amount *= factor;
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


        /// <summary>
        /// Form glyph to behaviour. A data block decides (railgun, projectile, orbit), so a new
        /// projectile type is only a new Def; self and touch are named. SpellGlyphDef.ConfigErrors
        /// guarantees every form is exactly one of these.
        /// </summary>
        public static SpellFormKind FormOf(SpellGlyphDef formGlyph)
        {
            if (formGlyph.IsCompleteForm)
            {
                return SpellFormKind.Railgun;
            }

            if (formGlyph.projectile != null)
            {
                return SpellFormKind.Projectile;
            }

            if (formGlyph.orbit != null)
            {
                return SpellFormKind.Orbit;
            }

            return formGlyph.defName == SpellDefs.FormTouch ? SpellFormKind.Touch : SpellFormKind.Self;
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

            // Every cast, the staff's range and the book's save all compile through here.
            if (tuning == null)
            {
                reason = SpellText.T("WotF_Spells.Reason.TuningMissing");
                return false;
            }

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

        private static float BaseAmount(SpellGlyphDef glyph)
        {
            // SpellGlyphDef.ConfigErrors guarantees a positive amount whenever one is required.
            return glyph.requiresAmount ? glyph.amount : -1f;
        }
    }
}
