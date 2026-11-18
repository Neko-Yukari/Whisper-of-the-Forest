using Verse;
using WhisperOfTheForest.Vfx;

namespace WhisperOfTheForest.Spells
{
    /// <summary>Where a cast in progress is. Phases run in this order and never repeat.</summary>
    public enum SpellCastPhase
    {
        Windup,
        Channel,
        Recovery
    }

    public enum SpellTargetKind
    {
        Cell,
        Thing
    }

    /// <summary>
    /// One cast in progress. Everything a cast needs after the player picked a target is frozen here:
    /// the compiled plan (numbers, payload, timings), the locked aim cell and the thing that was
    /// picked. The record is scribed with the game, so a save taken in any phase restores the cast and
    /// it still resolves on its own tick instead of disappearing.
    ///
    /// The service owns the transitions; this class only holds data and its save behaviour.
    /// </summary>
    public class SpellCast : IExposable
    {
        public Pawn caster;

        public Map map;

        // Identity survives a missing Thing reference after loading; null never means "picked a cell".
        public SpellTargetKind targetKind;
        public Thing targetThing;

        /// <summary>Aim cell locked at cast time; the caster's live position is the firing point.</summary>
        public IntVec3 cell;

        public SpellCastPlan plan;

        public SpellCastPhase phase;

        /// <summary>
        /// Absolute game tick at which the current phase ends. Absolute on purpose: the tick counter
        /// is scribed too, so a cast restored from a save continues on schedule.
        /// </summary>
        public int phaseEndTick;

        public int startedTick;

        internal VfxHandle castVfx;
        internal VfxHandle specialChannelVfx;

        internal void StopVisuals()
        {
            castVfx?.End();
            castVfx = null;
            specialChannelVfx?.End();
            specialChannelVfx = null;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref targetKind, "targetKind");
            Scribe_References.Look(ref targetThing, "targetThing");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Deep.Look(ref plan, "plan");
            Scribe_Values.Look(ref phase, "phase", SpellCastPhase.Windup);
            Scribe_Values.Look(ref phaseEndTick, "phaseEndTick", 0);
            Scribe_Values.Look(ref startedTick, "startedTick", 0);
        }
    }
}
