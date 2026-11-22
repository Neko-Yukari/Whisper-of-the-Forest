using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using WhisperOfTheForest.AncientRuins;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Rituals
{
    public enum RitualFamily
    {
        Unspecified,
        Crafting,
        Life,
        Domain
    }

    public enum ChalkColor
    {
        White,
        Green,
        Star
    }

    public class RuneAwakeningTier
    {
        public int tier;
        public int ringSize;
        public ChalkColor chalkColor;
        public float manaCost;
        public float workAmount;
    }

    /// <summary>
    /// One ruin the seeking rite can point at: the quest that places it, the site part that labels and
    /// draws it on the world map, and how much harder its guards are than the map's own threat points.
    /// </summary>
    public class RuinSeekOption
    {
        public QuestScriptDef quest;
        public SitePartDef sitePart;
        public float threatPointsFactor = 1f;
    }

    /// <summary>
    /// A ritual is a ring of chalk marks around an altar, a paid mana cost and a timed performance.
    /// The Def owns the numbers; the altar and the job driver only read them.
    /// </summary>
    public class RitualDef : Def
    {
        public int tier;
        public RitualFamily family;
        public int ringSize = 3;
        public ChalkColor chalkColor = ChalkColor.White;
        public float manaCost;
        public bool sustained;
        public float manaPerStrike;
        public int durationTicks;
        public float workAmount;
        public float effectRadius = 12.9f;
        public float manaRestore;
        public int shieldCharges;
        public int shieldDurationTicks;
        public string shieldVfxDef;
        public int counterStunTicks;
        public float baseStrikesPerMinute;
        public float minStrikeRadius;
        public int strikeDamage;
        public float strikeArmorPenetration;
        public float strikeRadius;
        public float strikeIntervalJitter;
        public int idleScanIntervalTicks;

        public SimpleCurve strengthSpeedFactor;
        public SimpleCurve strengthRadiusFactor;
        public SimpleCurve strengthEffectFactor;
        public float tameChancePerStrength;
        public RitualHealingTableDef healingTable;
        public float healingPoolFactor;

        /// <summary>Minimum ritual strength required to perform this rite; 0 means none.</summary>
        public float minRitualStrength;

        /// <summary>Bloodline participants required on the ring before the rite may start.</summary>
        public int minParticipants;

        /// <summary>Weather this rite settles over the map; only weather rites use it.</summary>
        public WeatherDef weatherDef;

        /// <summary>True when the rite needs deep resources under the altar before it may start.</summary>
        public bool requiresDeepResources;

        /// <summary>How many distinct deep resources a leyline rite brings up at most.</summary>
        public int maxResourceTypes = 1;

        /// <summary>The animal a beast-call rite tries to tame; null for every other rite.</summary>
        public PawnKindDef summonedAnimal;

        /// <summary>Base chance of the single taming attempt, before handler skill and wildness.</summary>
        public float tameChanceBase;

        /// <summary>Added to the taming chance per level of the handler's Animals skill.</summary>
        public float tameChancePerSkill;

        /// <summary>Subtracted from the taming chance per point of the animal's wildness.</summary>
        public float tameWildnessFactor;

        /// <summary>Materials taken from the ground around the altar when the rite starts.</summary>
        public List<ThingDefCountClass> inputCosts;

        /// <summary>Things left beside the altar when the rite finishes.</summary>
        public List<ThingDefCountClass> outputProducts;

        /// <summary>
        /// True when the ritual's target is one rune the colony has not unlocked yet. The altar asks
        /// which rune from its own menu and passes it to the effect worker; the Def carries no rune.
        /// </summary>
        public bool chooseRune;
        public List<RuneAwakeningTier> runeTiers;

        public RuneAwakeningTier RuneTierFor(int runeTier) => runeTiers.Find(entry => entry.tier == runeTier);

        /// <summary>True when the rite asks the altar to pick a teleport node before it starts.</summary>
        public bool chooseTeleportTarget;

        /// <summary>The ruin this seeking rite points at; null for every other rite.</summary>
        public RuinSeekOption ruinTarget;

        public bool SeeksRuin => ruinTarget != null;

        /// <summary>
        /// True when the rite writes one of the host's spells into a weapon or armour lying beside the
        /// altar. The altar asks which item and which book slot and passes both to the effect worker.
        /// </summary>
        public bool chooseEquipment;

        /// <summary>True when the altar may repeat this production rite on its own while idle.</summary>
        public bool autoRepeatable;

        /// <summary>Research project that must be completed before this rite becomes available.</summary>
        public ResearchProjectDef researchPrerequisite;

        public Type effectWorkerClass;

        private RitualEffectWorker effectWorker;

        public RitualEffectWorker EffectWorker
        {
            get
            {
                if (effectWorker == null)
                {
                    effectWorker = (RitualEffectWorker)Activator.CreateInstance(effectWorkerClass);
                }

                return effectWorker;
            }
        }

        public bool UsesWork => workAmount > 0f || chooseRune;

        public bool UsesEffectStrength => strengthRadiusFactor != null
            || strengthEffectFactor != null
            || tameChancePerStrength > 0f
            || healingTable != null
            || counterStunTicks > 0;

        public bool UsesRitualStrength => UsesWork || UsesEffectStrength;

        /// <summary>
        /// Single source of truth for whether this rite is unlocked by research in the active game.
        /// </summary>
        public bool IsResearchFinished => Current.Game?.researchManager != null && researchPrerequisite.IsFinished;

        public float HealingPoolFor(float ritualStrength)
        {
            return healingTable.BasePoolFor(ritualStrength) * healingPoolFactor;
        }

        public float SpeedFactorFor(float ritualStrength)
        {
            return strengthSpeedFactor.Evaluate(ritualStrength);
        }

        public float RadiusFor(float ritualStrength)
        {
            return effectRadius * (strengthRadiusFactor == null ? 1f : strengthRadiusFactor.Evaluate(ritualStrength));
        }

        public float EffectFactorFor(float ritualStrength)
        {
            return strengthEffectFactor == null ? 1f : strengthEffectFactor.Evaluate(ritualStrength);
        }

        public float StrikesPerMinuteFor(float ritualStrength)
        {
            return baseStrikesPerMinute * EffectFactorFor(ritualStrength);
        }

        public int ShieldChargesFor(float ritualStrength)
        {
            return UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(shieldCharges * EffectFactorFor(ritualStrength)));
        }

        public int CounterStunTicksFor(float ritualStrength)
        {
            return UnityEngine.Mathf.Max(60, UnityEngine.Mathf.RoundToInt(counterStunTicks * EffectFactorFor(ritualStrength)));
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (tier < 1 || tier > 3)
            {
                yield return defName + " tier must be between 1 and 3.";
            }

            if (family == RitualFamily.Unspecified || !Enum.IsDefined(typeof(RitualFamily), family))
            {
                yield return defName + " must declare a ritual family.";
            }

            if (ringSize < 3 || ringSize > 7 || ringSize % 2 == 0)
            {
                yield return defName + " ringSize must be an odd number of at least 3.";
            }

            if (manaCost < 0f)
            {
                yield return defName + " manaCost must not be negative.";
            }

            if (manaPerStrike < 0f)
            {
                yield return defName + " manaPerStrike must not be negative.";
            }

            if (sustained)
            {
                if (UsesWork)
                {
                    yield return defName + " is sustained and must use durationTicks, not workAmount.";
                }

                if (autoRepeatable || outputProducts != null || chooseRune || chooseTeleportTarget
                    || SeeksRuin || chooseEquipment || requiresDeepResources)
                {
                    yield return defName + " is sustained and cannot combine with production, target-picking or ruin-seeking flags.";
                }
            }

            if (workAmount < 0f)
            {
                yield return defName + " workAmount must not be negative.";
            }

            if (durationTicks < 0)
            {
                yield return defName + " durationTicks must not be negative.";
            }

            if (chooseRune)
            {
                if (workAmount > 0f || durationTicks > 0)
                {
                    yield return defName + " uses runeTiers and must not set top-level workAmount or durationTicks.";
                }

                for (int runeTier = 1; runeTier <= 3; runeTier++)
                {
                    if (runeTiers == null || runeTiers.FindAll(entry => entry != null && entry.tier == runeTier).Count != 1)
                        yield return defName + " runeTiers needs exactly one entry for tier " + runeTier;
                }
                if (runeTiers != null)
                {
                    foreach (RuneAwakeningTier entry in runeTiers)
                    {
                        if (entry == null || entry.tier < 1 || entry.tier > 3
                            || entry.ringSize < 3 || entry.ringSize > 7 || entry.ringSize % 2 == 0
                            || !Enum.IsDefined(typeof(ChalkColor), entry.chalkColor)
                            || entry.manaCost < 0f || entry.workAmount <= 0f)
                            yield return defName + " has an invalid runeTiers entry.";
                    }
                }
            }
            else if ((workAmount > 0f) == (durationTicks > 0))
            {
                yield return defName + " must set either workAmount or durationTicks, not both.";
            }

            if (UsesWork)
            {
                if (strengthSpeedFactor == null || strengthSpeedFactor.PointsCount == 0 || strengthSpeedFactor.MinY <= 0f)
                {
                    yield return defName + " uses workAmount and must declare a positive strengthSpeedFactor.";
                }
            }
            else if (strengthSpeedFactor != null)
            {
                yield return defName + " uses fixed durationTicks and must not declare strengthSpeedFactor.";
            }

            if (effectRadius <= 0f)
            {
                yield return defName + " effectRadius must be positive.";
            }

            if (strengthRadiusFactor != null && (strengthRadiusFactor.PointsCount == 0 || strengthRadiusFactor.MinY <= 0f
                || effectRadius * strengthRadiusFactor.MaxY > GenRadial.MaxRadialPatternRadius))
            {
                yield return defName + " strengthRadiusFactor must be positive and stay inside GenRadial's radius limit.";
            }

            if (strengthEffectFactor != null && (strengthEffectFactor.PointsCount == 0 || strengthEffectFactor.MinY <= 0f))
            {
                yield return defName + " strengthEffectFactor must be positive.";
            }

            if (tameChancePerStrength < 0f) yield return defName + " tameChancePerStrength must not be negative.";

            if (minRitualStrength < 0f) yield return defName + " minRitualStrength must not be negative.";

            if (minParticipants < 0)
            {
                yield return defName + " minParticipants must not be negative.";
            }

            if (maxResourceTypes < 1)
            {
                yield return defName + " maxResourceTypes must be at least 1.";
            }

            if (requiresDeepResources
                && (effectWorkerClass == null || !typeof(RitualEffect_LeylineMining).IsAssignableFrom(effectWorkerClass)))
            {
                yield return defName + " requiresDeepResources but does not use RitualEffect_LeylineMining.";
            }

            if (chooseTeleportTarget
                && (effectWorkerClass == null || !typeof(RitualEffect_Teleport).IsAssignableFrom(effectWorkerClass)))
            {
                yield return defName + " chooseTeleportTarget but does not use RitualEffect_Teleport.";
            }

            if (effectWorkerClass != null
                && typeof(RitualEffect_SeekRuin).IsAssignableFrom(effectWorkerClass))
            {
                if (ruinTarget == null)
                {
                    yield return defName + " uses RitualEffect_SeekRuin and must declare ruinTarget.";
                }
                else if (ruinTarget.quest == null || ruinTarget.sitePart == null || ruinTarget.threatPointsFactor <= 0f)
                {
                    yield return defName + " ruinTarget requires quest, sitePart and a positive threatPointsFactor.";
                }
            }
            else if (ruinTarget != null)
            {
                yield return defName + " sets ruinTarget without using RitualEffect_SeekRuin.";
            }

            if (chooseEquipment
                && (effectWorkerClass == null || !typeof(RitualEffect_InscribeEquipment).IsAssignableFrom(effectWorkerClass)))
            {
                yield return defName + " chooseEquipment but does not use RitualEffect_InscribeEquipment.";
            }

            if (autoRepeatable
                && (effectWorkerClass == null || !typeof(RitualEffect_Transmute).IsAssignableFrom(effectWorkerClass)))
            {
                yield return defName + " autoRepeatable is only supported for transmutation rites.";
            }

            if (effectWorkerClass != null
                && typeof(RitualEffect_BeastCall).IsAssignableFrom(effectWorkerClass))
            {
                if (summonedAnimal == null)
                {
                    yield return defName + " uses RitualEffect_BeastCall but has no summonedAnimal.";
                }
                else if (summonedAnimal.race == null || summonedAnimal.race.race == null || !summonedAnimal.race.race.Animal)
                {
                    yield return defName + " summonedAnimal must be an animal.";
                }

                if (tameChanceBase <= 0f || tameChanceBase > 1f)
                {
                    yield return defName + " tameChanceBase must be in (0, 1].";
                }

                if (tameChancePerSkill < 0f)
                {
                    yield return defName + " tameChancePerSkill must not be negative.";
                }

                if (tameWildnessFactor < 0f || tameWildnessFactor > 1f)
                {
                    yield return defName + " tameWildnessFactor must be in [0, 1].";
                }
            }

            if (manaRestore < 0f)
            {
                yield return defName + " manaRestore must not be negative.";
            }

            if (inputCosts != null)
            {
                foreach (ThingDefCountClass cost in inputCosts)
                {
                    if (cost == null || cost.thingDef == null)
                    {
                        yield return defName + " has an inputCosts entry without a thingDef.";
                    }
                    else if (cost.count <= 0)
                    {
                        yield return defName + " inputCosts for " + cost.thingDef.defName + " must be positive.";
                    }
                }
            }

            if (outputProducts != null)
            {
                foreach (ThingDefCountClass product in outputProducts)
                {
                    if (product == null || product.thingDef == null)
                    {
                        yield return defName + " has an outputProducts entry without a thingDef.";
                    }
                    else if (product.count <= 0)
                    {
                        yield return defName + " outputProducts for " + product.thingDef.defName + " must be positive.";
                    }
                }
            }

            if (effectWorkerClass != null
                && typeof(RitualEffect_Transmute).IsAssignableFrom(effectWorkerClass)
                && (outputProducts == null || outputProducts.Count == 0))
            {
                yield return defName + " uses RitualEffect_Transmute but has no outputProducts.";
            }

            if (effectWorkerClass == typeof(RitualEffect_SpellbookUpgrade))
            {
                if (outputProducts == null || outputProducts.Count != 1
                    || outputProducts[0]?.thingDef?.GetModExtension<SpellbookExtension>() == null
                    || outputProducts[0].count != 1)
                {
                    yield return defName + " spellbook upgrade must produce exactly one spellbook.";
                }
                else
                {
                    var books = inputCosts?.FindAll(cost =>
                        cost?.thingDef?.GetModExtension<SpellbookExtension>() != null);
                    if (books == null || books.Count != 1 || books[0].count != 1
                        || books[0].thingDef.GetModExtension<SpellbookExtension>().maxGlyphs
                            >= outputProducts[0].thingDef.GetModExtension<SpellbookExtension>().maxGlyphs)
                        yield return defName + " spellbook upgrade must consume exactly one lower-capacity spellbook.";
                }
            }

            if (effectWorkerClass != null
                && typeof(RitualEffect_Weather).IsAssignableFrom(effectWorkerClass)
                && weatherDef == null)
            {
                yield return defName + " uses RitualEffect_Weather but has no weatherDef.";
            }

            if (effectWorkerClass != null
                && typeof(RitualEffect_SkyWrath).IsAssignableFrom(effectWorkerClass))
            {
                if (!sustained)
                {
                    yield return defName + " uses RitualEffect_SkyWrath and must set sustained=true.";
                }

                if (manaPerStrike <= 0f)
                {
                    yield return defName + " uses RitualEffect_SkyWrath and needs a positive manaPerStrike.";
                }

                if (baseStrikesPerMinute <= 0f)
                {
                    yield return defName + " uses RitualEffect_SkyWrath and needs a positive baseStrikesPerMinute.";
                }

                if (minStrikeRadius < 0f)
                {
                    yield return defName + " minStrikeRadius must not be negative.";
                }

                if (strikeDamage <= 0)
                {
                    yield return defName + " uses RitualEffect_SkyWrath and needs a positive strikeDamage.";
                }

                if (strikeArmorPenetration < 0f)
                {
                    yield return defName + " strikeArmorPenetration must not be negative.";
                }

                if (strikeRadius <= 0f)
                {
                    yield return defName + " uses RitualEffect_SkyWrath and needs a positive strikeRadius.";
                }

                if (strikeIntervalJitter < 0f || strikeIntervalJitter >= 1f)
                {
                    yield return defName + " strikeIntervalJitter must be in [0, 1).";
                }

                if (idleScanIntervalTicks <= 0)
                {
                    yield return defName + " uses RitualEffect_SkyWrath and needs a positive idleScanIntervalTicks.";
                }
            }
            else if (manaPerStrike != 0f || baseStrikesPerMinute != 0f || minStrikeRadius != 0f || strikeDamage != 0
                || strikeArmorPenetration != 0f || strikeRadius != 0f || strikeIntervalJitter != 0f
                || idleScanIntervalTicks != 0)
            {
                yield return defName + " sets sky-wrath strike fields without using RitualEffect_SkyWrath.";
            }

            if (effectWorkerClass != null
                && typeof(RitualEffect_Healing).IsAssignableFrom(effectWorkerClass))
            {
                if (healingTable == null)
                {
                    yield return defName + " uses RitualEffect_Healing and must declare healingTable.";
                }

                if (healingPoolFactor <= 0f)
                {
                    yield return defName + " uses RitualEffect_Healing and needs a positive healingPoolFactor.";
                }
            }
            else if (healingTable != null || healingPoolFactor != 0f)
            {
                yield return defName + " sets healing pool fields without using RitualEffect_Healing.";
            }

            if (effectWorkerClass != null
                && typeof(RitualEffect_ShieldDome).IsAssignableFrom(effectWorkerClass))
            {
                if (shieldCharges <= 0)
                {
                    yield return defName + " uses RitualEffect_ShieldDome and needs a positive shieldCharges.";
                }

                if (shieldDurationTicks <= 0)
                {
                    yield return defName + " uses RitualEffect_ShieldDome and needs a positive shieldDurationTicks.";
                }

                if (shieldVfxDef.NullOrEmpty())
                {
                    yield return defName + " uses RitualEffect_ShieldDome and must name its shieldVfxDef.";
                }
            }
            else if (shieldCharges != 0 || shieldDurationTicks != 0 || !shieldVfxDef.NullOrEmpty())
            {
                yield return defName + " sets shield fields without using RitualEffect_ShieldDome.";
            }

            if (effectWorkerClass != null
                && typeof(RitualEffect_CounterPsychic).IsAssignableFrom(effectWorkerClass))
            {
                if (counterStunTicks <= 0)
                {
                    yield return defName + " uses RitualEffect_CounterPsychic and needs a positive counterStunTicks.";
                }

                if (strengthEffectFactor == null || strengthEffectFactor.PointsCount == 0 || strengthEffectFactor.MinY <= 0f)
                {
                    yield return defName + " uses RitualEffect_CounterPsychic and must declare a positive strengthEffectFactor.";
                }
            }
            else if (counterStunTicks != 0)
            {
                yield return defName + " sets counterStunTicks without using RitualEffect_CounterPsychic.";
            }

            if (effectWorkerClass == null)
            {
                yield return defName + " has no effectWorkerClass.";
            }
            else if (!typeof(RitualEffectWorker).IsAssignableFrom(effectWorkerClass))
            {
                yield return defName + " effectWorkerClass must derive from RitualEffectWorker.";
            }

            if (researchPrerequisite == null)
            {
                yield return defName + " must declare researchPrerequisite.";
            }
        }
    }
}
