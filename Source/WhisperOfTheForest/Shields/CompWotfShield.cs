using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Vfx;

namespace WhisperOfTheForest.Shields
{
    public class CompProperties_WotfShield : CompProperties_ProjectileInterceptor
    {
        public CompProperties_WotfShield()
        {
            compClass = typeof(CompWotfShield);
        }
    }

    /// <summary>
    /// Hit-count shield component shared by the grove dome rite and the planar shield spell.
    /// Derives from <see cref="CompProjectileInterceptor"/> so vanilla <c>ThingRequestGroup.ProjectileInterceptor</c>
    /// indexes it automatically while Harmony prefixes route interception to 1-hit-per-shot logic and RimLumen VFX.
    /// </summary>
    public sealed class CompWotfShield : CompProjectileInterceptor
    {
        private ShieldVfxShape shape;
        private Faction ownerFaction;
        private Thing anchorThing;
        private string vfxDefName;
        private SpellThemeDef theme;

        private Vector3 exactCenter;
        private Vector2 normalXZ;
        private Vector2 segmentA;
        private Vector2 segmentB;
        private float domeRadius;
        private float spanLength;

        private bool interceptGround;
        private bool interceptAir;
        private int maxCharges;
        private int remainingCharges;
        private int durationTicks;
        private int expiresTick;

        private VfxHandle vfx;
        private bool ended;
        private readonly int[] recentPassIds = new int[8];
        private readonly int[] recentPassTicks = new int[8];
        private int recentPassCursor;

        public ShieldVfxShape Shape => shape;
        public Thing AnchorThing => anchorThing;
        public int RemainingCharges => remainingCharges;
        public int MaxCharges => maxCharges;
        public int RemainingTicks => Mathf.Max(0, expiresTick - Find.TickManager.TicksGame);
        public bool IsActiveShield => !ended && parent.Spawned && remainingCharges > 0 && Find.TickManager.TicksGame < expiresTick;

        public void ConfigureDome(
            Building altar,
            Faction faction,
            float radius,
            int charges,
            int lifetimeTicks,
            string vfxDef)
        {
            shape = ShieldVfxShape.Dome;
            anchorThing = altar;
            ownerFaction = faction;
            exactCenter = altar.DrawPos;
            exactCenter.y = 0f;
            normalXZ = new Vector2(0f, 1f);
            domeRadius = radius;
            spanLength = 0f;
            interceptGround = true;
            interceptAir = true;
            maxCharges = Mathf.Max(1, charges);
            remainingCharges = maxCharges;
            durationTicks = lifetimeTicks;
            expiresTick = Find.TickManager.TicksGame + lifetimeTicks;
            vfxDefName = vfxDef;
            theme = null;
        }

        public void ConfigurePlanar(
            Faction faction,
            PlanarShieldGeometry geometry,
            float length,
            int charges,
            int lifetimeTicks,
            string vfxDef,
            SpellThemeDef spellTheme)
        {
            shape = ShieldVfxShape.Planar;
            anchorThing = null;
            ownerFaction = faction;
            exactCenter = new Vector3(geometry.Center.x, 0f, geometry.Center.y);
            normalXZ = geometry.Normal;
            segmentA = geometry.SegmentA;
            segmentB = geometry.SegmentB;
            domeRadius = 0f;
            spanLength = length;
            interceptGround = true;
            interceptAir = false;
            maxCharges = Mathf.Max(1, charges);
            remainingCharges = maxCharges;
            durationTicks = lifetimeTicks;
            expiresTick = Find.TickManager.TicksGame + lifetimeTicks;
            vfxDefName = vfxDef;
            theme = spellTheme;
        }

        public override void PostPostMake()
        {
            // Do not call base.PostPostMake(): avoids allocating unused vanilla StunHandler/charge timers.
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (ended || remainingCharges <= 0 || Find.TickManager.TicksGame >= expiresTick || vfxDefName.NullOrEmpty())
            {
                return;
            }

            StartVfx(respawningAfterLoad);
        }

        public override void CompTick()
        {
            if (!parent.Spawned || ended)
            {
                return;
            }

            if (anchorThing != null && (anchorThing.Destroyed || !anchorThing.Spawned || anchorThing.Map != parent.Map))
            {
                BreakAndDestroy("anchor lost");
                return;
            }

            if (Find.TickManager.TicksGame >= expiresTick)
            {
                ExpireAndDestroy();
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            if (!ended)
            {
                vfx?.End();
            }

            vfx = null;
        }

        public override void PostDraw()
        {
            // Intentionally empty: visuals belong strictly to VfxService / RimLumen.
        }

        public override void PostDrawExtraSelectionOverlays()
        {
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield break;
        }

        public override string CompInspectStringExtra()
        {
            return string.Empty;
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
        }

        public bool TryInterceptVanillaProjectile(Projectile projectile, Vector3 lastExactPos, Vector3 newExactPos)
        {
            if (!IsActiveShield || projectile == null)
            {
                return false;
            }

            bool isAir = projectile.def.projectile.flyOverhead;
            if ((isAir && !interceptAir) || (!isAir && !interceptGround))
            {
                return false;
            }

            var stepStart = new Vector2(lastExactPos.x, lastExactPos.z);
            var stepEnd = new Vector2(newExactPos.x, newExactPos.z);
            float stepReach = projectile.def.projectile.SpeedTilesPerTick;

            if (!ShieldInterception.IsHostileLauncher(projectile.Launcher, ownerFaction))
            {
                TryNotifyFriendlyPass(projectile.thingIDNumber, stepStart, stepEnd, stepReach, isAir);
                return false;
            }

            return TryIntersectAndSpend(
                stepStart,
                stepEnd,
                stepReach,
                isAir,
                projectile.def.defName,
                out _);
        }

        public bool TryInterceptBombardment(Bombardment bombardment, Bombardment.BombardmentProjectile projectile)
        {
            if (!IsActiveShield || !interceptAir || shape != ShieldVfxShape.Dome || bombardment == null)
            {
                return false;
            }

            if (!ShieldInterception.IsHostileLauncher(bombardment.instigator, ownerFaction))
            {
                return false;
            }

            if (!projectile.targetCell.InHorDistOf(parent.Position, domeRadius))
            {
                return false;
            }

            Vector3 hitPos = projectile.targetCell.ToVector3Shifted();
            SpendOneCharge(hitPos, Vector3.back, isAirProjectile: true, "Bombardment");
            return true;
        }

        public bool CanBombardmentStartFireAt(Bombardment bombardment, IntVec3 cell)
        {
            if (!IsActiveShield || !interceptAir || shape != ShieldVfxShape.Dome || bombardment == null)
            {
                return true;
            }

            if (!ShieldInterception.IsHostileLauncher(bombardment.instigator, ownerFaction))
            {
                return true;
            }

            return !cell.InHorDistOf(parent.Position, domeRadius);
        }

        public bool TryInterceptArcaneStep(Thing caster, Vector2 prevPos, Vector2 newPos, out Vector2 hitPos)
        {
            hitPos = default;
            if (!IsActiveShield || !interceptGround)
            {
                return false;
            }

            float stepDist = (newPos - prevPos).magnitude;
            if (!ShieldInterception.IsHostileLauncher(caster, ownerFaction))
            {
                int sourceKey = caster != null ? (caster.thingIDNumber ^ 0x40000000) : 0;
                TryNotifyFriendlyPass(sourceKey, prevPos, newPos, stepDist, isAir: false);
                return false;
            }

            return TryIntersectAndSpend(prevPos, newPos, stepDist, isAirProjectile: false, "ArcaneMissile", out hitPos);
        }

        private void TryNotifyFriendlyPass(int sourceId, Vector2 p0, Vector2 p1, float stepReach, bool isAir)
        {
            if (vfx == null)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (sourceId != 0)
            {
                for (int i = 0; i < recentPassIds.Length; i++)
                {
                    if (recentPassIds[i] == sourceId && now - recentPassTicks[i] < 8)
                    {
                        return;
                    }
                }
            }

            var center2D = new Vector2(exactCenter.x, exactCenter.z);
            float boundRadius = (shape == ShieldVfxShape.Dome ? domeRadius : spanLength * 0.5f) + stepReach + 0.25f;
            if ((p1 - center2D).sqrMagnitude > boundRadius * boundRadius)
            {
                return;
            }

            Vector2 passPoint;
            bool crossed = shape == ShieldVfxShape.Dome
                ? ShieldGeometry.TryIntersectCircleCrossing(center2D, domeRadius, p0, p1, out passPoint)
                : ShieldGeometry.TryIntersectSegment(segmentA, segmentB, p0, p1, out passPoint);
            if (!crossed)
            {
                return;
            }

            if (sourceId != 0)
            {
                recentPassIds[recentPassCursor] = sourceId;
                recentPassTicks[recentPassCursor] = now;
                recentPassCursor = (recentPassCursor + 1) % recentPassIds.Length;
            }

            Vector2 delta = p1 - p0;
            Vector3 travelDir = delta.sqrMagnitude > 1e-8f
                ? new Vector3(delta.x, 0f, delta.y).normalized
                : new Vector3(normalXZ.x, 0f, normalXZ.y);
            vfx.ReportShieldPassThrough(new ShieldPassEvent(
                new Vector3(passPoint.x, 0f, passPoint.y),
                travelDir,
                isAir));
        }

        internal void ReplaceSilently()
        {
            ended = true;
            vfx?.End();
            vfx = null;
            if (parent.Spawned)
            {
                parent.Destroy();
            }
        }

        private bool TryIntersectAndSpend(
            Vector2 p0,
            Vector2 p1,
            float stepReach,
            bool isAirProjectile,
            string sourceLabel,
            out Vector2 hitPoint)
        {
            hitPoint = default;
            var center2D = new Vector2(exactCenter.x, exactCenter.z);
            float boundRadius = (shape == ShieldVfxShape.Dome ? domeRadius : spanLength * 0.5f) + stepReach + 0.25f;
            if ((p1 - center2D).sqrMagnitude > boundRadius * boundRadius)
            {
                return false;
            }

            bool hit = shape == ShieldVfxShape.Dome
                ? ShieldGeometry.TryIntersectCircleIncoming(center2D, domeRadius, p0, p1, out hitPoint)
                : ShieldGeometry.TryIntersectSegment(segmentA, segmentB, p0, p1, out hitPoint);
            if (!hit)
            {
                return false;
            }

            Vector2 delta = p1 - p0;
            Vector3 incomingDir = delta.sqrMagnitude > 1e-8f
                ? new Vector3(delta.x, 0f, delta.y).normalized
                : new Vector3(-normalXZ.x, 0f, -normalXZ.y);
            SpendOneCharge(new Vector3(hitPoint.x, 0f, hitPoint.y), incomingDir, isAirProjectile, sourceLabel);
            return true;
        }

        private void SpendOneCharge(Vector3 hitWorldPos, Vector3 incomingDirXZ, bool isAirProjectile, string sourceLabel)
        {
            remainingCharges = Mathf.Max(0, remainingCharges - 1);
            vfx?.ReportShieldHit(new ShieldHitEvent(
                hitWorldPos,
                incomingDirXZ,
                isAirProjectile,
                remainingCharges,
                maxCharges));

            WotfLog.Debug(WotfLog.ModuleShield, shape + " shield at " + parent.Position
                + " intercepted " + sourceLabel + ", charges=" + remainingCharges + "/" + maxCharges);

            if (remainingCharges <= 0)
            {
                BreakAndDestroy("charges depleted");
            }
        }

        private void BreakAndDestroy(string reason)
        {
            if (ended)
            {
                return;
            }

            ended = true;
            vfx?.Trigger(VfxStates.Break);
            vfx = null;
            WotfLog.Info(WotfLog.ModuleShield, shape + " shield broke at " + parent.Position + " (" + reason + ")");
            if (parent.Spawned)
            {
                parent.Destroy();
            }
        }

        private void ExpireAndDestroy()
        {
            if (ended)
            {
                return;
            }

            ended = true;
            vfx?.Trigger(VfxStates.Expire);
            vfx = null;
            WotfLog.Info(WotfLog.ModuleShield, shape + " shield expired at " + parent.Position
                + " with " + remainingCharges + "/" + maxCharges + " charges unused");
            if (parent.Spawned)
            {
                parent.Destroy();
            }
        }

        private void StartVfx(bool respawningAfterLoad)
        {
            vfx = VfxService.Play(new ShieldVfxRequest
            {
                map = parent.Map,
                follow = parent,
                theme = theme,
                origin = exactCenter,
                direction = new Vector3(normalXZ.x, 0f, normalXZ.y),
                length = spanLength,
                width = shape == ShieldVfxShape.Dome ? domeRadius * 2f : spanLength,
                vfxDefName = vfxDefName,
                shape = shape,
                radius = domeRadius,
                spanLength = spanLength,
                maxCharges = maxCharges,
                currentCharges = remainingCharges,
                durationTicks = durationTicks,
                remainingTicks = RemainingTicks,
                respawningAfterLoad = respawningAfterLoad
            });
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref shape, "shieldShape", ShieldVfxShape.Dome);
            Scribe_References.Look(ref ownerFaction, "ownerFaction");
            Scribe_References.Look(ref anchorThing, "anchorThing");
            Scribe_Values.Look(ref vfxDefName, "vfxDefName");
            Scribe_Defs.Look(ref theme, "theme");
            Scribe_Values.Look(ref exactCenter, "exactCenter");
            Scribe_Values.Look(ref normalXZ, "normalXZ");
            Scribe_Values.Look(ref segmentA, "segmentA");
            Scribe_Values.Look(ref segmentB, "segmentB");
            Scribe_Values.Look(ref domeRadius, "domeRadius");
            Scribe_Values.Look(ref spanLength, "spanLength");
            Scribe_Values.Look(ref interceptGround, "interceptGround", true);
            Scribe_Values.Look(ref interceptAir, "interceptAir");
            Scribe_Values.Look(ref maxCharges, "maxCharges", 1);
            Scribe_Values.Look(ref remainingCharges, "remainingCharges");
            Scribe_Values.Look(ref durationTicks, "durationTicks");
            Scribe_Values.Look(ref expiresTick, "expiresTick");
        }
    }

    public static class ShieldInterception
    {
        public const string ShieldThingDefName = "WotF_ShieldField";

        /// <summary>
        /// One-way faction rule: allied, own-faction and neutral/unaffiliated shots pass through from any
        /// direction; only hostile launchers are intercepted. Handles launchers destroyed mid-flight cleanly.
        /// </summary>
        public static bool IsHostileLauncher(Thing launcher, Faction shieldFaction)
        {
            if (launcher == null || shieldFaction == null)
            {
                return false;
            }

            if (!launcher.Destroyed && launcher.Spawned)
            {
                return launcher.HostileTo(shieldFaction);
            }

            return launcher.Faction != null && launcher.Faction.HostileTo(shieldFaction);
        }

        public static CompWotfShield FindActiveOnAltar(Building altar)
        {
            if (altar == null || !altar.Spawned || altar.Map == null)
            {
                return null;
            }

            List<Thing> list = altar.Map.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].TryGetComp<CompWotfShield>() is CompWotfShield shield
                    && shield.AnchorThing == altar
                    && shield.IsActiveShield)
                {
                    return shield;
                }
            }

            return null;
        }

        public static CompWotfShield SpawnOrReplaceDome(
            Building altar,
            float radius,
            int charges,
            int durationTicks,
            string vfxDefName)
        {
            Map map = altar.Map;
            CompWotfShield existing = FindActiveOnAltar(altar);
            existing?.ReplaceSilently();

            ThingDef def = DefDatabase<ThingDef>.GetNamed(ShieldThingDefName);
            var thing = (ThingWithComps)ThingMaker.MakeThing(def);
            CompWotfShield shield = thing.GetComp<CompWotfShield>();
            shield.ConfigureDome(altar, altar.Faction, radius, charges, durationTicks, vfxDefName);
            GenSpawn.Spawn(thing, altar.Position, map);

            WotfLog.Info(WotfLog.ModuleShield, "grove dome opened at " + altar.Position
                + " radius=" + radius.ToString("0.#") + " charges=" + shield.MaxCharges
                + " durationTicks=" + durationTicks);
            return shield;
        }

        public static CompWotfShield SpawnPlanar(
            Map map,
            IntVec3 spawnCell,
            Faction faction,
            PlanarShieldGeometry geometry,
            float spanLength,
            int charges,
            int durationTicks,
            string vfxDefName,
            SpellThemeDef theme)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamed(ShieldThingDefName);
            var thing = (ThingWithComps)ThingMaker.MakeThing(def);
            CompWotfShield shield = thing.GetComp<CompWotfShield>();
            shield.ConfigurePlanar(faction, geometry, spanLength, charges, durationTicks, vfxDefName, theme);
            GenSpawn.Spawn(thing, spawnCell, map);

            WotfLog.Info(WotfLog.ModuleShield, "planar shield spawned at " + spawnCell
                + " length=" + spanLength.ToString("0.#") + " charges=" + shield.MaxCharges
                + " durationTicks=" + durationTicks);
            return shield;
        }

        public static bool TryInterceptArcaneMissile(
            Map map,
            Thing caster,
            float prevX,
            float prevZ,
            ref float posX,
            ref float posZ)
        {
            List<Thing> list = map.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor);
            if (list.Count == 0)
            {
                return false;
            }

            var prevPos = new Vector2(prevX, prevZ);
            var newPos = new Vector2(posX, posZ);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].TryGetComp<CompWotfShield>() is CompWotfShield shield
                    && shield.TryInterceptArcaneStep(caster, prevPos, newPos, out Vector2 hit))
                {
                    posX = hit.x;
                    posZ = hit.y;
                    return true;
                }
            }

            return false;
        }
    }
}
