using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WhisperOfTheForest.AncientRuins
{
    /// <summary>
    /// One ruin site's forced-exit clock. The deadline belongs to the site, not to the quest: the quest
    /// ends as soon as the guards are dead (Quest.cs:252-263 stops ticking ended quests), which would
    /// otherwise let the player stay in the ruin map forever. It is an absolute deadline measured from
    /// the first moment the site's map existed, so leaving and coming back to that same map neither
    /// pauses nor resets it. The clock remembers which map it belongs to; a site that no longer has a
    /// map has nothing counting, and the record of a destroyed site is dropped.
    /// </summary>
    public sealed class RuinDeadline : IExposable
    {
        public MapParent site;

        public int durationTicks;

        private bool started;

        private int startTick;

        private int mapId = -1;

        private bool exited;

        public bool Exited => exited;

        public bool Started => started;

        public int StartTick => startTick;

        /// <summary>True while the running clock belongs to this very map.</summary>
        public bool IsFor(int forMapId)
        {
            return started && mapId == forMapId;
        }

        /// <summary>Starts a fresh clock for a newly generated map; only the component calls this.</summary>
        public void Start(int tick, int forMapId)
        {
            started = true;
            startTick = tick;
            mapId = forMapId;
            exited = false;
        }

        /// <summary>The site has no map: nothing is counting.</summary>
        public void Reset()
        {
            started = false;
            startTick = 0;
            mapId = -1;
            exited = false;
        }

        public void MarkExited()
        {
            exited = true;
        }

        public int TicksLeft
        {
            get
            {
                if (!started || exited)
                {
                    return 0;
                }

                return durationTicks - (Find.TickManager.TicksGame - startTick);
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref site, "site");
            Scribe_Values.Look(ref durationTicks, "durationTicks");
            Scribe_Values.Look(ref started, "started");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref exited, "exited");
        }
    }

    /// <summary>
    /// The one owner of ruin exit deadlines. It ticks with the game rather than with a quest, so the
    /// 11-day cap holds whether the quest is ongoing, ended or cleaned up. The clock starts the first
    /// tick a site map exists and forces the caravan out when it runs out. A site whose map is gone
    /// counts nothing, and the record of a destroyed site is dropped on the next tick.
    ///
    /// A fresh map for the same site is not a path the shipped Defs produce: a ruin site with no player
    /// pawns left and no active threat is removed together with its map (Site.ShouldRemoveMapNow sets
    /// alsoRemoveWorldObject, Site.cs:403; MapParent.CheckRemoveMapNow destroys the world object,
    /// MapParent.cs:321-323), so leaving for good ends that ruin instead of parking it for a later
    /// visit. The map id is kept so a clock can never be mistaken for one belonging to another map; it
    /// does not hand out a second 11 days.
    /// </summary>
    public sealed class GameComponent_RuinDeadlines : GameComponent
    {
        private static GameComponent_RuinDeadlines current;

        private List<RuinDeadline> deadlines = new List<RuinDeadline>();

        public GameComponent_RuinDeadlines(Game game)
        {
            current = this;
        }

        /// <summary>The live component, or null before a game is loaded.</summary>
        public static GameComponent_RuinDeadlines Current
        {
            get
            {
                if (current == null)
                {
                    current = Verse.Current.Game?.GetComponent<GameComponent_RuinDeadlines>();
                }

                return current;
            }
        }

        public RuinDeadline For(MapParent site)
        {
            if (site == null || deadlines == null)
            {
                return null;
            }

            for (int i = 0; i < deadlines.Count; i++)
            {
                if (deadlines[i] != null && deadlines[i].site == site)
                {
                    return deadlines[i];
                }
            }

            return null;
        }

        /// <summary>Arms the clock of one ruin. Called once when the quest places the site.</summary>
        public void Track(MapParent site, int durationTicks)
        {
            if (site == null || durationTicks <= 0 || For(site) != null)
            {
                return;
            }

            if (deadlines == null)
            {
                deadlines = new List<RuinDeadline>();
            }

            deadlines.Add(new RuinDeadline { site = site, durationTicks = durationTicks });
        }

        public override void GameComponentTick()
        {
            if (deadlines == null || deadlines.Count == 0)
            {
                return;
            }

            for (int i = deadlines.Count - 1; i >= 0; i--)
            {
                RuinDeadline deadline = deadlines[i];
                if (deadline == null || deadline.site == null || deadline.site.Destroyed)
                {
                    deadlines.RemoveAt(i);
                    continue;
                }

                if (!deadline.site.HasMap)
                {
                    deadline.Reset();
                    continue;
                }

                int mapId = deadline.site.Map.uniqueID;
                if (!deadline.IsFor(mapId))
                {
                    deadline.Start(Find.TickManager.TicksGame, mapId);
                    WotfLog.Info(WotfLog.ModuleRuin, "exit clock started for " + deadline.site.Label
                        + " on map " + mapId + ": " + deadline.durationTicks + " ticks");
                }

                if (deadline.Exited || deadline.TicksLeft > 0)
                {
                    continue;
                }

                deadline.MarkExited();
                WotfLog.Info(WotfLog.ModuleRuin, "exit deadline reached at " + deadline.site.Label
                    + " on map " + mapId + ", forcing the caravan out");
#pragma warning disable CS0612, CS0618 // 1.7 marked TimedForcedExit obsolete but never replaced ForceReform; it is still the vanilla exit routine.
                TimedForcedExit.ForceReform(deadline.site);
#pragma warning restore CS0612, CS0618
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref deadlines, "ruinDeadlines", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (deadlines == null)
                {
                    deadlines = new List<RuinDeadline>();
                }

                deadlines.RemoveAll(deadline => deadline == null || deadline.site == null);
            }
        }
    }
}
