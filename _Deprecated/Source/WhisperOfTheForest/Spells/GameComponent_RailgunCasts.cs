using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Experimental.Spells
{
    /// <summary>
    /// One composite cast waiting for its charge to finish. The aim point is locked at cast time and
    /// the numbers are a copy of the compiled plan, so nothing that happens to the book or the Def in
    /// between can change what this cast will do.
    ///
    /// The record is scribed with the game: a save taken during the charge restores it and the shot
    /// still lands, instead of silently disappearing.
    /// </summary>
    public class RailgunCast : IExposable
    {
        public Map map;
        public Pawn caster;

        /// <summary>Locked aim cell; the ray direction is re-derived from it at fire time.</summary>
        public IntVec3 aimCell;

        /// <summary>Game tick at which the damage resolves.</summary>
        public int fireTick;

        public SpellRailgunSpec spec;

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref aimCell, "aimCell");
            Scribe_Values.Look(ref fireTick, "fireTick");
            Scribe_Deep.Look(ref spec, "spec");
        }
    }

    /// <summary>
    /// Owns the charged casts of the whole game and resolves each one on its fire tick. Kept apart
    /// from the visual layer on purpose: the damage is settled here on game ticks, whatever the
    /// screen effect is doing.
    /// </summary>
    public class GameComponent_RailgunCasts : GameComponent
    {
        private static GameComponent_RailgunCasts current;

        private List<RailgunCast> pending = new List<RailgunCast>();

        public GameComponent_RailgunCasts(Game game)
        {
            current = this;
        }

        /// <summary>The live component, or null before a game is loaded.</summary>
        public static GameComponent_RailgunCasts Current
        {
            get
            {
                if (current == null)
                {
                    current = Verse.Current.Game?.GetComponent<GameComponent_RailgunCasts>();
                }

                return current;
            }
        }

        public void Add(RailgunCast cast)
        {
            if (cast == null)
            {
                return;
            }

            if (pending == null)
            {
                pending = new List<RailgunCast>();
            }

            pending.Add(cast);
        }

        /// <summary>Number of casts still charging, exposed read-only for the lab harness.</summary>
        public int PendingCount => pending == null ? 0 : pending.Count;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pending, "railgunCasts", LookMode.Deep);

            // Scribing only clears the list while loading; a null list would drop every later cast.
            if (pending == null)
            {
                pending = new List<RailgunCast>();
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            current = this;
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            current = this;
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            current = this;
        }

        public override void GameComponentTick()
        {
            if (pending == null || pending.Count == 0)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                RailgunCast cast = pending[i];
                if (cast == null)
                {
                    pending.RemoveAt(i);
                    continue;
                }

                if (now < cast.fireTick)
                {
                    continue;
                }

                // Remove before resolving: a target that dies here must never be hit by a second pass
                // of the same cast, and a throw must not leave the shot queued forever.
                pending.RemoveAt(i);
                RailgunCastService.Resolve(cast);
            }
        }
    }
}
