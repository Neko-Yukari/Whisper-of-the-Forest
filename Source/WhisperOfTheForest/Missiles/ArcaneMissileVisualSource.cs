using System;
using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Experimental.Missiles
{
    /// <summary>Authoritative state handed to the visual layer once per game tick.</summary>
    public struct ArcaneMissileVisualFrame
    {
        public Map Map;
        public float X;
        public float Y;
        public float Z;
        public float HeadingX;
        public float HeadingZ;
        public int AgeTicks;
        public float WobblePhase;
        public ArcaneMissileSettings Settings;
    }

    /// <summary>Where a missile detonated, for the visual layer's impact effect.</summary>
    public struct ArcaneMissileImpactVisualContext
    {
        public Map Map;
        public IntVec3 Cell;
        public float X;
        public float Y;
        public float Z;
        public Thing Hit;
        public ArcaneMissileSettings Settings;
    }

    /// <summary>
    /// Lifecycle of one missile's presentation. Instances are created per missile from the Def's
    /// visualClass, receive the authoritative frame every game tick while their map is current, and
    /// are disposed when the missile ends or the map stops being current. Implementations own every
    /// engine object they create.
    ///
    /// The wobble is decoration: <see cref="VisualPosition"/> offsets only the drawn position, so
    /// collision and the saved state never depend on a backend.
    /// </summary>
    public abstract class ArcaneMissileVisualSource
    {
        private static readonly List<Action<Map>> mapFrameHandlers = new List<Action<Map>>();
        private static readonly List<Action<Map>> mapHiddenHandlers = new List<Action<Map>>();

        /// <summary>
        /// Registers backend-wide effects that outlive a single missile (impact bursts, probes).
        /// Called by the map component every frame and when a map stops being the current one, so a
        /// backend never has to guess about visibility or rely on its own delayed callbacks.
        /// </summary>
        public static void RegisterMapScopedEffects(Action<Map> onMapFrame, Action<Map> onMapHidden)
        {
            if (onMapFrame != null)
            {
                mapFrameHandlers.Add(onMapFrame);
            }

            if (onMapHidden != null)
            {
                mapHiddenHandlers.Add(onMapHidden);
            }
        }

        public static void NotifyMapFrame(Map map)
        {
            for (int i = 0; i < mapFrameHandlers.Count; i++)
            {
                try
                {
                    mapFrameHandlers[i](map);
                }
                catch (Exception exception)
                {
                    ArcaneMissileDiagnostics.Error("map-frame visual handler failed: " + exception);
                }
            }
        }

        public static void NotifyMapHidden(Map map)
        {
            for (int i = 0; i < mapHiddenHandlers.Count; i++)
            {
                try
                {
                    mapHiddenHandlers[i](map);
                }
                catch (Exception exception)
                {
                    ArcaneMissileDiagnostics.Error("map-hidden visual handler failed: " + exception);
                }
            }
        }

        public abstract void Spawn(ArcaneMissileVisualFrame frame);

        public abstract void Update(ArcaneMissileVisualFrame frame);

        /// <summary>Plays the detonation effect. Must not own state that needs per-tick updates.</summary>
        public virtual void Impact(ArcaneMissileImpactVisualContext context)
        {
        }

        /// <summary>Fire-and-forget debug probe used by the lab to check a backend in isolation.</summary>
        public virtual void Probe(ArcaneMissileImpactVisualContext context)
        {
            Impact(context);
        }

        public virtual void Dispose()
        {
        }

        /// <summary>
        /// Applies the shared cosmetic weave and height bob on top of the authoritative position.
        /// Every backend uses this, so switching visuals never changes gameplay or saved state.
        /// </summary>
        protected static void VisualPosition(ArcaneMissileVisualFrame frame, out float x, out float y, out float z)
        {
            if (frame.Settings == null)
            {
                x = frame.X;
                y = frame.Y;
                z = frame.Z;
                return;
            }

            float phase = frame.AgeTicks * frame.Settings.wobbleFrequency / GenTicks.TicksPerRealSecond * MathF.PI * 2f
                + frame.WobblePhase;
            float wobble = MathF.Sin(phase) * frame.Settings.wobbleAmplitude;
            float bob = MathF.Cos(phase * 0.7f) * frame.Settings.wobbleAmplitude * 0.35f;

            x = frame.X + frame.HeadingZ * wobble;
            z = frame.Z - frame.HeadingX * wobble;
            y = MathF.Max(0.1f, frame.Y + bob);
        }
    }
}
