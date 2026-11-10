using System.Collections.Generic;
using Verse;
using WhisperOfTheForest.Missiles;

namespace WhisperOfTheForest.Spells
{
    /// <summary>The rune categories the book shows and the grammar understands.</summary>
    public enum SpellGlyphKind
    {
        Form,
        Effect,
        Augment
    }

    /// <summary>
    /// One catalogue entry. The Def only names the kind and the bounded numbers; every behaviour is
    /// implemented in code (<see cref="SpellCompiler"/>, <see cref="SpellExecutor"/>), so a new XML
    /// entry can tune a spell but cannot invent behaviour.
    /// </summary>
    public class SpellGlyphDef : Def
    {
        public SpellGlyphKind kind;

        /// <summary>Texture path under Textures/. Empty falls back to the contract convention.</summary>
        public string iconPath;

        /// <summary>Effect: base damage / healing / explosion damage before Amplify.</summary>
        public float amount;

        /// <summary>
        /// Data switch for Effects that resolve without a number: an instant growth finishes the
        /// plant and has neither damage nor healing, so its own XML entry declares
        /// <c>requiresAmount=false</c>. Everything else keeps the default, so a damage/heal/explosion
        /// entry that forgets its amount still reports a visible config error instead of silently
        /// falling back to a code default. Only Effect glyphs may turn this off.
        /// </summary>
        public bool requiresAmount = true;

        /// <summary>
        /// What this rune's effect may be aimed at, in the spell system's own categories. Required
        /// for every Effect rune and for a composite form (which is a whole spell on its own); other
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
        /// Composite form payload: when set, this Form rune is a whole spell on its own - it needs no
        /// Effect and accepts none - and these numbers are what it casts. Null for every other rune.
        /// </summary>
        public SpellRailgunSpec railgun;

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

        /// <summary>Damage effects: armour penetration of the damage they deal; Break Armor adds to it.</summary>
        public float armorPenetration;

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
        public List<ThingDefCountClass> unlockCost = new List<ThingDefCountClass>();

        /// <summary>
        /// How long the effect keeps lasting (Freeze, Snare, Hex, Ignite, Clotting, Magelight, a
        /// Linger/Rune/Delay, or a sustained Harm). 0 means the rune has no duration of its own;
        /// Extend/Shorten only ever turn a Harm into its sustained form.
        /// </summary>
        public int durationTicks;

        /// <summary>Spacing between two pulses: sustained Harm, a Linger beat.</summary>
        public int pulseIntervalTicks;

        /// <summary>Cold Snap: damage multiplier against a target that is already slowed.</summary>
        public float vulnerableFactor;

        /// <summary>Lightning: EMP damage delivered before the burn.</summary>
        public float empAmount;

        /// <summary>Lightning: extra damage per charge stack already on the target.</summary>
        public float perStackFactor;

        /// <summary>Ignite: size of the fire the spreading variant starts.</summary>
        public float fireSize;

        /// <summary>Ignite (Sensitive): damage per pulse of the non-spreading burn.</summary>
        public float burnAmount;

        /// <summary>Magelight: radius of the light it places.</summary>
        public float glowRadius;

        /// <summary>Magelight: colour of the light, as an HTML hex string.</summary>
        public string glowColor;

        /// <summary>True when this rune already carries its own effect (the composite form entry).</summary>
        public bool IsCompleteForm => railgun != null;

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

            if (railgun != null && kind != SpellGlyphKind.Form)
            {
                yield return "only a Form glyph may carry composite-form numbers";
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

            if (unlockCost != null)
            {
                for (int i = 0; i < unlockCost.Count; i++)
                {
                    ThingDefCountClass cost = unlockCost[i];
                    if (cost == null || cost.thingDef == null || cost.count <= 0)
                    {
                        yield return "unlockCost has a missing thing or a non-positive count";
                    }
                }
            }

            if (durationTicks < 0 || pulseIntervalTicks < 0)
            {
                yield return "durationTicks and pulseIntervalTicks must not be negative";
            }

            if (glowRadius < 0f || armorPenetration < 0f)
            {
                yield return "glowRadius and armorPenetration must not be negative";
            }

            if (kind != SpellGlyphKind.Form && (projectile != null || orbit != null || range > 0f))
            {
                yield return "only a Form glyph may carry range, projectile or orbit numbers";
            }

            if (vulnerableFactor < 0f || empAmount < 0f || perStackFactor < 0f || fireSize < 0f)
            {
                yield return "vulnerableFactor, empAmount, perStackFactor and fireSize must not be negative";
            }

            if (!glowColor.NullOrEmpty() && !glowColor.StartsWith("#"))
            {
                yield return "glowColor must be an HTML colour such as #FFE9A0";
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

            switch (kind)
            {
                case SpellGlyphKind.Form:
                    if (amount > 0f || factor > 0f || missiles > 0)
                    {
                        yield return "a Form glyph must not carry effect/augment numbers";
                    }

                    if (!requiresAmount)
                    {
                        yield return "only an Effect glyph may set requiresAmount to false";
                    }

                    if (railgun != null && (targets == null || targets.Count == 0))
                    {
                        yield return "a composite form must declare its own targets";
                    }
                    else if (railgun == null && targets != null)
                    {
                        yield return "only an Effect glyph or a composite form may declare targets";
                    }

                    // The form kind is read from the data blocks; self and touch are the only forms
                    // known by name.
                    int blocks = (railgun != null ? 1 : 0) + (projectile != null ? 1 : 0) + (orbit != null ? 1 : 0);
                    bool named = defName == SpellDefs.FormSelf || defName == SpellDefs.FormTouch;
                    if (blocks + (named ? 1 : 0) != 1)
                    {
                        yield return "a Form glyph must be self or touch, or carry exactly one projectile, orbit or railgun block";
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

                    if (railgun != null)
                    {
                        if (railgun.damage <= 0f)
                        {
                            yield return "a composite form needs a positive damage";
                        }

                        // range has no lower bound on purpose: 0 or less is the documented whole-map
                        // aim (SpellRailgunSpec.range), so it must not be reported as a config error.
                        if (railgun.hitWidth <= 0f)
                        {
                            yield return "a composite form needs a positive hitWidth";
                        }

                        if (railgun.vfxDef.NullOrEmpty())
                        {
                            yield return "a composite form must name its vfxDef";
                        }
                    }

                    break;
                case SpellGlyphKind.Effect:
                    if (targets == null || targets.Count == 0)
                    {
                        yield return "an Effect glyph must declare its targets";
                    }

                    if (requiresAmount)
                    {
                        if (amount <= 0f)
                        {
                            yield return "an Effect glyph needs a positive amount";
                        }
                    }
                    else if (amount != 0f)
                    {
                        yield return "an Effect glyph with requiresAmount=false must not carry an amount";
                    }

                    if (radius < 0f)
                    {
                        yield return "an Effect glyph must not define a negative radius";
                    }

                    // AOE grows the effect's own radius; there is no shared base radius behind it.
                    if (AcceptsAugment(SpellDefs.AugmentAoe) && radius <= 0f)
                    {
                        yield return "an Effect that accepts AOE needs a positive radius";
                    }

                    break;
                case SpellGlyphKind.Augment:
                    if (amount > 0f)
                    {
                        yield return "an Augment glyph must not define an amount";
                    }

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

                    if (!requiresAmount)
                    {
                        yield return "only an Effect glyph may set requiresAmount to false";
                    }

                    break;
            }
        }
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
        }
    }

    /// <summary>Fixed def names and lookups the runtime and the book share. No duplicated literals.</summary>
    public static class SpellDefs
    {
        public const string FormSelf = "WotF_Form_Self";
        public const string FormTouch = "WotF_Form_Touch";
        public const string FormProjectile = "WotF_Form_Projectile";
        public const string FormOrbit = "WotF_Form_Orbit";

        /// <summary>Composite form: this one rune is a complete spell (the railgun supra rune).</summary>
        public const string FormRailgun = "WotF_Form_Railgun";

        /// <summary>The railgun's visual effect Def; the spell only ever names it, never its look.</summary>
        public const string VfxRailgun = "WotF_Vfx_Railgun";

        public const string EffectHarm = "WotF_Effect_Harm";
        public const string EffectHeal = "WotF_Effect_Heal";
        public const string EffectExplosion = "WotF_Effect_Explosion";
        public const string EffectGrow = "WotF_Effect_Grow";
        public const string EffectIgnite = "WotF_Effect_Ignite";
        public const string EffectLightning = "WotF_Effect_Lightning";
        public const string EffectFreeze = "WotF_Effect_Freeze";
        public const string EffectColdSnap = "WotF_Effect_ColdSnap";
        public const string EffectKnockback = "WotF_Effect_Knockback";
        public const string EffectPull = "WotF_Effect_Pull";
        public const string EffectSnare = "WotF_Effect_Snare";
        public const string EffectHex = "WotF_Effect_Hex";
        public const string EffectMagelight = "WotF_Effect_Magelight";
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
