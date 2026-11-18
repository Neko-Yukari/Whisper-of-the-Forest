using System;
using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>How long a running effect lives; declared once per Def, not per request.</summary>
    public enum VfxLifetime
    {
        /// <summary>
        /// The worker reports <see cref="VfxWorker.Finished"/> itself, on its own game-tick clock.
        /// This is the zero value, so an effect that does not trail a thing need not state it.
        /// </summary>
        FixedDuration,

        /// <summary>
        /// The effect presents <see cref="VfxRequest.follow"/>; the map component drops it as soon as
        /// that thing is destroyed or despawned. A request without a follow is a caller bug.
        /// </summary>
        FollowThing
    }

    /// <summary>
    /// A visual effect definition: it chooses the worker that presents the request. Every look
    /// parameter belongs to a derived Def (see <see cref="RailgunLumenVfxDef"/> and the missile visuals), so
    /// the spell layer cannot reach any of them.
    /// </summary>
    public class VfxDef : Def
    {
        /// <summary>Concrete <see cref="VfxWorker"/> with a parameterless constructor.</summary>
        public Type workerClass;

        /// <summary>Lifecycle of the effect this Def describes.</summary>
        public VfxLifetime lifetime;

        public virtual bool SupportsState(string state) => false;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (workerClass == null)
            {
                yield return "a VfxDef needs a workerClass";
                yield break;
            }

            if (!typeof(VfxWorker).IsAssignableFrom(workerClass))
            {
                yield return "workerClass " + workerClass.FullName + " does not derive from VfxWorker";
                yield break;
            }

            if (workerClass.IsAbstract || workerClass.GetConstructor(Type.EmptyTypes) == null)
            {
                yield return "workerClass " + workerClass.FullName + " needs to be concrete with a parameterless constructor";
            }
        }
    }
}
