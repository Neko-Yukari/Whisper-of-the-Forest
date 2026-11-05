using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>
    /// The whole interface between a spell and the visual system: what happened, where, how wide and
    /// how long the charge is, plus which Vfx Def should present it.
    ///
    /// A spell never names a colour, a beam, a screen effect or a particle. Everything that is not
    /// shared by every spell of this shape stays behind <see cref="vfxDefName"/>, so a new composite
    /// spell can reuse this request without inheriting the railgun's look.
    /// </summary>
    public class VfxRequest
    {
        /// <summary>Map the effect belongs to; the worker is dropped with that map.</summary>
        public Map map;

        /// <summary>Thing the charge follows. Null when the effect does not follow anything.</summary>
        public Thing follow;

        /// <summary>Origin at request time, in world coordinates (XZ used, Y ignored).</summary>
        public Vector3 origin;

        /// <summary>Locked aim point, in world coordinates.</summary>
        public Vector3 target;

        /// <summary>Unit direction in the XZ plane.</summary>
        public Vector3 direction;

        /// <summary>Usable length in cells, already shortened to the map edge by the caster.</summary>
        public float length;

        /// <summary>Corridor width in cells.</summary>
        public float width;

        /// <summary>Charge duration in game ticks; the damage resolves after the same number.</summary>
        public int chargeTicks;

        /// <summary>Vfx Def that owns the worker and its look.</summary>
        public string vfxDefName;
    }
}
