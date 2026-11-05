using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Experimental.Vfx
{
    /// <summary>
    /// Per-map owner of the running visual effects. It drives them every frame on a game-tick clock,
    /// lets them draw during the GUI pass (which runs after the map and after RimUnity's screen effects),
    /// and drops all of them when the map goes away.
    ///
    /// Nothing here is scribed: a visual effect is rebuilt from gameplay state, never restored.
    /// </summary>
    public class MapComponent_Vfx : MapComponent
    {
        private readonly List<VfxWorker> workers = new List<VfxWorker>();

        /// <summary>Last game tick this component advanced its effects on; -1 before the first frame.</summary>
        private int lastTick = -1;

        public MapComponent_Vfx(Map map) : base(map)
        {
        }

        /// <summary>The component for this map, created and attached on demand.</summary>
        public static MapComponent_Vfx For(Map map)
        {
            MapComponent_Vfx component = map.GetComponent<MapComponent_Vfx>();
            if (component != null)
            {
                return component;
            }

            component = new MapComponent_Vfx(map);
            map.components.Add(component);
            return component;
        }

        public void Add(VfxWorker worker)
        {
            workers.Add(worker);
        }

        /// <summary>
        /// Runs every frame for every map, paused or not (<c>Game.UpdatePlay</c> loops all maps), so
        /// map changes and load rebuilds never wait for a game tick. Effects advance on the game tick
        /// delta only: a paused game passes zero and every timed effect freezes with it.
        /// </summary>
        public override void MapComponentUpdate()
        {
            int tick = Find.TickManager.TicksGame;
            float deltaSeconds = lastTick < 0 ? 0f : Mathf.Max(0, tick - lastTick) / (float)GenTicks.TicksPerRealSecond;
            lastTick = tick;

            for (int i = workers.Count - 1; i >= 0; i--)
            {
                VfxWorker worker = workers[i];

                // A follow effect has no clock of its own: its subject is gone, so it ends with it.
                if (worker.Def.lifetime == VfxLifetime.FollowThing && FollowGone(worker.Request.follow))
                {
                    worker.Stop();
                    workers.RemoveAt(i);
                    continue;
                }

                worker.Update(deltaSeconds);
                if (worker.Finished)
                {
                    worker.Stop();
                    workers.RemoveAt(i);
                }
            }
        }

        public override void MapComponentOnGUI()
        {
            if (workers.Count == 0 || Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            for (int i = 0; i < workers.Count; i++)
            {
                workers[i].DrawOverlay();
            }
        }

        /// <summary>Leaving or removing the map must not leave a clip, a beam or a screen effect behind.</summary>
        public override void MapRemoved()
        {
            base.MapRemoved();
            for (int i = 0; i < workers.Count; i++)
            {
                workers[i].Stop();
            }

            workers.Clear();
        }

        private static bool FollowGone(Thing follow)
        {
            return follow == null || follow.Destroyed || !follow.Spawned;
        }
    }
}
