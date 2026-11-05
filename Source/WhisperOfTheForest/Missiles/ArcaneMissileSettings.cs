using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Missiles
{
    /// <summary>
    /// One volley: launch pattern, steering, hit test, look, and the payload it delivers. The numbers
    /// come from XML (a projectile form's <c>&lt;projectile&gt;</c> block) and are checked by
    /// <see cref="ConfigErrors"/> when the Def loads; there is no code default behind them and the
    /// launcher never corrects a value. The launcher copies the instance per volley, so mutating one
    /// later never changes a Def or a dart in flight.
    /// </summary>
    public class ArcaneMissileSettings : IExposable
    {
        public int missileCount;
        public float speedCellsPerSecond;
        public float maxTurnDegreesPerSecond;
        public float wobbleAmplitude;
        public float wobbleFrequency;
        public float launchSpreadDegrees;
        public float flightHeight;
        public float hitRadius;
        public float lifetimeSeconds;
        public float maxRangeCells;

        /// <summary>
        /// When true the missile requires a live Thing target and expires (without resolving a hit)
        /// as soon as that target dies, despawns, changes map or is lost while loading.
        /// </summary>
        public bool expireOnTargetLost;

        public ArcaneMissileVisualDef visualDef;
        public float missileSize;
        public float trailTime;
        public string missileColorHex;
        public string trailColorHex;

        /// <summary>
        /// What a hit does. Set by whoever fires the volley (a spell freezes its plan here); the
        /// missile layer only calls it and never knows what it is.
        /// </summary>
        public IArcaneMissilePayload payload;

        /// <summary>Load-time checks for an XML block; the owning Def reports them.</summary>
        public IEnumerable<string> ConfigErrors()
        {
            if (missileCount < 1)
            {
                yield return "missileCount must be at least 1";
            }

            if (speedCellsPerSecond <= 0f || maxTurnDegreesPerSecond <= 0f)
            {
                yield return "speedCellsPerSecond and maxTurnDegreesPerSecond must be positive";
            }

            if (hitRadius <= 0f || flightHeight <= 0f || lifetimeSeconds <= 0f || maxRangeCells <= 0f)
            {
                yield return "hitRadius, flightHeight, lifetimeSeconds and maxRangeCells must be positive";
            }

            if (wobbleAmplitude < 0f || wobbleFrequency < 0f || launchSpreadDegrees < 0f)
            {
                yield return "wobbleAmplitude, wobbleFrequency and launchSpreadDegrees must not be negative";
            }

            if (missileSize <= 0f || trailTime <= 0f)
            {
                yield return "missileSize and trailTime must be positive";
            }

            if (visualDef == null)
            {
                yield return "visualDef is null";
            }

            if (!ColorUtility.TryParseHtmlString(missileColorHex, out _) || !ColorUtility.TryParseHtmlString(trailColorHex, out _))
            {
                yield return "missileColorHex and trailColorHex must be HTML colours such as #7FE3C7";
            }

            if (payload != null)
            {
                yield return "a payload is set by the caster at launch, never in XML";
            }
        }

        public ArcaneMissileSettings Copy()
        {
            var copy = (ArcaneMissileSettings)MemberwiseClone();

            // The payload is a mutable object graph, so a plain memberwise copy would let an edit of
            // the caller's settings rewrite a volley that is already in flight.
            copy.payload = payload?.Copy();
            return copy;
        }

        /// <summary>
        /// Missiles keep their own copy so a save reproduces the volley that was actually fired,
        /// even if the Def or the caller's instance changes later.
        /// </summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref missileCount, "missileCount");
            Scribe_Values.Look(ref speedCellsPerSecond, "speedCellsPerSecond");
            Scribe_Values.Look(ref maxTurnDegreesPerSecond, "maxTurnDegreesPerSecond");
            Scribe_Values.Look(ref wobbleAmplitude, "wobbleAmplitude");
            Scribe_Values.Look(ref wobbleFrequency, "wobbleFrequency");
            Scribe_Values.Look(ref launchSpreadDegrees, "launchSpreadDegrees");
            Scribe_Values.Look(ref flightHeight, "flightHeight");
            Scribe_Values.Look(ref hitRadius, "hitRadius");
            Scribe_Values.Look(ref lifetimeSeconds, "lifetimeSeconds");
            Scribe_Values.Look(ref maxRangeCells, "maxRangeCells");
            Scribe_Values.Look(ref expireOnTargetLost, "expireOnTargetLost");
            Scribe_Defs.Look(ref visualDef, "visualDef");
            Scribe_Values.Look(ref missileSize, "missileSize");
            Scribe_Values.Look(ref trailTime, "trailTime");
            Scribe_Values.Look(ref missileColorHex, "missileColorHex");
            Scribe_Values.Look(ref trailColorHex, "trailColorHex");

            // Saved with the projectile, so a dart in flight across a load keeps its exact payload.
            Scribe_Deep.Look(ref payload, "payload");
        }
    }
}
