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
        public int durationTicks;
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
        public int durationTicks = 600;
        public float effectRadius = 12.9f;
        public float manaRestore;

        public SimpleCurve powerRadiusFactor;
        public SimpleCurve powerEffectFactor;
        public float tameChancePerPower;

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

        /// <summary>
        /// True when the rite asks the altar which ruin the colony is looking for. The altar shows the
        /// options and passes the chosen one to the effect worker; the Def carries no single ruin.
        /// </summary>
        public bool chooseRuinType;

        /// <summary>The ruins a seeking rite can point at, in the order the altar lists them.</summary>
        public List<RuinSeekOption> ruinOptions;

        public RuinSeekOption RuinOptionFor(QuestScriptDef quest)
        {
            return ruinOptions?.Find(option => option.quest == quest);
        }

        /// <summary>
        /// True when the rite writes one of the host's spells into a weapon or armour lying beside the
        /// altar. The altar asks which item and which book slot and passes both to the effect worker.
        /// </summary>
        public bool chooseEquipment;

        /// <summary>True when the altar may repeat this production rite on its own while idle.</summary>
        public bool autoRepeatable;

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

        public bool UsesPower => powerRadiusFactor != null || powerEffectFactor != null || tameChancePerPower > 0f;

        public float RadiusFor(float power)
        {
            return effectRadius * (powerRadiusFactor == null ? 1f : powerRadiusFactor.Evaluate(power));
        }

        public float EffectFactorFor(float power)
        {
            return powerEffectFactor == null ? 1f : powerEffectFactor.Evaluate(power);
        }

        private static bool AltarHasShieldComp()
        {
            ThingDef altar = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_RitualAltar");
            return altar != null
                && altar.comps != null
                && altar.comps.Exists(comp => comp is CompProperties_ProjectileInterceptor);
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

            if (durationTicks <= 0)
            {
                yield return defName + " durationTicks must be positive.";
            }

            if (chooseRune)
            {
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
                            || entry.manaCost < 0f || entry.durationTicks <= 0)
                            yield return defName + " has an invalid runeTiers entry.";
                    }
                }
            }

            if (effectRadius <= 0f)
            {
                yield return defName + " effectRadius must be positive.";
            }

            if (powerRadiusFactor != null && (powerRadiusFactor.PointsCount == 0 || powerRadiusFactor.MinY <= 0f
                || effectRadius * powerRadiusFactor.MaxY > GenRadial.MaxRadialPatternRadius))
            {
                yield return defName + " powerRadiusFactor must be positive and stay inside GenRadial's radius limit.";
            }

            if (powerEffectFactor != null && (powerEffectFactor.PointsCount == 0 || powerEffectFactor.MinY <= 0f))
            {
                yield return defName + " powerEffectFactor must be positive.";
            }

            if (tameChancePerPower < 0f) yield return defName + " tameChancePerPower must not be negative.";

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

            if (chooseRuinType)
            {
                if (effectWorkerClass == null || !typeof(RitualEffect_SeekRuin).IsAssignableFrom(effectWorkerClass))
                {
                    yield return defName + " chooseRuinType but does not use RitualEffect_SeekRuin.";
                }

                if (ruinOptions == null || ruinOptions.Count == 0)
                {
                    yield return defName + " chooseRuinType but has no ruinOptions.";
                }
                else
                {
                    foreach (RuinSeekOption option in ruinOptions)
                    {
                        if (option == null || option.quest == null || option.sitePart == null || option.threatPointsFactor <= 0f)
                        {
                            yield return defName + " has a ruinOptions entry without quest, sitePart or a positive threatPointsFactor.";
                        }
                    }
                }
            }
            else if (ruinOptions != null)
            {
                yield return defName + " has ruinOptions but does not set chooseRuinType.";
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
                        || books[0].thingDef.GetModExtension<SpellbookExtension>().slotCount
                            >= outputProducts[0].thingDef.GetModExtension<SpellbookExtension>().slotCount)
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
                && typeof(RitualEffect_ShieldDome).IsAssignableFrom(effectWorkerClass)
                && !AltarHasShieldComp())
            {
                yield return defName + " uses RitualEffect_ShieldDome but WotF_RitualAltar has no CompProjectileInterceptor.";
            }

            if (effectWorkerClass == null)
            {
                yield return defName + " has no effectWorkerClass.";
            }
            else if (!typeof(RitualEffectWorker).IsAssignableFrom(effectWorkerClass))
            {
                yield return defName + " effectWorkerClass must derive from RitualEffectWorker.";
            }
        }
    }
}
