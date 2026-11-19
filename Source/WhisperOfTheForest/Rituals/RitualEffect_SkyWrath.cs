using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using WhisperOfTheForest.Mana;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Sustained T3 Sky Wrath rite: while the circle holds, lightning strikes the closest unroofed
    /// hostile pawn at a rate governed by ritual strength, spending <see cref="RitualDef.manaPerStrike"/>
    /// from the network before each strike (nothing is spent while no target is struck). Contained or
    /// queued-for-capture anomaly entities, colony prisoners and buildings are spared.
    /// </summary>
    public sealed class RitualEffect_SkyWrath : RitualEffectWorker
    {
        public override void Apply(RitualContext context)
        {
            throw new InvalidOperationException("Sky wrath is a sustained ritual and does not settle through Apply.");
        }

        public override int InitialSustainedDelayTicks(RitualDef def, float ritualStrength)
        {
            return 0;
        }

        public override int SustainedTick(
            Building altar,
            Pawn host,
            RitualDef def,
            float ritualStrength,
            ManaNetwork network,
            out bool manaDepleted)
        {
            Map map = altar.Map;
            Pawn target = FindClosestTarget(map, altar.Position, def.minStrikeRadius);
            if (target == null)
            {
                manaDepleted = false;
                return def.idleScanIntervalTicks;
            }

            if (network == null || !network.TryDraw(def.manaPerStrike))
            {
                manaDepleted = true;
                return 0;
            }

            manaDepleted = false;
            IntVec3 strikeCell = target.Position;
            map.weatherManager.eventHandler.AddEvent(new WeatherEvent_SkyWrathStrike(
                map,
                strikeCell,
                host,
                def.strikeDamage,
                def.strikeArmorPenetration,
                def.strikeRadius));

            int nextDelay = NextStrikeDelayTicks(def, ritualStrength);
            WotfLog.Debug(
                WotfLog.ModuleRitual,
                "sky wrath struck " + target.LabelShort + " at " + strikeCell
                + " mana=" + def.manaPerStrike + " damage=" + def.strikeDamage + " nextTicks=" + nextDelay);
            return nextDelay;
        }

        public static int NextStrikeDelayTicks(RitualDef def, float ritualStrength)
        {
            float perMinute = def.StrikesPerMinuteFor(ritualStrength);
            float baseTicks = 60f * GenTicks.TicksPerRealSecond / perMinute;
            float jitter = def.strikeIntervalJitter > 0f
                ? Rand.Range(-def.strikeIntervalJitter, def.strikeIntervalJitter)
                : 0f;
            return Math.Max(1, (int)Math.Round(baseTicks * (1f + jitter)));
        }

        public static Pawn FindClosestTarget(Map map, IntVec3 altarPos, float minStrikeRadius)
        {
            Pawn closest = null;
            int bestDistSq = int.MaxValue;
            foreach (IAttackTarget candidate in map.attackTargetsCache.TargetsHostileToColony)
            {
                if (!IsEligibleTarget(candidate, map, altarPos, minStrikeRadius, out Pawn pawn))
                {
                    continue;
                }

                int distSq = (pawn.Position - altarPos).LengthHorizontalSquared;
                if (distSq < bestDistSq || (distSq == bestDistSq && pawn.thingIDNumber < closest.thingIDNumber))
                {
                    bestDistSq = distSq;
                    closest = pawn;
                }
            }

            return closest;
        }

        public static bool IsEligibleTarget(
            IAttackTarget candidate,
            Map map,
            IntVec3 altarPos,
            float minStrikeRadius,
            out Pawn pawn)
        {
            if (!(candidate?.Thing is Pawn found))
            {
                pawn = null;
                return false;
            }

            pawn = found;
            if (!pawn.Spawned || pawn.Map != map || pawn.Dead || pawn.Downed)
            {
                return false;
            }

            if (pawn.Faction == Faction.OfPlayer || pawn.IsPrisonerOfColony || pawn.IsPrisoner)
            {
                return false;
            }

            CompHoldingPlatformTarget holding = pawn.TryGetComp<CompHoldingPlatformTarget>();
            if (holding != null && (holding.CurrentlyHeldOnPlatform || holding.targetHolder != null))
            {
                return false;
            }

            if (candidate.ThreatDisabled(null))
            {
                return false;
            }

            CompCanBeDormant dormant = pawn.TryGetComp<CompCanBeDormant>();
            if (dormant != null && !dormant.Awake)
            {
                return false;
            }

            CompMechanoid mechanoid = pawn.TryGetComp<CompMechanoid>();
            if (mechanoid != null && mechanoid.Deactivated)
            {
                return false;
            }

            if (!pawn.HostileTo(Faction.OfPlayer))
            {
                return false;
            }

            if (pawn.Position.Roofed(map) || pawn.Position.Fogged(map))
            {
                return false;
            }

            if (minStrikeRadius > 0f && pawn.Position.InHorDistOf(altarPos, minStrikeRadius))
            {
                return false;
            }

            return true;
        }
    }

    [StaticConstructorOnStartup]
    internal sealed class WeatherEvent_SkyWrathStrike : WeatherEvent_LightningFlash
    {
        private static readonly Material LightningMat = MatLoader.LoadMat("Weather/LightningBolt");

        private readonly IntVec3 strikeLoc;
        private readonly Pawn instigator;
        private readonly int damage;
        private readonly float armorPenetration;
        private readonly float radius;
        private Mesh boltMesh;

        public WeatherEvent_SkyWrathStrike(
            Map map,
            IntVec3 strikeLoc,
            Pawn instigator,
            int damage,
            float armorPenetration,
            float radius)
            : base(map)
        {
            this.strikeLoc = strikeLoc;
            this.instigator = instigator;
            this.damage = damage;
            this.armorPenetration = armorPenetration;
            this.radius = radius;
        }

        public override void FireEvent()
        {
            SoundDefOf.Thunder_OffMap.PlayOneShotOnCamera(map);
            boltMesh = LightningBoltMeshPool.RandomBoltMesh;
            if (!strikeLoc.Fogged(map))
            {
                List<Thing> entityHolders = map.listerThings.ThingsInGroup(ThingRequestGroup.EntityHolder);
                List<Thing> ignored = entityHolders != null && entityHolders.Count > 0
                    ? new List<Thing>(entityHolders)
                    : null;
                GenExplosion.DoExplosion(
                    center: strikeLoc,
                    map: map,
                    radius: radius,
                    damType: DamageDefOf.Flame,
                    instigator: instigator,
                    damAmount: damage,
                    armorPenetration: armorPenetration,
                    ignoredThings: ignored);
                Vector3 loc = strikeLoc.ToVector3Shifted();
                for (int i = 0; i < 4; i++)
                {
                    FleckMaker.ThrowSmoke(loc, map, 1.5f);
                    FleckMaker.ThrowMicroSparks(loc, map);
                    FleckMaker.ThrowLightningGlow(loc, map, 1.5f);
                }
            }

            SoundDefOf.Thunder_OnMap.PlayOneShot(SoundInfo.InMap(new TargetInfo(strikeLoc, map)));
        }

        public override void WeatherEventDraw()
        {
            if (boltMesh != null)
            {
                Graphics.DrawMesh(
                    boltMesh,
                    strikeLoc.ToVector3ShiftedWithAltitude(AltitudeLayer.Weather),
                    Quaternion.identity,
                    FadedMaterialPool.FadedVersionOf(LightningMat, LightningBrightness),
                    0);
            }
        }
    }
}
