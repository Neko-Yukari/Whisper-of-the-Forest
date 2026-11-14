namespace WhisperOfTheForest.Vfx
{
    /// <summary>
    /// One running visual effect. <see cref="VfxService"/> creates it, <see cref="MapComponent_Vfx"/>
    /// drives it once per frame and on the GUI pass, and drops it when it reports <see cref="Finished"/>
    /// or, for a <see cref="VfxLifetime.FollowThing"/> Def, when the followed thing is gone.
    ///
    /// A worker owns everything it creates and must release it in <see cref="Release"/>: screen clips,
    /// materials, textures. Nothing here may affect gameplay state.
    /// </summary>
    public abstract class VfxWorker
    {
        public VfxRequest Request { get; private set; }

        public VfxDef Def { get; private set; }

        /// <summary>Set by the worker when it has nothing left to show; the component then drops it.</summary>
        public bool Finished { get; protected set; }

        /// <summary>Called once, after construction and before the first update.</summary>
        public void Start(VfxRequest request, VfxDef def)
        {
            Request = request;
            Def = def;
            OnStart();
        }

        /// <summary>
        /// Called once per frame while the game runs, and also while it is paused. <paramref name="deltaSeconds"/>
        /// is the game-time delta since the previous frame: zero while paused, and it follows game speed.
        /// An effect that needs absolute time reads <c>Find.TickManager.TicksGame</c> instead.
        /// </summary>
        public abstract void Update(float deltaSeconds);

        public virtual void Trigger(string state)
        {
            throw new System.InvalidOperationException(Def.defName + ": worker cannot receive state '" + state + "'");
        }

        /// <summary>
        /// Called during the GUI pass, which runs after the map and after RimUnity's screen effects.
        /// That is where a beam must be drawn if it has to stay bright over a darkening pass.
        /// </summary>
        public virtual void DrawOverlay()
        {
        }

        /// <summary>Releases everything the worker owns. Safe to call more than once.</summary>
        public virtual void FollowLost()
        {
            Stop();
        }

        public virtual void Stop()
        {
            Finished = true;
            Release();
        }

        protected virtual void OnStart()
        {
        }

        protected virtual void Release()
        {
        }
    }
}
