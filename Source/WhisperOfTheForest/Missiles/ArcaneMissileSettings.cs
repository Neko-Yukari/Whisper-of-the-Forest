using System;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Experimental.Missiles
{
    /// <summary>What a missile does where it lands.</summary>
    public enum ArcaneMissileImpactMode
    {
        Damage,
        Heal,
        Explode
    }

    /// <summary>
    /// Everything that defines one volley: launch pattern, steering, hit test and payload.
    /// XML authors configure it through <see cref="CompProperties_AbilityWotFArcaneMissiles"/>;
    /// code callers may pass their own instance to <see cref="ArcaneMissileLauncher.Fire"/>.
    /// The launcher copies the instance per volley, so mutating one later never changes a Def.
    /// </summary>
    public class ArcaneMissileSettings : IExposable
    {
        public int missileCount = 3;
        public float speedCellsPerSecond = 13f;
        public float maxTurnDegreesPerSecond = 300f;
        public float wobbleAmplitude = 0.14f;
        public float wobbleFrequency = 2.2f;
        public float launchSpreadDegrees = 8f;
        public float flightHeight = 1.2f;
        public float hitRadius = 0.45f;
        public float lifetimeSeconds = 10f;
        public float maxRangeCells = 34f;

        public float damageAmount = 7f;
        public float armorPenetration = 0.5f;
        public DamageDef damageDef;
        public ArcaneMissileImpactMode impactMode = ArcaneMissileImpactMode.Damage;
        public float explosionRadius = 1.7f;
        public float healAmount = 10f;

        /// <summary>Selected visual backend. Null means "use the documented default" at launch.</summary>
        public ArcaneMissileVisualDef visualDef;

        /// <summary>Selected hit strategy. Non-null always wins over <see cref="impactMode"/>.</summary>
        public ArcaneMissileImpactDef impactDef;

        /// <summary>
        /// Skips only the lifetime and range checks. Out-of-bounds, map removal and collision
        /// cleanup still apply, and existing numeric limits are not changed.
        /// </summary>
        public bool ignoreFlightLimits;

        /// <summary>
        /// When true the missile requires a live Thing target and expires (without resolving a hit)
        /// as soon as that target dies, despawns, changes map or is lost while loading.
        /// </summary>
        public bool expireOnTargetLost;

        public float missileSize = 0.55f;
        public float trailTime = 0.35f;
        public string missileColorHex = "#9BD8FF";
        public string trailColorHex = "#3F7DFF";

        /// <summary>Damage type actually used, falling back to a core damage def when unset.</summary>
        public DamageDef ResolvedDamageDef => damageDef ?? ArcaneMissileDefs.ArcaneForce;

        /// <summary>Sane defaults for every value; invalid XML or lab input is clamped here.</summary>
        public void Clamp()
        {
            missileCount = Math.Clamp(missileCount, 1, 12);
            speedCellsPerSecond = Math.Clamp(speedCellsPerSecond, 1f, 60f);
            maxTurnDegreesPerSecond = Math.Clamp(maxTurnDegreesPerSecond, 10f, 1440f);
            wobbleAmplitude = Math.Clamp(wobbleAmplitude, 0f, 1.5f);
            wobbleFrequency = Math.Clamp(wobbleFrequency, 0f, 12f);
            launchSpreadDegrees = Math.Clamp(launchSpreadDegrees, 0f, 180f);
            flightHeight = Math.Clamp(flightHeight, 0.2f, 4f);
            hitRadius = Math.Clamp(hitRadius, 0.05f, 2f);
            lifetimeSeconds = Math.Clamp(lifetimeSeconds, 1f, 120f);
            maxRangeCells = Math.Clamp(maxRangeCells, 1f, 120f);
            damageAmount = Math.Clamp(damageAmount, 0f, 500f);
            armorPenetration = Math.Clamp(armorPenetration, 0f, 10f);
            explosionRadius = Math.Clamp(explosionRadius, 0.1f, 12f);
            healAmount = Math.Clamp(healAmount, 0f, 500f);
            missileSize = Math.Clamp(missileSize, 0.05f, 4f);
            trailTime = Math.Clamp(trailTime, 0.02f, 3f);
        }

        public ArcaneMissileSettings Copy()
        {
            return (ArcaneMissileSettings)MemberwiseClone();
        }

        /// <summary>
        /// Missiles keep their own copy so a save reproduces the volley that was actually fired,
        /// even if the Def or the caller's instance changes later.
        /// </summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref missileCount, "missileCount", 3);
            Scribe_Values.Look(ref speedCellsPerSecond, "speedCellsPerSecond", 13f);
            Scribe_Values.Look(ref maxTurnDegreesPerSecond, "maxTurnDegreesPerSecond", 300f);
            Scribe_Values.Look(ref wobbleAmplitude, "wobbleAmplitude", 0.14f);
            Scribe_Values.Look(ref wobbleFrequency, "wobbleFrequency", 2.2f);
            Scribe_Values.Look(ref launchSpreadDegrees, "launchSpreadDegrees", 8f);
            Scribe_Values.Look(ref flightHeight, "flightHeight", 1.2f);
            Scribe_Values.Look(ref hitRadius, "hitRadius", 0.45f);
            Scribe_Values.Look(ref lifetimeSeconds, "lifetimeSeconds", 10f);
            Scribe_Values.Look(ref maxRangeCells, "maxRangeCells", 34f);
            Scribe_Values.Look(ref damageAmount, "damageAmount", 7f);
            Scribe_Values.Look(ref armorPenetration, "armorPenetration", 0.5f);
            Scribe_Defs.Look(ref damageDef, "damageDef");
            Scribe_Values.Look(ref impactMode, "impactMode", ArcaneMissileImpactMode.Damage);
            Scribe_Defs.Look(ref visualDef, "visualDef");
            Scribe_Defs.Look(ref impactDef, "impactDef");
            Scribe_Values.Look(ref ignoreFlightLimits, "ignoreFlightLimits", false);
            Scribe_Values.Look(ref expireOnTargetLost, "expireOnTargetLost", false);
            Scribe_Values.Look(ref explosionRadius, "explosionRadius", 1.7f);
            Scribe_Values.Look(ref healAmount, "healAmount", 10f);
            Scribe_Values.Look(ref missileSize, "missileSize", 0.55f);
            Scribe_Values.Look(ref trailTime, "trailTime", 0.35f);
            Scribe_Values.Look(ref missileColorHex, "missileColorHex", "#9BD8FF");
            Scribe_Values.Look(ref trailColorHex, "trailColorHex", "#3F7DFF");
        }

        public static ArcaneMissileSettings CreateDefault()
        {
            return new ArcaneMissileSettings
            {
                damageDef = ArcaneMissileDefs.ArcaneForce
            };
        }
    }
}
