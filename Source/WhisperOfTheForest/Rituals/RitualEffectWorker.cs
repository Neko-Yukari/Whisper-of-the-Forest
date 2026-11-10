using Verse;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// What one finished ritual does. The altar calls this once, after the ring, the mana cost and the
    /// performance have all been accepted; the worker owns only the effect and its own message.
    /// </summary>
    public abstract class RitualEffectWorker
    {
        public abstract void Apply(RitualContext context);
    }

    public sealed class RitualContext
    {
        public readonly Map Map;
        public readonly Building Altar;
        public readonly Pawn Caster;
        public readonly RitualDef Def;

        /// <summary>The rune this performance awakens; null for every ritual that is not a rune awakening.</summary>
        public readonly SpellGlyphDef Rune;

        /// <summary>The teleport node this performance targets; null for every other ritual.</summary>
        public readonly Building TeleportTarget;

        /// <summary>Extra bloodline pawns that stood on the ring when the rite started.</summary>
        public readonly int Participants;

        public float EffectRadius => Def.RadiusFor(Participants);

        public RitualContext(
            Map map,
            Building altar,
            Pawn caster,
            RitualDef def,
            SpellGlyphDef rune,
            Building teleportTarget = null,
            int participants = 0)
        {
            Map = map;
            Altar = altar;
            Caster = caster;
            Def = def;
            Rune = rune;
            TeleportTarget = teleportTarget;
            Participants = participants;
        }
    }
}
