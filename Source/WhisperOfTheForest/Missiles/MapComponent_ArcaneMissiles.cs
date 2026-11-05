using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Missiles
{
    /// <summary>
    /// Map-scoped adapter for the arcane missiles. It owns no missile list and no visual: every
    /// missile is a spawned <see cref="Projectile_ArcaneMissiles"/> that the game registers, ticks,
    /// saves and destroys, and its look belongs to the Vfx layer. What remains here is map-wide
    /// bookkeeping - the live cap, the lab counters and the lab snapshots - all read from the game's
    /// own registry (<see cref="ListerThings.ThingsOfDef"/>), never from a mod-side simulation list.
    /// </summary>
    public class MapComponent_ArcaneMissiles : MapComponent
    {
        /// <summary>Live cap for one map; the launcher checks it before spawning another missile.</summary>
        public const int MaxLiveMissiles = 96;

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
        internal void NotifyLaunched()
        {
            totalLaunched++;
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
        /// The game's registry for this map, filtered to our projectile def. Read-only use: the
        /// returned list belongs to <see cref="ListerThings"/> and must not be modified.
        /// </summary>
        private List<Thing> LiveMissiles()
        {
            ThingDef def = ArcaneMissileDefs.ProjectileThingDef;
            return def == null || map?.listerThings == null ? null : map.listerThings.ThingsOfDef(def);
        }
    }
}
