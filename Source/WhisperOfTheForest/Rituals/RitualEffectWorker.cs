using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Mana;
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

        public virtual int InitialSustainedDelayTicks(RitualDef def, float ritualStrength)
        {
            return 0;
        }

        public virtual int SustainedTick(
            Building altar,
            Pawn host,
            RitualDef def,
            float ritualStrength,
            ManaNetwork network,
            out bool manaDepleted)
        {
            manaDepleted = false;
            return 0;
        }

        public virtual void OnSustainedEnd(Building altar, RitualDef def)
        {
        }
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

        /// <summary>The weapon or armour the rite writes into; null for every other ritual.</summary>
        public readonly Thing Equipment;

        /// <summary>The host's spellbook slot that was written; -1 for every other ritual.</summary>
        public readonly int BookSlot;

        /// <summary>
        /// Where each colonist of a teleport rite lands, one cell per pawn. The altar settles it before it
        /// charges anything, so the effect only carries out a plan that already exists; null for every
        /// other ritual.
        /// </summary>
        public readonly Dictionary<Pawn, IntVec3> TeleportLanding;

        /// <summary>Extra bloodline pawns that stood on the ring when the rite started.</summary>
        public readonly int Participants;

        public readonly float RitualStrength;
        public float EffectRadius => Def.RadiusFor(RitualStrength);
        public float EffectFactor => Def.EffectFactorFor(RitualStrength);

        public RitualContext(
            Map map,
            Building altar,
            Pawn caster,
            RitualDef def,
            SpellGlyphDef rune,
            Building teleportTarget = null,
            int participants = 0,
            float ritualStrength = 0f,
            Thing equipment = null,
            int bookSlot = -1,
            Dictionary<Pawn, IntVec3> teleportLanding = null)
        {
            Map = map;
            Altar = altar;
            Caster = caster;
            Def = def;
            Rune = rune;
            TeleportTarget = teleportTarget;
            Participants = participants;
            RitualStrength = ritualStrength;
            Equipment = equipment;
            BookSlot = bookSlot;
            TeleportLanding = teleportLanding;
        }
    }
}
