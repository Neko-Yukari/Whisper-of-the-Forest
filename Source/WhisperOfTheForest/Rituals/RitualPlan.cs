using System.Collections.Generic;
using Verse;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>Where one performance stands: still gathering on the chalk, or chanting.</summary>
    public enum RitualPhase
    {
        Gathering,
        Chanting
    }

    /// <summary>
    /// A concrete stack the altar has set aside for one cost line. The reservation lives on the host's
    /// job; this object is what completion re-checks before anything is taken.
    /// </summary>
    public sealed class RitualOffering : IExposable
    {
        public Thing Thing;
        public int Count;

        public void ExposeData()
        {
            Scribe_References.Look(ref Thing, "thing", true);
            Scribe_Values.Look(ref Count, "count", 0);
        }
    }

    /// <summary>One participant and the chalk cell they stand on for the whole performance.</summary>
    public sealed class RitualStation : IExposable
    {
        public Pawn Pawn;
        public IntVec3 Cell;

        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn", true);
            Scribe_Values.Look(ref Cell, "cell");
        }
    }

    /// <summary>
    /// Everything one [主持仪式…] press decides. The window fills it, the altar validates it and then
    /// performs it, so the panel and the start rule read the same numbers. A later step that needs
    /// more input (equipment inscription) adds its fields here instead of opening a second start path.
    /// </summary>
    public sealed class RitualRequest
    {
        public RitualDef Def;
        public SpellGlyphDef Rune;
        public Building TeleportTarget;

        /// <summary>The weapon or armour the rite writes into; only equipment inscription rituals use it.</summary>
        public Thing Equipment;

        /// <summary>The host's spellbook slot that is written; -1 when nobody has chosen one.</summary>
        public int BookSlot = -1;

        /// <summary>The ruin a seeking rite points at; only ruin-seeking rituals use it.</summary>
        public RuinSeekOption RuinOption;

        public Pawn Host;
        public readonly List<Pawn> Helpers = new List<Pawn>();
    }

    /// <summary>
    /// The answer of one validation pass: every number the panel shows plus the blocking reasons in
    /// the order the player should fix them. Missing cells and offerings are kept separately so the
    /// panel can point at them on the map.
    /// </summary>
    public sealed class RitualAssessment
    {
        public RitualRequest Request;
        public int RingSize;
        public ChalkColor ChalkColor;
        public float ManaCost;
        public float ManaStored;
        public bool ManaEnough;
        public int DurationTicks;
        public float Power;
        public float BloodlinePower;
        public float FacilityPower;
        public bool RingComplete;
        public readonly List<IntVec3> MissingCells = new List<IntVec3>();
        public readonly List<string> MissingOfferings = new List<string>();
        public readonly List<string> Problems = new List<string>();
        public readonly List<RitualOffering> Offerings = new List<RitualOffering>();
        public readonly List<RitualStation> Stations = new List<RitualStation>();

        public bool CanStart => Problems.Count == 0;

        public int Participants => Request == null ? 0 : Request.Helpers.Count;

        /// <summary>The first thing to fix, for the one-line rejection a start press shows.</summary>
        public string FirstProblem => Problems.Count == 0 ? null : Problems[0];
    }
}
