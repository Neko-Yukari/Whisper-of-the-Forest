using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Experimental.Spells
{
    /// <summary>
    /// The rune categories the book shows and the grammar understands. A Trigger rune is not carried
    /// on its own: it ends the segment on its left and starts the next one, which must be a Form.
    /// </summary>
    public enum SpellGlyphKind
    {
        Form,
        Effect,
        Augment,
        Trigger
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

        /// <summary>Effect: base working radius. Augment AOE: radius added per stack.</summary>
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
        /// Composite form payload: when set, this Form rune is a whole spell on its own - it needs no
        /// Effect and accepts none - and these numbers are what it casts. Null for every other rune.
        /// </summary>
        public SpellRailgunSpec railgun;

        /// <summary>
        /// Ticks this rune adds to a spell's three phases. They are summed along the whole chain when
        /// the book creates the spell, and casting reads only that frozen sum, so neither a rebalance
        /// nor a new rune can change a spell that is already in the book.
        /// </summary>
        public int windupTicks;

        public int channelTicks;

        public int recoveryTicks;

        /// <summary>
        /// How long the effect keeps lasting (Freeze, Snare, Hex, Ignite, Clotting, Magelight, a
        /// Linger/Rune/Delay, an Orbit, or a sustained Harm). 0 means the rune has no duration of its
        /// own; Extend/Shorten only ever turn a Harm into its sustained form.
        /// </summary>
        public int durationTicks;

        /// <summary>Spacing between two pulses: sustained Harm, a Linger beat, an Orbit re-check.</summary>
        public int pulseIntervalTicks;

        /// <summary>Orbit: how close a pawn must pass to be touched.</summary>
        public float width;

        /// <summary>Orbit: turn rate in degrees per second.</summary>
        public float angularSpeedDegrees;

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

            if (windupTicks < 0 || channelTicks < 0 || recoveryTicks < 0)
            {
                yield return "cast timings windupTicks/channelTicks/recoveryTicks must not be negative";
            }

            if (durationTicks < 0 || pulseIntervalTicks < 0)
            {
                yield return "durationTicks and pulseIntervalTicks must not be negative";
            }

            if (width < 0f || angularSpeedDegrees < 0f || glowRadius < 0f)
            {
                yield return "width, angularSpeedDegrees and glowRadius must not be negative";
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
                yield return "iconPath is empty; the book will fall back to Textures/Experiments/Spellbook/Glyphs/"
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

                    if (defName == SpellDefs.FormOrbit)
                    {
                        if (radius <= 0f)
                        {
                            yield return "the orbit form needs a positive radius (distance from the caster)";
                        }

                        if (width <= 0f)
                        {
                            yield return "the orbit form needs a positive width (touch radius)";
                        }

                        if (angularSpeedDegrees <= 0f)
                        {
                            yield return "the orbit form needs a positive angularSpeedDegrees";
                        }

                        if (durationTicks <= 0)
                        {
                            yield return "the orbit form needs a positive durationTicks";
                        }
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

                    break;
                case SpellGlyphKind.Augment:
                    if (amount > 0f)
                    {
                        yield return "an Augment glyph must not define an amount";
                    }

                    if (!requiresAmount)
                    {
                        yield return "only an Effect glyph may set requiresAmount to false";
                    }

                    break;
                case SpellGlyphKind.Trigger:
                    // A trigger rune only marks where one segment ends. Numbers on it would be read
                    // by nobody, so they are a config error instead of a silent no-op.
                    if (amount > 0f || factor > 0f || missiles > 0 || radius > 0f)
                    {
                        yield return "a Trigger glyph must not carry effect/augment numbers";
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
    /// Bounded defaults shared by every recipe: casting range, touch range, cooldown and the
    /// projectile envelope a spell dart starts from. Kept in the mod's own XML so a balance pass
    /// never needs a rebuild.
    /// </summary>
    public class SpellTuningDef : Def
    {
        /// <summary>Longest chain the book accepts; the UI shows the same value from this Def.</summary>
        public int maxGlyphs = 10;

        /// <summary>Casting range for Projectile and Touch forms, in cells, measured from the caster.</summary>
        public float castRange = 24.9f;

        /// <summary>Touch form only reaches cells this close (8-way neighbours and the caster's own cell).</summary>
        public float touchRange = 1.45f;

        /// <summary>Ticks between two casts by the same pawn; the only limiter in this experiment.</summary>
        public int cooldownTicks = 120;

        /// <summary>
        /// Movement speed during a cast's channel phase, as a fraction of normal speed. Windup is not
        /// slowed; recovery roots the caster instead of slowing it.
        /// </summary>
        public float channelMoveSpeedFactor;

        /// <summary>Base missile speed for a spell dart before Accelerate/Decelerate.</summary>
        public float missileSpeed = 16f;

        /// <summary>How fast a spell dart may turn, in degrees per second.</summary>
        public float missileTurnDegreesPerSecond = 300f;

        /// <summary>Missiles per cast before Split.</summary>
        public int missileCount = 1;

        /// <summary>Hard cap on missiles after Split.</summary>
        public int maxMissiles = 5;

        /// <summary>Base radius of an AOE'd Harm/Heal effect.</summary>
        public float aoeBaseRadius = 1.9f;

        /// <summary>Hard cap on any effect radius after AOE.</summary>
        public float radiusCap = 6f;

        /// <summary>Hard cap on the total Amplify multiplier applied to one effect.</summary>
        public float amplifyCap = 5f;

        /// <summary>Absolute damage/heal ceiling after Amplify.</summary>
        public float amountCap = 200f;

        /// <summary>Armour penetration of every spell damage/explosion source.</summary>
        public float armorPenetration = 0.5f;

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

            if (castRange <= 0f || touchRange <= 0f)
            {
                yield return "castRange and touchRange must be positive";
            }

            if (cooldownTicks < 0)
            {
                yield return "cooldownTicks must not be negative";
            }

            if (channelMoveSpeedFactor <= 0f || channelMoveSpeedFactor > 1f)
            {
                yield return "channelMoveSpeedFactor must be greater than 0 and at most 1";
            }

            if (missileCount < 1 || maxMissiles < missileCount)
            {
                yield return "missileCount must be at least 1 and maxMissiles must not be below it";
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

        /// <summary>Composite form: this one rune is a complete spell (Experiments/railgun.md).</summary>
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

        /// <summary>Trigger rune: the segment on its left continues after a real hit.</summary>
        public const string TriggerOnHit = "WotF_Trigger_OnHit";

        /// <summary>Ring fleck played where a trigger hands over to the next segment.</summary>
        public const string TriggerFleckDefName = "WotF_Fleck_TriggerRing";

        /// <summary>Receiver of a spell projectile's payload.</summary>
        public const string SpellImpactDefName = "WotF_Impact_SpellCast";

        /// <summary>Carrier ability that puts the book and the cast gizmos on an eligible pawn.</summary>
        public const string CastAbilityDefName = "WotF_SpellbookCast";

        public const string TuningDefName = "WotF_SpellTuning";

        /// <summary>Texture convention for glyph icons owned by the interface task.</summary>
        public const string GlyphTextureFolder = "Experiments/Spellbook/Glyphs/";

        public static SpellGlyphDef Glyph(string defName)
        {
            return defName.NullOrEmpty() ? null : DefDatabase<SpellGlyphDef>.GetNamedSilentFail(defName);
        }

        /// <summary>Tuning Def, or documented in-code defaults when the XML is missing.</summary>
        public static SpellTuningDef Tuning
        {
            get
            {
                SpellTuningDef def = DefDatabase<SpellTuningDef>.GetNamedSilentFail(TuningDefName);
                if (def == null)
                {
                    SpellDiagnostics.ErrorOnce("tuning def " + TuningDefName + " is missing; using built-in defaults.");
                    return new SpellTuningDef();
                }

                return def;
            }
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
