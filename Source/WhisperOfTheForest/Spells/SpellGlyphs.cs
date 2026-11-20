using System.Collections.Generic;
using Verse;
using WhisperOfTheForest.Missiles;
using WhisperOfTheForest.Vfx;

namespace WhisperOfTheForest.Spells
{
    /// <summary>The rune categories the book shows and the grammar understands.</summary>
    public enum SpellGlyphKind
    {
        Form,
        Effect,
        Augment,
        Super
    }

    /// <summary>
    /// One catalogue entry. The Def only names the kind and the bounded numbers; every behaviour is
    /// implemented in code (<see cref="SpellCompiler"/>, <see cref="SpellExecutor"/>), so a new XML
    /// entry can tune a spell but cannot invent behaviour.
    /// </summary>
    public class SpellGlyphDef : Def
    {
        public SpellGlyphKind kind;

        /// <summary>
        /// Ordinary rune awakening tier: 0 is initially known, 1..3 requires that altar tier.
        /// Super runes have no altar tier; their requiresUnlock is fulfilled by rubbings.
        /// </summary>
        public int tier;

        /// <summary>Texture path under Textures/. Empty falls back to the contract convention.</summary>
        public string iconPath;

        /// <summary>
        /// What this rune's effect may be aimed at, in the spell system's own categories. Required
        /// for every Effect and Super rune (which is a whole spell on its own); ordinary
        /// forms take their categories from the effects they carry. The book freezes the union into
        /// the recipe when the spell is created, so editing this Def cannot change a saved spell.
        /// </summary>
        public List<SpellTargetClass> targets;

        /// <summary>
        /// Effect: working radius - the blast of an explosion, the field of a linger, and the area an
        /// AOE'd effect covers before its AOE stacks. Augment AOE: radius added per stack.
        /// </summary>
        public float radius;

        /// <summary>Augment Amplify: multiplier per stack. Accelerate/Decelerate: speed multiplier.</summary>
        public float factor;

        /// <summary>Augment Split: extra missiles per stack.</summary>
        public int missiles;

        /// <summary>
        /// Augments this glyph accepts, by defName, exactly as Ars Nouveau 5.13.3 declares them per
        /// glyph. The compiler only reads this list - there is no second copy of the rules in code -
        /// so a new glyph or a rebalanced pairing is an XML edit, not a rebuild.
        ///
        /// Null or empty means "accepts no augment at all" (self, touch, trigger and the augments
        /// themselves): appending any augment after such a glyph is refused.
        /// </summary>
        public List<string> compatibleAugments;

        /// <summary>
        /// Augments only: other augments that may not bind to the same rune as this one, by defName.
        /// One side listing the other is enough; the compiler checks both directions.
        /// </summary>
        public List<string> excludesAugments;

        /// <summary>
        /// Railgun payload of a Super rune: it needs no Effect and accepts none. These numbers are
        /// what it casts. Null for every other rune.
        /// </summary>
        public SpellRailgunSpec railgun;
        public SpellTornadoSpec tornado;

        /// <summary>
        /// Projectile and Touch forms: how far the caster may aim (projectile) or reach (touch), in
        /// cells from the caster. Self and Orbit act around the caster and read no range.
        /// </summary>
        public float range;

        /// <summary>
        /// Projectile form: the dart's flight envelope, hit strategy and look. A Form carrying this is a
        /// projectile; another projectile type is another form Def with its own block, not new code.
        /// The payload numbers in it (damage, heal, radius) are unused: the arranged effects decide.
        /// </summary>
        public ArcaneMissileSettings projectile;

        /// <summary>Orbit form: the orbs' count, distance, turn rate, touch radius and lifetime.</summary>
        public SpellOrbitSpec orbit;

        /// <summary>Data-driven cast visual and sound theme for Effect and Super glyphs.</summary>
        public SpellThemeDef theme;

        /// <summary>Combat-log flavor rules owned by each damaging Effect or Super glyph.</summary>
        public RulePackDef combatLogRules;

        /// <summary>
        /// Ticks this rune adds to a spell's three phases. They are summed along the whole chain when
        /// the book creates the spell, and casting reads only that frozen sum, so neither a rebalance
        /// nor a new rune can change a spell that is already in the book.
        /// </summary>
        public int windupTicks;

        public int channelTicks;

        public int recoveryTicks;

        /// <summary>
        /// Mana and neural heat this rune adds to a spell's cost, and the bloodline experience it
        /// adds to the reward. Summed and frozen like the timings; 0 means the rune costs nothing.
        /// </summary>
        public float manaCost;

        public float entropyCost;

        public float experience;

        /// <summary>
        /// True when the colony has to unlock this rune before anyone may arrange it. Unlocks are
        /// shared by the whole colony (<see cref="GameComponent_Spellbook.Unlock"/>).
        /// </summary>
        public bool requiresUnlock;

        /// <summary>
        /// Materials the altar consumes from its surroundings when a rune awakening opens this rune.
        /// Empty means the rune needs no materials beyond the ritual's mana.
        /// </summary>
        public List<SpellGlyphCost> unlockCost = new List<SpellGlyphCost>();

        /// <summary>True when this augment declares <paramref name="augmentDefName"/> as mutually exclusive.</summary>
        public bool ExcludesAugment(string augmentDefName)
        {
            return excludesAugments != null && excludesAugments.Contains(augmentDefName);
        }

        /// <summary>True only when this glyph declares <paramref name="augmentDefName"/> as compatible.</summary>
        public bool AcceptsAugment(string augmentDefName)
        {
            if (compatibleAugments == null || compatibleAugments.Count == 0 || augmentDefName.NullOrEmpty())
            {
                return false;
            }

            for (int i = 0; i < compatibleAugments.Count; i++)
            {
                if (compatibleAugments[i] == augmentDefName)
                {
                    return true;
                }
            }

            return false;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if ((railgun != null || tornado != null) && kind != SpellGlyphKind.Super)
            {
                yield return "only a Super glyph may carry railgun or tornado numbers";
            }

            if (excludesAugments != null)
            {
                if (kind != SpellGlyphKind.Augment)
                {
                    yield return "only an Augment glyph may list excludesAugments";
                }

                foreach (string entry in excludesAugments)
                {
                    if (entry.NullOrEmpty() || entry == defName)
                    {
                        yield return "excludesAugments has an empty entry or names this glyph itself";
                    }
                }
            }

            if (windupTicks < 0 || channelTicks < 0 || recoveryTicks < 0)
            {
                yield return "cast timings windupTicks/channelTicks/recoveryTicks must not be negative";
            }

            if (manaCost < 0f || entropyCost < 0f || experience < 0f)
            {
                yield return "manaCost, entropyCost and experience must not be negative";
            }

            if (tier < 0 || tier > 3)
            {
                yield return "tier must be between 0 (known from the start) and 3";
            }
            else if (kind != SpellGlyphKind.Super && requiresUnlock != (tier > 0))
            {
                yield return "requiresUnlock must be true exactly when tier is above 0";
            }

            if (unlockCost != null)
            {
                for (int i = 0; i < unlockCost.Count; i++)
                {
                    SpellGlyphCost cost = unlockCost[i];
                    if (cost == null || cost.count <= 0)
                    {
                        yield return "unlockCost has a missing entry or a non-positive count";
                        continue;
                    }

                    if ((cost.thingDef == null) == (cost.category == null))
                    {
                        yield return "an unlockCost entry must name exactly one thingDef or one category";
                    }
                }
            }

            if (kind != SpellGlyphKind.Form && (projectile != null || orbit != null || range > 0f))
            {
                yield return "only a Form glyph may carry range, projectile or orbit numbers";
            }

            if (iconPath.NullOrEmpty())
            {
                yield return "iconPath is empty; the book will fall back to Textures/Spellbook/Glyphs/"
                             + (defName.NullOrEmpty() ? "<defName>" : defName);
            }

            if (compatibleAugments != null)
            {
                for (int i = 0; i < compatibleAugments.Count; i++)
                {
                    string entry = compatibleAugments[i];
                    if (entry.NullOrEmpty())
                    {
                        yield return "compatibleAugments has an empty entry";
                        continue;
                    }

                    for (int j = i + 1; j < compatibleAugments.Count; j++)
                    {
                        if (compatibleAugments[j] == entry)
                        {
                            yield return "compatibleAugments lists " + entry + " more than once";
                        }
                    }
                }
            }

            if ((kind == SpellGlyphKind.Form || kind == SpellGlyphKind.Augment) && combatLogRules != null)
            {
                yield return "only a damaging Effect or Super glyph may declare combatLogRules";
            }

            switch (kind)
            {
                case SpellGlyphKind.Form:
                    if (factor > 0f || missiles > 0)
                    {
                        yield return "a Form glyph must not carry effect/augment numbers";
                    }

                    if (targets != null)
                    {
                        yield return "only an Effect or Super glyph may declare targets";
                    }

                    // The form kind is read from the data blocks; self and touch are the only forms
                    // known by name.
                    int blocks = (projectile != null ? 1 : 0) + (orbit != null ? 1 : 0);
                    bool named = defName == SpellDefs.FormSelf || defName == SpellDefs.FormTouch;
                    if (blocks + (named ? 1 : 0) != 1)
                    {
                        yield return "a Form glyph must be self or touch, or carry exactly one projectile or orbit block";
                    }

                    bool aims = projectile != null || defName == SpellDefs.FormTouch;
                    if (aims && range <= 0f)
                    {
                        yield return "a projectile or touch form needs a positive range";
                    }
                    else if (!aims && range != 0f)
                    {
                        yield return "only a projectile or touch form reads range";
                    }

                    if (projectile != null)
                    {
                        foreach (string error in projectile.ConfigErrors())
                        {
                            yield return "projectile: " + error;
                        }

                        if (projectile.maxRangeCells < range)
                        {
                            yield return "a projectile's maxRangeCells must reach at least its aim range";
                        }
                    }

                    if (orbit != null && (orbit.count < 1 || orbit.distance <= 0f || orbit.angularSpeedDegrees <= 0f
                                          || orbit.hitRadius <= 0f || orbit.lifetimeTicks <= 0))
                    {
                        yield return "an orbit form needs a positive count, distance, angularSpeedDegrees, hitRadius and lifetimeTicks";
                    }

                    break;
                case SpellGlyphKind.Super:
                    if (factor > 0f || missiles > 0)
                    {
                        yield return "a Super glyph must not carry effect/augment numbers";
                    }

                    if (theme == null)
                    {
                        yield return "a Super glyph must declare its theme";
                    }

                    if (combatLogRules == null)
                    {
                        yield return "a Super glyph must declare its combatLogRules";
                    }

                    if (targets == null || targets.Count == 0)
                    {
                        yield return "a Super glyph must declare its own targets";
                    }

                    if ((railgun == null) == (tornado == null))
                    {
                        yield return "a Super glyph must carry exactly one railgun or tornado block";
                    }
                    if (tornado != null)
                        foreach (string error in tornado.ConfigErrors()) yield return error;
                    if (railgun != null)
                    {
                        if (railgun.damage <= 0f)
                        {
                            yield return "a Super glyph needs a positive damage";
                        }

                        // range has no lower bound on purpose: 0 or less is the documented whole-map
                        // aim (SpellRailgunSpec.range), so it must not be reported as a config error.
                        if (railgun.hitWidth <= 0f)
                        {
                            yield return "a Super glyph needs a positive hitWidth";
                        }

                        if (railgun.vfxDef.NullOrEmpty())
                        {
                            yield return "a Super glyph must name its vfxDef";
                        }
                    }

                    break;
                case SpellGlyphKind.Effect:
                    if (targets == null || targets.Count == 0)
                    {
                        yield return "an Effect glyph must declare its targets";
                    }

                    if (theme == null)
                    {
                        yield return "an Effect glyph must declare its theme";
                    }

                    if (!(this is EffectGlyphDef))
                        yield return "an Effect glyph must use a concrete EffectGlyphDef Class";

                    break;
                case SpellGlyphKind.Augment:
                    // The compiler reads these numbers as written; there is no code default behind them.
                    switch (defName)
                    {
                        case SpellDefs.AugmentAmplify:
                        case SpellDefs.AugmentDampen:
                        case SpellDefs.AugmentExtend:
                        case SpellDefs.AugmentShorten:
                        case SpellDefs.AugmentBreakArmor:
                        case SpellDefs.AugmentAccelerate:
                        case SpellDefs.AugmentDecelerate:
                            if (factor <= 0f)
                            {
                                yield return defName + " needs a positive factor";
                            }

                            break;
                        case SpellDefs.AugmentAoe:
                            if (radius <= 0f)
                            {
                                yield return defName + " needs a positive radius";
                            }

                            break;
                        case SpellDefs.AugmentSplit:
                            if (missiles <= 0)
                            {
                                yield return defName + " needs a positive missiles count";
                            }

                            break;
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// One material a rune awakening takes from the ground around the altar. Exactly one of
    /// <see cref="thingDef"/> and <see cref="category"/> is set: a named item, or any item in a thing
    /// category - the spell table asks for "any stone blocks", which no single stone Def can express.
    /// </summary>
    public class SpellGlyphCost
    {
        public ThingDef thingDef;

        public ThingCategoryDef category;

        public int count;

        /// <summary>True when <paramref name="thing"/> is a thing this entry counts.</summary>
        public bool Matches(Thing thing)
        {
            return thingDef != null ? thing.def == thingDef : thing.def.IsWithinCategory(category);
        }

        /// <summary>Player-facing name of what is asked for, for the altar's rune menu.</summary>
        public string Label => thingDef != null ? thingDef.label : category.label;
    }

    /// <summary>
    /// The few numbers that belong to the book and the caster rather than to any rune: chain length,
    /// cooldown and channel movement. Everything a form or an effect owns lives on its glyph Def.
    /// </summary>
    public class SpellTuningDef : Def
    {
        /// <summary>Longest chain the book accepts; the UI shows the same value from this Def.</summary>
        public int maxGlyphs;

        /// <summary>Ticks between two casts by the same pawn; the only limiter in this experiment.</summary>
        public int cooldownTicks;

        /// <summary>
        /// Movement speed during a cast's channel phase, as a fraction of normal speed. Windup is not
        /// slowed; recovery roots the caster instead of slowing it.
        /// </summary>
        public float channelMoveSpeedFactor;

        /// <summary>Hard cap on darts or orbs after Split.</summary>
        public int maxMissiles;

        /// <summary>Base spell strength of a staff, before its material adds anything.</summary>
        public float staffSpellPower;

        /// <summary>Duration extension per point of the material's heat armour power.</summary>
        public float staffHeatArmorDurationBonus;

        /// <summary>Lowest mana multiplier a material may reach.</summary>
        public float staffMinManaFactor;

        /// <summary>Mana-cost reduction per point of the material's blunt armour power.</summary>
        public float staffBluntArmorCostReduction;

        /// <summary>Highest duration multiplier a material may reach.</summary>
        public float staffMaxDurationFactor;

        /// <summary>Highest damage multiplier a material may reach.</summary>
        public float staffMaxDamageFactor;

        /// <summary>Fallback spell theme when a plan contains no themed glyph.</summary>
        public SpellThemeDef defaultTheme;

        /// <summary>Shared VfxDef used during normal spell windup + channel convergence.</summary>
        public string castVfxDef;

        /// <summary>Prep-tick threshold below which spells use <see cref="quickCastVfxDef"/> instead of <see cref="castVfxDef"/>.</summary>
        public int quickCastMaxTicks;

        /// <summary>Compact surging particle-cloud VfxDef for fast casts (<c>&lt; quickCastMaxTicks</c>).</summary>
        public string quickCastVfxDef;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (maxGlyphs < 1)
            {
                yield return "maxGlyphs must be at least 1";
            }

            if (cooldownTicks < 0)
            {
                yield return "cooldownTicks must not be negative";
            }

            if (channelMoveSpeedFactor <= 0f || channelMoveSpeedFactor > 1f)
            {
                yield return "channelMoveSpeedFactor must be greater than 0 and at most 1";
            }

            if (maxMissiles < 1)
            {
                yield return "maxMissiles must be at least 1";
            }

            if (staffSpellPower <= 0f)
            {
                yield return "staffSpellPower must be greater than 0";
            }

            if (staffHeatArmorDurationBonus < 0f)
            {
                yield return "staffHeatArmorDurationBonus must not be negative";
            }

            if (staffMinManaFactor <= 0f || staffMinManaFactor > 1f)
            {
                yield return "staffMinManaFactor must be greater than 0 and at most 1";
            }

            if (staffBluntArmorCostReduction < 0f || staffBluntArmorCostReduction > 1f)
            {
                yield return "staffBluntArmorCostReduction must be between 0 and 1";
            }

            if (staffMaxDurationFactor < 1f)
            {
                yield return "staffMaxDurationFactor must be at least 1";
            }

            if (staffMaxDamageFactor < 1f)
            {
                yield return "staffMaxDamageFactor must be at least 1";
            }

            if (defaultTheme == null)
            {
                yield return "defaultTheme is required";
            }

            if (castVfxDef.NullOrEmpty())
            {
                yield return "castVfxDef is required";
            }

            if (quickCastMaxTicks <= 0)
            {
                yield return "quickCastMaxTicks must be positive";
            }

            if (quickCastVfxDef.NullOrEmpty())
            {
                yield return "quickCastVfxDef is required";
            }
        }
    }

    /// <summary>Fixed def names and lookups the runtime and the book share. No duplicated literals.</summary>
    public static class SpellDefs
    {
        public const string FormSelf = "WotF_Form_Self";
        public const string FormTouch = "WotF_Form_Touch";
        public const string FormProjectile = "WotF_Form_Projectile";
        public const string FormOrbit = "WotF_Form_Orbit";
        public const string SuperRailgun = "WotF_Form_Railgun";
        public const string SuperTornado = "WotF_Super_Tornado";

        /// <summary>The railgun's visual effect Def; the spell only ever names it, never its look.</summary>
        public const string VfxRailgun = "WotF_Vfx_Railgun";

        public const string EffectHarm = "WotF_Effect_Harm";
        public const string EffectHeal = "WotF_Effect_Heal";
        public const string EffectExplosion = "WotF_Effect_Explosion";
        public const string EffectGrow = "WotF_Effect_Grow";
        public const string EffectLightning = "WotF_Effect_Lightning";
        public const string EffectFreeze = "WotF_Effect_Freeze";
        public const string EffectColdSnap = "WotF_Effect_ColdSnap";
        public const string EffectKnockback = "WotF_Effect_Knockback";
        public const string EffectPull = "WotF_Effect_Pull";
        public const string EffectSnare = "WotF_Effect_Snare";
        public const string EffectHex = "WotF_Effect_Hex";
        public const string EffectMagelight = "WotF_Effect_Magelight";
        public const string EffectPlanarShield = "WotF_Effect_PlanarShield";
        public const string EffectBlink = "WotF_Effect_Blink";
        public const string EffectLinger = "WotF_Effect_Linger";
        public const string EffectRune = "WotF_Effect_Rune";
        public const string EffectDelay = "WotF_Effect_Delay";

        public const string AugmentAmplify = "WotF_Augment_Amplify";
        public const string AugmentAoe = "WotF_Augment_AOE";
        public const string AugmentAccelerate = "WotF_Augment_Accelerate";
        public const string AugmentDecelerate = "WotF_Augment_Decelerate";
        public const string AugmentSplit = "WotF_Augment_Split";
        public const string AugmentDampen = "WotF_Augment_Dampen";
        public const string AugmentExtend = "WotF_Augment_Extend";
        public const string AugmentShorten = "WotF_Augment_Shorten";
        public const string AugmentSensitive = "WotF_Augment_Sensitive";
        public const string AugmentBreakArmor = "WotF_Augment_BreakArmor";
        public const string AugmentPierce = "WotF_Augment_Pierce";

        /// <summary>The damage type of every damaging spell.</summary>
        public const string ArcaneForceDamageDefName = "WotF_ArcaneMissileDamage";

        /// <summary>Carrier ability that puts the book and the cast gizmos on an eligible pawn.</summary>
        public const string CastAbilityDefName = "WotF_SpellbookCast";

        public const string TuningDefName = "WotF_SpellTuning";

        /// <summary>Texture convention for glyph icons owned by the interface task.</summary>
        public const string GlyphTextureFolder = "Spellbook/Glyphs/";

        public static SpellGlyphDef Glyph(string defName)
        {
            return defName.NullOrEmpty() ? null : DefDatabase<SpellGlyphDef>.GetNamedSilentFail(defName);
        }

        /// <summary>
        /// The tuning Def, or null when the XML is missing. There is no in-code fallback: the spell
        /// entries (compile, book, ability grant) refuse service instead.
        /// </summary>
        public static SpellTuningDef Tuning => DefDatabase<SpellTuningDef>.GetNamedSilentFail(TuningDefName);

        public static SpellThemeDef ResolveTheme(SpellCastPlan plan)
        {
            if (plan?.glyphs != null)
            {
                for (int i = 0; i < plan.glyphs.Count; i++)
                {
                    SpellGlyphDef glyph = Glyph(plan.glyphs[i]);
                    if (glyph?.theme != null)
                    {
                        return glyph.theme;
                    }
                }
            }

            return Tuning?.defaultTheme;
        }

        public static SpellThemeDef ResolveTheme(IReadOnlyList<SpellGlyphDef> defs, int start, int end, SpellThemeDef fallback)
        {
            if (defs != null)
            {
                for (int i = start; i < end && i < defs.Count; i++)
                {
                    if (defs[i]?.theme != null)
                    {
                        return defs[i].theme;
                    }
                }
            }

            return fallback ?? Tuning?.defaultTheme;
        }

        public static string IconPathFor(SpellGlyphDef glyph)
        {
            if (glyph == null)
            {
                return null;
            }

            return glyph.iconPath.NullOrEmpty() ? GlyphTextureFolder + glyph.defName : glyph.iconPath;
        }
    }
}
