using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    public enum ChalkColor
    {
        White,
        Green,
        Star
    }

    /// <summary>
    /// A ritual is a ring of chalk marks around an altar, a paid mana cost and a timed performance.
    /// The Def owns the numbers; the altar and the job driver only read them.
    /// </summary>
    public class RitualDef : Def
    {
        public int ringSize = 3;
        public ChalkColor chalkColor = ChalkColor.White;
        public float manaCost;
        public int durationTicks = 600;
        public float effectRadius = 12.9f;
        public float manaRestore;

        /// <summary>How much the effect radius grows per extra bloodline participant on the ring.</summary>
        public float participantRadiusPerExtra;

        /// <summary>How many extra participants count toward the radius bonus.</summary>
        public int maxParticipants;

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

        /// <summary>True when the rite asks the altar to pick a teleport node before it starts.</summary>
        public bool chooseTeleportTarget;

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

        /// <summary>The effect radius for a performance with this many extra participants.</summary>
        public float RadiusFor(int participants)
        {
            int counted = participants < maxParticipants ? participants : maxParticipants;
            return effectRadius * (1f + participantRadiusPerExtra * counted);
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

            if (ringSize < 3 || ringSize % 2 == 0)
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

            if (effectRadius <= 0f)
            {
                yield return defName + " effectRadius must be positive.";
            }

            if (participantRadiusPerExtra < 0f)
            {
                yield return defName + " participantRadiusPerExtra must not be negative.";
            }

            if (maxParticipants < 0)
            {
                yield return defName + " maxParticipants must not be negative.";
            }

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
