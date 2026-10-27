using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Experimental.Missiles
{
    /// <summary>
    /// Map-scoped adapter for the arcane missiles. It no longer owns a missile list and no longer
    /// advances gameplay: every missile is a spawned <see cref="Projectile_ArcaneMissiles"/> that the
    /// game registers, ticks, saves and destroys. What remains here is map-wide presentation and
    /// observation - the current-map visibility gate for the visual backends, the lab counters and
    /// the lab snapshots - all read from the game's own registry
    /// (<see cref="ListerThings.ThingsOfDef"/>), never from a mod-side simulation list.
    /// </summary>
    public class MapComponent_ArcaneMissiles : MapComponent
    {
        /// <summary>Live cap for one map; the launcher checks it before spawning another missile.</summary>
        public const int MaxLiveMissiles = 96;

        private bool rebuildVisuals = true;
        private bool visualsSuspended;

        // Lab observation only. Not scribed: counters describe this component instance since the
        // map was loaded, which is what an in-game acceptance run needs.
        private int totalLaunched;
        private int totalImpacts;
        private int totalExpired;

        public MapComponent_ArcaneMissiles(Map map) : base(map)
        {
        }

        public int LiveMissileCount => LiveMissiles()?.Count ?? 0;

        public int TotalLaunched => totalLaunched;

        public int TotalImpacts => totalImpacts;

        public int TotalExpired => totalExpired;

        public void ResetLabCounters()
        {
            totalLaunched = 0;
            totalImpacts = 0;
            totalExpired = 0;
        }

        /// <summary>
        /// Live snapshots for the lab. Settings are copies; no missile or visual state is exposed.
        /// </summary>
        public IReadOnlyList<ArcaneMissileLabFrame> GetLabFrames()
        {
            List<Thing> live = LiveMissiles();
            var frames = new List<ArcaneMissileLabFrame>(live?.Count ?? 0);
            if (live == null)
            {
                return frames;
            }

            for (int i = 0; i < live.Count; i++)
            {
                if (live[i] is Projectile_ArcaneMissiles missile && missile.State?.settings != null)
                {
                    frames.Add(missile.ToLabFrame());
                }
            }

            return frames;
        }

        public static MapComponent_ArcaneMissiles GetOrCreate(Map map)
        {
            MapComponent_ArcaneMissiles component = map.GetComponent<MapComponent_ArcaneMissiles>();
            if (component == null)
            {
                // Maps construct every MapComponent subclass themselves; this only covers a map
                // created before that ran, so the launcher never works without an adapter.
                component = new MapComponent_ArcaneMissiles(map);
                map.components.Add(component);
            }

            return component;
        }

        /// <summary>False once the map holds the live cap; the launcher then drops the rest.</summary>
        internal bool CanAcceptMore()
        {
            return LiveMissileCount < MaxLiveMissiles;
        }

        /// <summary>Called by the projectile-bearing launch path once a missile is really spawned.</summary>
        internal void NotifyLaunched(Projectile_ArcaneMissiles missile)
        {
            totalLaunched++;

            // A volley can be cast on a map that is not on screen (other colony, caravan map);
            // such a missile must not spawn a world-space effect until its map is current.
            if (visualsSuspended || Find.CurrentMap != map)
            {
                missile?.SuspendVisual();
            }
        }

        internal void NotifyImpact()
        {
            totalImpacts++;
        }

        internal void NotifyExpired()
        {
            totalExpired++;
        }

        /// <summary>
        /// Runs every frame for every map - `Game.UpdatePlay` loops all maps and `Root_Play.Update`
        /// calls it even while paused - so visibility, load rebuild and burst cleanup never wait for
        /// a game tick.
        /// </summary>
        public override void MapComponentUpdate()
        {
            bool current = Find.CurrentMap == map;

            if (!current)
            {
                if (!visualsSuspended)
                {
                    visualsSuspended = true;
                    SuspendVisuals();
                    ArcaneMissileVisualSource.NotifyMapHidden(map);
                }

                return;
            }

            if (visualsSuspended)
            {
                visualsSuspended = false;
                rebuildVisuals = true;
            }

            if (rebuildVisuals)
            {
                rebuildVisuals = false;
                RebuildVisuals();
            }

            ArcaneMissileVisualSource.NotifyMapFrame(map);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            // Rebuild on the next frame update, not on a tick: a paused game must still show
            // restored missiles immediately after loading.
            rebuildVisuals = true;
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            SuspendVisuals();
            ArcaneMissileVisualSource.NotifyMapHidden(map);
        }

        /// <summary>
        /// The game's registry for this map, filtered to our projectile def. Read-only use: the
        /// returned list belongs to <see cref="ListerThings"/> and must not be modified.
        /// </summary>
        private List<Thing> LiveMissiles()
        {
            ThingDef def = ArcaneMissileDefs.ProjectileThingDef;
            return def == null || map?.listerThings == null ? null : map.listerThings.ThingsOfDef(def);
        }

        private void SuspendVisuals()
        {
            List<Thing> live = LiveMissiles();
            if (live == null)
            {
                return;
            }

            for (int i = 0; i < live.Count; i++)
            {
                if (live[i] is Projectile_ArcaneMissiles missile)
                {
                    missile.SuspendVisual();
                }
            }
        }

        private void RebuildVisuals()
        {
            List<Thing> live = LiveMissiles();
            if (live == null)
            {
                return;
            }

            for (int i = 0; i < live.Count; i++)
            {
                if (live[i] is Projectile_ArcaneMissiles missile)
                {
                    missile.ResumeVisual();
                }
            }
        }
    }
}
