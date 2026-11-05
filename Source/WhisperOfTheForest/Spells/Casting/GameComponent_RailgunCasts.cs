using Verse;

namespace WhisperOfTheForest.Experimental.Spells
{
    /// <summary>
    /// Compatibility shell only, it owns no cast and no timer:
    ///   - saves written before casting was unified name this component, and a type that no longer
    ///     exists logs a missing-class error on every load, so the name has to stay resolvable;
    ///   - the lab's status block still reads <see cref="Current"/>/<see cref="PendingCount"/>; the
    ///     number now comes from <see cref="GameComponent_SpellCasts"/>, i.e. every cast in flight.
    ///
    /// A shot that was pending in an old save is dropped rather than restored. Nothing else should
    /// use this type: <see cref="SpellCastingService"/> is the entry point.
    /// </summary>
    public class GameComponent_RailgunCasts : GameComponent
    {
        private static GameComponent_RailgunCasts current;

        public GameComponent_RailgunCasts(Game game)
        {
        }

        /// <summary>The live shell, or null before a game is loaded.</summary>
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

        /// <summary>Casts in flight right now, read from the one component that owns them.</summary>
        public int PendingCount => GameComponent_SpellCasts.Current?.Count ?? 0;

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
    }
}
