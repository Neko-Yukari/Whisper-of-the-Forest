using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// The target categories the spell system names itself. Vanilla's targeting switches cannot
    /// express them (a factionless mech counts as an animal there, wild men and entities fall under
    /// the animal switch), so they are only a coarse pre-filter and this classification decides.
    /// </summary>
    public enum SpellTargetClass
    {
        Humanlike,
        Animal,
        Mechanoid,
        Entity,
        Building,
        Rock,
        Plant,
        Item,
        Cell
    }

    /// <summary>The one owner of the category rules and of what a frozen whitelist accepts.</summary>
    public static class SpellTargeting
    {
        /// <summary>
        /// Category of a real thing. Pawns are classified by race, never by faction; wild men count
        /// as humanlike. Natural rock is its own category so a spell can hit ore without hitting
        /// built walls; everything that is not a pawn, plant or building is an item (chunks included).
        /// </summary>
        public static SpellTargetClass ClassOf(Thing thing)
        {
            if (thing is Pawn pawn)
            {
                if (pawn.RaceProps.IsAnomalyEntity)
                {
                    return SpellTargetClass.Entity;
                }

                if (pawn.RaceProps.IsMechanoid)
                {
                    return SpellTargetClass.Mechanoid;
                }

                return pawn.RaceProps.Animal ? SpellTargetClass.Animal : SpellTargetClass.Humanlike;
            }

            if (thing.def.category == ThingCategory.Plant)
            {
                return SpellTargetClass.Plant;
            }

            if (thing.def.category == ThingCategory.Building)
            {
                return thing.def.building != null && thing.def.building.isNaturalRock
                    ? SpellTargetClass.Rock
                    : SpellTargetClass.Building;
            }

            return SpellTargetClass.Item;
        }

        /// <summary>
        /// True when the plan's frozen whitelist accepts this pick. Self and Orbit need no selection,
        /// so they accept anything (their callers already run on the caster).
        /// </summary>
        public static bool Accepts(SpellCastPlan plan, TargetInfo pick)
        {
            if (plan.form == SpellFormKind.Self || plan.form == SpellFormKind.Orbit)
            {
                return true;
            }

            return pick.HasThing
                ? plan.targets.Contains(ClassOf(pick.Thing))
                : plan.targets.Contains(SpellTargetClass.Cell);
        }

        /// <summary>The same test for a turret's candidate thing.</summary>
        public static bool AcceptsThing(SpellCastPlan plan, Thing thing)
        {
            return plan.form == SpellFormKind.Self || plan.form == SpellFormKind.Orbit
                   || plan.targets.Contains(ClassOf(thing));
        }

        /// <summary>Adds a category that is not listed yet.</summary>
        public static void Add(List<SpellTargetClass> into, SpellTargetClass value)
        {
            if (!into.Contains(value))
            {
                into.Add(value);
            }
        }

        /// <summary>Adds every category in <paramref name="from"/> that is not listed yet.</summary>
        public static void AddAll(List<SpellTargetClass> into, List<SpellTargetClass> from)
        {
            if (from == null)
            {
                return;
            }

            for (int i = 0; i < from.Count; i++)
            {
                if (!into.Contains(from[i]))
                {
                    into.Add(from[i]);
                }
            }
        }
    }
}
