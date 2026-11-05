using RimWorld;
using Verse;

namespace WhisperOfTheForest.Experimental.Spells
{
    /// <summary>
    /// "Can this thing receive harm/healing at all" rules, in one place so the executor and the
    /// checks agree.
    ///
    /// The pawn rule is not decoration: every pawn ThingDef inherits <c>useHitPoints=false</c> from
    /// the vanilla abstract <c>BasePawn</c> def (Data/Core/Defs/ThingDefs_Races/Races_Animal_Base.xml),
    /// so a predicate that only looked at <c>def.useHitPoints</c> silently refused to damage or heal a
    /// real pawn. Pawns own health instead of def hit points, so they are checked through health.
    /// </summary>
    public static class SpellTargets
    {
        /// <summary>Live pawn (health-based) or a spawned thing that owns hit points.</summary>
        public static bool CanTakeDamage(Thing thing)
        {
            if (thing == null || thing.Destroyed || !thing.Spawned || thing.def == null)
            {
                return false;
            }

            if (thing is Pawn pawn)
            {
                return CanTakeDamage(pawn);
            }

            return thing.def.useHitPoints;
        }

        /// <summary>Same rule for an already known pawn; a dead or health-less pawn is refused.</summary>
        public static bool CanTakeDamage(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && pawn.health != null && !pawn.Destroyed && pawn.Spawned;
        }

        /// <summary>A live pawn with a health tracker is the only thing a Heal effect can treat.</summary>
        public static bool CanBeHealed(Thing thing)
        {
            return thing is Pawn pawn && CanTakeDamage(pawn);
        }
    }
}
