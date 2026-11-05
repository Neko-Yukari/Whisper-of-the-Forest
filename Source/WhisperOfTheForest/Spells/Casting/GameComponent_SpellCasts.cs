using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Experimental.Spells
{
    /// <summary>
    /// Owns every cast in progress for the whole game and advances it on game ticks. This is the only
    /// owner of that state: a pawn can run one cast at a time and nothing else keeps a second copy,
    /// so the book, the gizmo, the staff and a loaded save all see the same phase.
    ///
    /// Kept apart from the visual layer on purpose: the payload resolves here on game ticks whatever
    /// the screen effect is doing.
    /// </summary>
    public class GameComponent_SpellCasts : GameComponent
    {
        private static GameComponent_SpellCasts current;

        private List<SpellCast> casts = new List<SpellCast>();

        public GameComponent_SpellCasts(Game game)
        {
            current = this;
        }

        /// <summary>The live component, or null before a game is loaded.</summary>
        public static GameComponent_SpellCasts Current
        {
            get
            {
                if (current == null)
                {
                    current = Verse.Current.Game?.GetComponent<GameComponent_SpellCasts>();
                }

                return current;
            }
        }

        /// <summary>Casts in progress, exposed read-only for diagnostics.</summary>
        public int Count => casts == null ? 0 : casts.Count;

        public void Add(SpellCast cast)
        {
            if (cast == null)
            {
                return;
            }

            if (casts == null)
            {
                casts = new List<SpellCast>();
            }

            casts.Add(cast);
        }

        public void Remove(SpellCast cast)
        {
            if (cast != null && casts != null)
            {
                casts.Remove(cast);
            }
        }

        /// <summary>The pawn's cast in progress, or null. One cast per pawn, so the first match is it.</summary>
        public SpellCast FindFor(Pawn caster)
        {
            if (casts == null || caster == null)
            {
                return null;
            }

            for (int i = 0; i < casts.Count; i++)
            {
                SpellCast cast = casts[i];
                if (cast != null && cast.caster == caster)
                {
                    return cast;
                }
            }

            return null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref casts, "spellCasts", LookMode.Deep);

            // Scribing only clears the list while loading; a null list would drop every later cast.
            if (casts == null)
            {
                casts = new List<SpellCast>();
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
            base.GameComponentTick();
            if (casts == null || casts.Count == 0)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                SpellCast cast = casts[i];
                if (cast == null || SpellCastingService.Advance(cast, now))
                {
                    casts.RemoveAt(i);
                }
            }
        }
    }
}
