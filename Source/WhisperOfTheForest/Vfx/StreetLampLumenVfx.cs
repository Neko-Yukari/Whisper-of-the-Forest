using System;
using System.Collections.Generic;
using RimLumen.Core.Api;
using RimLumen.Core.Contracts;
using RimLumen.Core.Lighting;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Mana;
using CurveKey = RimLumen.Core.Contracts.CurveKey;
using NumVector3 = System.Numerics.Vector3;
using NumVector4 = System.Numerics.Vector4;

namespace WhisperOfTheForest.Vfx
{
    public sealed class PlaceWorker_LumenGlowRadius : PlaceWorker
    {
        private static readonly Color RingColor = new Color(0.8f, 0.8f, 0.4f);

        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            if (thing != null)
            {
                return;
            }

            CompProperties_RimLumenLogicalLight props = def.GetCompProperties<CompProperties_RimLumenLogicalLight>();
            if (props != null)
            {
                float radius = Mathf.Max(0.5f, props.glowRadius - 2.1f);
                GenDraw.DrawRadiusRing(center, radius, RingColor);
            }
        }
    }

    public static class StreetLampVfxMath
    {
        public static float EvaluateNightFactor(float curSkyGlow, float highGlow, float lowGlow)
        {
            if (highGlow <= lowGlow)
            {
                return curSkyGlow <= lowGlow ? 1f : 0f;
            }

            float t = Mathf.Clamp01((highGlow - curSkyGlow) / (highGlow - lowGlow));
            return t * t * (3f - 2f * t);
        }

        public static bool ShouldStartleButterfly(Vector2 cursorXZ, Vector2 butterflyXZ, float fleeRadius)
        {
            return fleeRadius > 0f && (cursorXZ - butterflyXZ).sqrMagnitude < fleeRadius * fleeRadius;
        }
    }

    public sealed class StreetLampLumenVfxDef : VfxDef
    {
        // Light pool
        public float lightRadius;
        public float lightHeightCells;
        public float lightIntensity;
        public string dayLightColorHex;
        public string nightLightColorHex;
        public string dayCoreColorHex;
        public string nightCoreColorHex;
        public float nightSkyGlowHigh;
        public float nightSkyGlowLow;
        public float lightFadeSeconds;

        // Dual interior globes
        public Vector3 globe1Offset;
        public Vector2 globe1Radius;
        public Vector3 globe2Offset;
        public Vector2 globe2Radius;
        public float globeCapDipFactor;
        public float globeCapSideLiftFactor;
        public float globeBottomScale;
        public float globeHdrMultiplier;
        public float globeBreathSpeed;
        public float globeBreathAmp;

        // Night crystal butterfly
        public float butterflyNightThreshold;
        public float butterflyInitialDelaySeconds;
        public float butterflyRespawnSeconds;
        public float butterflyFleeRadius;
        public float butterflyFleeDurationSeconds;
        public float butterflyFleeSpeed;
        public float butterflyHeightMin;
        public float butterflyHeightMax;
        public float butterflyOrbitRadiusMin;
        public float butterflyOrbitRadiusMax;
        public float butterflyVisualScale;
        public float butterflySteerGain;
        public float butterflyHoverSteerGain;
        public float butterflyDamping;
        public float butterflyDustRate;
        public float butterflyDustLifetimeMin;
        public float butterflyDustLifetimeMax;
        public string butterflyBrightColorHex;
        public string butterflyMainColorHex;
        public string butterflyDeepColorHex;
        public string butterflyEdgeColorHex;
        public float butterflyHdrMultiplier;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (lightRadius <= 0f || lightHeightCells < 1f || lightIntensity <= 0f || lightFadeSeconds <= 0f)
            {
                yield return "streetlamp lightRadius > 0, lightHeightCells >= 1, lightIntensity > 0 and lightFadeSeconds > 0 are required";
            }

            if (nightSkyGlowHigh <= nightSkyGlowLow || nightSkyGlowLow < 0f || nightSkyGlowHigh > 1.5f)
            {
                yield return "nightSkyGlowHigh > nightSkyGlowLow >= 0 is required";
            }

            if (!VfxColors.IsValid(dayLightColorHex) || !VfxColors.IsValid(nightLightColorHex)
                || !VfxColors.IsValid(dayCoreColorHex) || !VfxColors.IsValid(nightCoreColorHex))
            {
                yield return "streetlamp light/core hex colors must be " + VfxColors.Format;
            }

            if (globe1Radius.x <= 0f || globe1Radius.y <= 0f || globe2Radius.x <= 0f || globe2Radius.y <= 0f
                || globeBottomScale <= 0f || globeHdrMultiplier <= 0f || globeBreathSpeed < 0f)
            {
                yield return "streetlamp globe radii, bottomScale and globeHdrMultiplier must be positive";
            }

            if (butterflyNightThreshold < 0f || butterflyNightThreshold > 1f
                || butterflyInitialDelaySeconds < 0f || butterflyRespawnSeconds <= 0f
                || butterflyFleeRadius <= 0f || butterflyFleeDurationSeconds <= 0f || butterflyFleeSpeed <= 0f)
            {
                yield return "streetlamp butterfly timing, flee radius and speed must be valid positive values";
            }

            if (butterflyHeightMin <= 0f || butterflyHeightMax <= butterflyHeightMin
                || butterflyOrbitRadiusMin <= 0f || butterflyOrbitRadiusMax <= butterflyOrbitRadiusMin
                || butterflyVisualScale <= 0f || butterflySteerGain <= 0f || butterflyHoverSteerGain <= 0f
                || butterflyDamping <= 0f || butterflyHdrMultiplier <= 0f)
            {
                yield return "streetlamp butterfly orbit/flight scale and steering parameters must be positive";
            }

            if (butterflyDustRate < 0f || butterflyDustLifetimeMin <= 0f || butterflyDustLifetimeMax < butterflyDustLifetimeMin)
            {
                yield return "streetlamp butterfly dust parameters must be positive";
            }

            if (!VfxColors.IsValid(butterflyBrightColorHex) || !VfxColors.IsValid(butterflyMainColorHex)
                || !VfxColors.IsValid(butterflyDeepColorHex) || !VfxColors.IsValid(butterflyEdgeColorHex))
            {
                yield return "streetlamp butterfly hex colors must be " + VfxColors.Format;
            }
        }
    }

    internal static class StreetLampLumenCatalog
    {
        private static readonly HashSet<string> registeredIds = new HashSet<string>(StringComparer.Ordinal);

        public static string EnsureLampLightEffect(StreetLampLumenVfxDef def)
        {
            string effectId = $"NekoYukari.WhisperOfTheForest:StreetLampLight_{def.defName}";
            if (!registeredIds.Add(effectId))
            {
                return effectId;
            }

            Color dayLight = VfxColors.ParseUnity(def.dayLightColorHex);
            RimLumenApi.RegisterEffect(new EffectAsset
            {
                EffectId = effectId,
                DefaultDuration = 4f,
                Nodes = new[]
                {
                    new EffectNodeSpec
                    {
                        Name = "lamp_pool_light",
                        LocalPosition = new NumVector3(0f, def.lightHeightCells, 0f),
                        PointLight = new PointLightSpec(
                            def.lightRadius,
                            new NumVector3(Mathf.Clamp01(dayLight.r), Mathf.Clamp01(dayLight.g), Mathf.Clamp01(dayLight.b)),
                            ScalarCurve.Constant(def.lightIntensity))
                    }
                },
                InitialState = "Lit",
                States = new[]
                {
                    new EffectStateSpec { Name = "Lit", Nodes = new[] { "lamp_pool_light" }, Duration = 4f, Loop = true }
                }
            });
            return effectId;
        }

        public static string EnsureButterflyDustEffect(StreetLampLumenVfxDef def)
        {
            string effectId = $"NekoYukari.WhisperOfTheForest:StreetLampButterflyDust_{def.defName}";
            if (!registeredIds.Add(effectId))
            {
                return effectId;
            }

            Color bright = VfxColors.ParseUnity(def.butterflyBrightColorHex);
            Color main = VfxColors.ParseUnity(def.butterflyMainColorHex);
            Color deep = VfxColors.ParseUnity(def.butterflyDeepColorHex);

            RimLumenApi.RegisterEffect(new EffectAsset
            {
                EffectId = effectId,
                DefaultDuration = 2f,
                Nodes = new[]
                {
                    new EffectNodeSpec
                    {
                        Name = "butterfly_dust",
                        SpacePlane = LumenSpacePlane.GroundXZ,
                        LocalPosition = new NumVector3(0f, 0.04f, 0f),
                        Emitter = new EmitterSpec
                        {
                            Name = "butterfly_dust",
                            PreferredTier = LumenParticleTier.L2_GpuCompute,
                            MaxParticles = 40,
                            Duration = 2f,
                            Looping = true,
                            WorldSpaceSimulation = true,
                            RateOverTime = def.butterflyDustRate,
                            Shape = LumenShapeKind.Sphere,
                            ShapeExtents = new NumVector3(0.03f, 0.03f, 0.03f),
                            StartLifetimeMin = def.butterflyDustLifetimeMin,
                            StartLifetimeMax = def.butterflyDustLifetimeMax,
                            StartSpeedMin = 0.03f,
                            StartSpeedMax = 0.11f,
                            StartSizeMin = 0.020f,
                            StartSizeMax = 0.038f,
                            BlendMode = LumenBlendMode.Additive,
                            BillboardMode = LumenBillboardMode.Camera,
                            SoftParticleFadeDistance = 0.05f,
                            HdrIntensity = def.butterflyHdrMultiplier,
                            SizeOverLifetime = new ScalarCurve(new[]
                            {
                                new CurveKey(0f, 0.65f),
                                new CurveKey(0.35f, 1f),
                                new CurveKey(1f, 0f)
                            }),
                            ColorOverLifetime = new ColorGradient(new[]
                            {
                                new ColorStop(0f, ToVec4(bright, 0.90f)),
                                new ColorStop(0.42f, ToVec4(main, 0.68f)),
                                new ColorStop(1f, ToVec4(deep, 0f))
                            }),
                            ForceFields = new[]
                            {
                                new ForceFieldSpec(ForceFieldKind.GravityDrag, new NumVector3(0f, 1f, 0f), new NumVector3(3.8f, 0.08f, 0f), 0f, 0f),
                                new ForceFieldSpec(ForceFieldKind.CurlTurbulence, NumVector3.Zero, NumVector3.Zero, 0f, 0.25f, 1.5f)
                            },
                            EnvironmentChannels = LumenEnvironmentChannel.None
                        }
                    }
                }
            });
            return effectId;
        }

        private static NumVector4 ToVec4(Color c, float alpha) => new NumVector4(c.r, c.g, c.b, alpha);
    }

    public sealed class StreetLampLumenVfxWorker : VfxWorker
    {
        private enum ButterflyPhase
        {
            Cooldown,
            Orbit,
            Flee
        }

        // Wing crystal facet geometry (side profile: +X head, -X swept wings, canvas Y-down offsets)
        private static readonly Vector2[] BodyPolygon =
        {
            new Vector2(3.4f, -0.3f),
            new Vector2(1.4f, -1.1f),
            new Vector2(-1.0f, -0.8f),
            new Vector2(-5.2f, 0.4f),
            new Vector2(-1.0f, 1.0f),
            new Vector2(1.6f, 0.9f)
        };

        private static readonly Vector2[] HindFacet1 =
        {
            new Vector2(-0.6f, 0.2f), new Vector2(-6.8f, -1.2f), new Vector2(-11.6f, -3.6f), new Vector2(-4.4f, -0.2f)
        };

        private static readonly Vector2[] HindFacet2 =
        {
            new Vector2(-0.6f, 0.2f), new Vector2(-4.4f, -0.2f), new Vector2(-11.6f, -3.6f), new Vector2(-8.8f, 2.2f), new Vector2(-3.6f, 1.8f)
        };

        private static readonly Vector2[] ForeFacet1 =
        {
            new Vector2(0f, 0f), new Vector2(-2.4f, -7.4f), new Vector2(-13.0f, -13.6f), new Vector2(-6.0f, -4.6f)
        };

        private static readonly Vector2[] ForeFacet2 =
        {
            new Vector2(0f, 0f), new Vector2(-6.0f, -4.6f), new Vector2(-13.0f, -13.6f), new Vector2(-11.0f, -5.8f), new Vector2(-5.8f, -1.5f)
        };

        private static readonly Vector2[] ForeFacet3 =
        {
            new Vector2(0f, 0f), new Vector2(-5.8f, -1.5f), new Vector2(-11.0f, -5.8f), new Vector2(-0.4f, 0.5f)
        };

        private static readonly Vector2[] WingOutline =
        {
            new Vector2(0f, 0f), new Vector2(-2.4f, -7.4f), new Vector2(-13.0f, -13.6f),
            new Vector2(-11.0f, -5.8f), new Vector2(-5.8f, -1.5f), new Vector2(-11.6f, -3.6f),
            new Vector2(-8.8f, 2.2f), new Vector2(-3.6f, 1.8f), new Vector2(-0.6f, 0.2f)
        };

        private StreetLampLumenVfxDef def;
        private Map map;
        private Vector3 anchorPos;
        private CompElvenStreetLamp lampComp;

        private Color dayLightColor;
        private Color nightLightColor;
        private Color dayCoreColor;
        private Color nightCoreColor;
        private Color butterflyBrightColor;
        private Color butterflyMainColor;
        private Color butterflyDeepColor;
        private Color butterflyEdgeColor;

        private float clockSeconds;
        private float nightFactor;
        private float lightFadeFactor;

        // Butterfly state
        private ButterflyPhase butterflyPhase = ButterflyPhase.Cooldown;
        private float respawnTimer;
        private float orbitAngle;
        private float bx;
        private float by;
        private float bz;
        private float tx;
        private float ty;
        private float tz;
        private float bvx;
        private float bvy;
        private float bvz;
        private float waypointTimer;
        private float hoverTimer;
        private float seedA;
        private float seedB;
        private int facingX = 1;
        private float wingPhase;
        private float spawnAge;
        private float fleeAge;
        private float butterflyAlpha;
        private float butterflyDim;

        private LumenRibbonMesh additiveMesh;
        private LumenRibbonMesh facetMesh;
        private ILumenEffectHandle lightHandle;
        private ILumenEffectHandle dustHandle;
        private readonly List<Vector3> scratchPoly = new List<Vector3>(8);

        private float ButterflyDisplayX => bx;

        private float ButterflyDisplayZ => anchorPos.z
            + (bz - anchorPos.z) * LumenRibbonMesh.Ortho60AspectZ
            + by * LumenRibbonMesh.Ortho60HeightZ;

        protected override void OnStart()
        {
            def = Def as StreetLampLumenVfxDef;
            if (def == null)
            {
                throw new InvalidOperationException("StreetLampLumenVfxWorker requires StreetLampLumenVfxDef");
            }

            map = Request.map;
            anchorPos = Request.follow != null && Request.follow.Spawned ? Request.follow.DrawPos : Request.origin;
            lampComp = Request.follow?.TryGetComp<CompElvenStreetLamp>();

            dayLightColor = VfxColors.ParseUnity(def.dayLightColorHex);
            nightLightColor = VfxColors.ParseUnity(def.nightLightColorHex);
            dayCoreColor = VfxColors.ParseUnity(def.dayCoreColorHex);
            nightCoreColor = VfxColors.ParseUnity(def.nightCoreColorHex);
            butterflyBrightColor = VfxColors.ParseUnity(def.butterflyBrightColorHex);
            butterflyMainColor = VfxColors.ParseUnity(def.butterflyMainColorHex);
            butterflyDeepColor = VfxColors.ParseUnity(def.butterflyDeepColorHex);
            butterflyEdgeColor = VfxColors.ParseUnity(def.butterflyEdgeColorHex);

            bool isLit = lampComp?.IsLit ?? false;
            lightFadeFactor = isLit ? 1f : 0f;
            nightFactor = EvaluateCurrentNightFactor();
            respawnTimer = def.butterflyInitialDelaySeconds;

            additiveMesh = new LumenRibbonMesh("WotF_StreetLamp_Additive");
            facetMesh = new LumenRibbonMesh("WotF_StreetLamp_ButterflyFacets");

            if (RimLumenApi.IsReady)
            {
                lightHandle = RimLumenApi.Play(new LumenPlayRequest
                {
                    EffectId = StreetLampLumenCatalog.EnsureLampLightEffect(def),
                    MapIndex = map != null ? map.uniqueID : -1,
                    OriginWorld = new NumVector3(anchorPos.x, 0f, anchorPos.z),
                    Follow = FollowAnchor,
                    RealTime = false
                });
                dustHandle = RimLumenApi.Play(new LumenPlayRequest
                {
                    EffectId = StreetLampLumenCatalog.EnsureButterflyDustEffect(def),
                    MapIndex = map != null ? map.uniqueID : -1,
                    OriginWorld = new NumVector3(anchorPos.x, 0f, anchorPos.z),
                    Follow = FollowButterfly,
                    RealTime = false
                });
                dustHandle?.SetParam("rate", 0f);
                PushLightHandleState(isLit);
            }
        }

        public override void Update(float deltaSeconds)
        {
            if (Finished)
            {
                return;
            }

            if (Request.follow != null && Request.follow.Spawned && Request.follow.Map == map)
            {
                anchorPos = Request.follow.DrawPos;
            }

            bool isLit = lampComp?.IsLit ?? false;
            nightFactor = EvaluateCurrentNightFactor();

            if (deltaSeconds > 0f)
            {
                clockSeconds += deltaSeconds;
                lightFadeFactor = Mathf.MoveTowards(lightFadeFactor, isLit ? 1f : 0f, deltaSeconds / def.lightFadeSeconds);
                AdvanceButterfly(deltaSeconds, isLit);
            }

            PushLightHandleState(isLit);
            if (dustHandle != null && dustHandle.IsAlive)
            {
                dustHandle.SetParam("rate", butterflyPhase == ButterflyPhase.Orbit ? def.butterflyDustRate * butterflyAlpha : 0f);
            }
        }

        public override void DrawWorld()
        {
            if (Finished || additiveMesh == null || facetMesh == null || map != Find.CurrentMap)
            {
                return;
            }

            additiveMesh.Clear();
            facetMesh.Clear();

            float drawAlt = Altitudes.AltitudeFor(AltitudeLayer.MoteOverhead);
            if (lightFadeFactor > 0.005f)
            {
                DrawGlobeInteriorHalos(drawAlt);
            }

            if (butterflyAlpha > 0.01f && butterflyPhase != ButterflyPhase.Cooldown)
            {
                DrawCrystalButterfly(drawAlt);
            }

            additiveMesh.Upload();
            if (additiveMesh.HasGeometry)
            {
                LumenShaders.DrawLumen(additiveMesh.Mesh, LumenShaders.RibbonOver);
            }

            facetMesh.Upload();
            if (facetMesh.HasGeometry)
            {
                LumenShaders.DrawLumen(facetMesh.Mesh, LumenShaders.FacetOver);
            }
        }

        protected override void Release()
        {
            if (lightHandle != null)
            {
                lightHandle.Stop(allowFadeOut: false);
                lightHandle = null;
            }

            if (dustHandle != null)
            {
                dustHandle.Stop(allowFadeOut: false);
                dustHandle = null;
            }

            additiveMesh?.Dispose();
            additiveMesh = null;
            facetMesh?.Dispose();
            facetMesh = null;
        }

        private float EvaluateCurrentNightFactor()
        {
            return map?.skyManager != null
                ? StreetLampVfxMath.EvaluateNightFactor(map.skyManager.CurSkyGlow, def.nightSkyGlowHigh, def.nightSkyGlowLow)
                : 0f;
        }

        private void PushLightHandleState(bool isLit)
        {
            if (lightHandle == null || !lightHandle.IsAlive)
            {
                return;
            }

            Color poolCol = Color.Lerp(dayLightColor, nightLightColor, nightFactor);
            lightHandle.SetLightColor(new NumVector3(
                Mathf.Clamp01(poolCol.r),
                Mathf.Clamp01(poolCol.g),
                Mathf.Clamp01(poolCol.b)));

            float breath = 1f + 0.03f * Mathf.Sin(clockSeconds * def.globeBreathSpeed);
            lightHandle.SetLightIntensity(lightFadeFactor * (isLit ? breath : 1f));
        }

        private void AdvanceButterfly(float dt, bool isLit)
        {
            if (butterflyPhase == ButterflyPhase.Cooldown)
            {
                if (isLit && nightFactor >= def.butterflyNightThreshold)
                {
                    respawnTimer -= dt;
                    if (respawnTimer <= 0f)
                    {
                        SpawnButterfly();
                    }
                }

                return;
            }

            if (butterflyPhase == ButterflyPhase.Orbit)
            {
                spawnAge += dt;
                waypointTimer -= dt;
                if (hoverTimer > 0f)
                {
                    hoverTimer -= dt;
                }

                if (waypointTimer <= 0f)
                {
                    bool isPause = Rand.Value < 0.36f;
                    if (isPause)
                    {
                        hoverTimer = Rand.Range(1.0f, 2.4f);
                        waypointTimer = hoverTimer + 0.25f;
                    }
                    else
                    {
                        float turnSign = Rand.Value < 0.42f ? -1f : 1f;
                        orbitAngle += turnSign * Rand.Range(0.45f, 1.80f);
                        float r = Rand.Range(def.butterflyOrbitRadiusMin, def.butterflyOrbitRadiusMax);
                        float h = Rand.Range(def.butterflyHeightMin, def.butterflyHeightMax);
                        tx = anchorPos.x + Mathf.Cos(orbitAngle) * r;
                        tz = anchorPos.z + Mathf.Sin(orbitAngle) * r;
                        ty = h;
                        waypointTimer = Rand.Range(1.8f, 4.0f);
                    }
                }

                float dxT = tx - bx;
                float dyT = ty - by;
                float dzT = tz - bz;
                float distT = Mathf.Sqrt(dxT * dxT + dyT * dyT + dzT * dzT);
                if (distT < 0.08f && hoverTimer <= 0f && waypointTimer > 0.35f)
                {
                    waypointTimer = Mathf.Min(waypointTimer, 0.35f);
                }

                float steerGain = hoverTimer > 0f ? def.butterflyHoverSteerGain : def.butterflySteerGain;
                float flutX = Mathf.Sin(clockSeconds * 1.9f + seedA) * 0.24f + Mathf.Cos(clockSeconds * 3.4f + seedB) * 0.14f;
                float flutZ = Mathf.Cos(clockSeconds * 1.7f + seedB) * 0.20f + Mathf.Sin(clockSeconds * 3.6f + seedA) * 0.12f;
                float wingLift = -Mathf.Sin(wingPhase) * 0.26f;
                float flutY = Mathf.Sin(clockSeconds * 2.3f + seedA * 0.7f) * 0.18f + wingLift;

                float damp = Mathf.Exp(-def.butterflyDamping * dt);
                bvx = (bvx + (dxT * steerGain + flutX) * dt) * damp;
                bvy = (bvy + (dyT * steerGain + flutY) * dt) * damp;
                bvz = (bvz + (dzT * steerGain + flutZ) * dt) * damp;

                // Soft pole repulsion so flight glides around the iron shaft cleanly
                float rx = bx - anchorPos.x;
                float rz = bz - anchorPos.z;
                float rDist = Mathf.Sqrt(rx * rx + rz * rz);
                if (rDist < 0.001f)
                {
                    rDist = 0.001f;
                }

                if (rDist < def.butterflyOrbitRadiusMin)
                {
                    float push = (def.butterflyOrbitRadiusMin - rDist) * 8.0f * dt;
                    bvx += (rx / rDist) * push;
                    bvz += (rz / rDist) * push;
                }
                else if (rDist > def.butterflyOrbitRadiusMax + 0.04f)
                {
                    float pull = (rDist - (def.butterflyOrbitRadiusMax + 0.04f)) * 6.0f * dt;
                    bvx -= (rx / rDist) * pull;
                    bvz -= (rz / rDist) * pull;
                }

                bx += bvx * dt;
                by = Mathf.Clamp(by + bvy * dt, def.butterflyHeightMin - 0.13f, def.butterflyHeightMax + 0.10f);
                bz += bvz * dt;

                float spd = Mathf.Sqrt(bvx * bvx + bvy * bvy + bvz * bvz);
                if (Mathf.Abs(bvx) > 0.045f)
                {
                    facingX = bvx >= 0f ? 1 : -1;
                }

                wingPhase += dt * (hoverTimer > 0f ? 6.8f : (7.8f + Mathf.Min(4.8f, spd * 9.0f)));
                butterflyAlpha = Mathf.Clamp01(spawnAge / 0.45f);
                butterflyDim = 1f;

                bool cursorClose = false;
                Vector2 cursorXZ = default;
                if (IsPointerActiveOnMap(out Vector3 mouseWorld))
                {
                    cursorXZ = new Vector2(mouseWorld.x, mouseWorld.z);
                    cursorClose = StreetLampVfxMath.ShouldStartleButterfly(
                        cursorXZ,
                        new Vector2(ButterflyDisplayX, ButterflyDisplayZ),
                        def.butterflyFleeRadius);
                }

                if (!isLit || nightFactor < def.butterflyNightThreshold || cursorClose)
                {
                    BeginButterflyFlee(cursorClose, cursorXZ);
                }
            }
            else if (butterflyPhase == ButterflyPhase.Flee)
            {
                fleeAge += dt;
                float u = Mathf.Clamp01(fleeAge / def.butterflyFleeDurationSeconds);
                float flutter = Mathf.Sin(fleeAge * 10.5f) * 0.55f;
                float perpX = -bvz * 0.28f * flutter;
                float perpZ = bvx * 0.28f * flutter;
                bx += (bvx + perpX) * dt;
                by += bvy * dt;
                bz += (bvz + perpZ) * dt;
                bvy += 0.85f * dt;
                float fleeDamp = Mathf.Exp(-0.55f * dt);
                bvx *= fleeDamp;
                bvz *= fleeDamp;

                if (Mathf.Abs(bvx) > 0.045f)
                {
                    facingX = bvx >= 0f ? 1 : -1;
                }

                wingPhase += dt * 18.5f;
                butterflyDim = Mathf.Pow(1f - u, 1.45f);
                butterflyAlpha = Mathf.Pow(1f - u, 1.15f);

                if (fleeAge >= def.butterflyFleeDurationSeconds)
                {
                    butterflyPhase = ButterflyPhase.Cooldown;
                    respawnTimer = def.butterflyRespawnSeconds;
                    butterflyAlpha = 0f;
                    butterflyDim = 0f;
                }
            }
        }

        private void SpawnButterfly()
        {
            butterflyPhase = ButterflyPhase.Orbit;
            orbitAngle = Rand.Range(0f, Mathf.PI * 2f);
            float r = Mathf.Lerp(def.butterflyOrbitRadiusMin, def.butterflyOrbitRadiusMax, 0.35f);
            float h = Mathf.Lerp(def.butterflyHeightMin, def.butterflyHeightMax, 0.5f);
            bx = anchorPos.x + Mathf.Cos(orbitAngle) * r;
            bz = anchorPos.z + Mathf.Sin(orbitAngle) * r;
            by = h;
            tx = bx;
            ty = by;
            tz = bz;
            bvx = 0f;
            bvy = 0f;
            bvz = 0f;
            waypointTimer = 0.25f;
            hoverTimer = 0f;
            seedA = Rand.Range(0f, 100f);
            seedB = Rand.Range(0f, 100f);
            facingX = -Mathf.Sin(orbitAngle) >= 0f ? 1 : -1;
            wingPhase = Rand.Range(0f, Mathf.PI * 2f);
            spawnAge = 0f;
            fleeAge = 0f;
            butterflyAlpha = 0f;
            butterflyDim = 1f;
        }

        private void BeginButterflyFlee(bool startledByCursor, Vector2 cursorXZ)
        {
            butterflyPhase = ButterflyPhase.Flee;
            fleeAge = 0f;
            float fx = startledByCursor ? ButterflyDisplayX - cursorXZ.x : Mathf.Cos(orbitAngle);
            float fz = startledByCursor ? ButterflyDisplayZ - cursorXZ.y : Mathf.Sin(orbitAngle);
            float flen = Mathf.Sqrt(fx * fx + fz * fz);
            if (flen < 0.001f)
            {
                fx = Mathf.Cos(orbitAngle);
                fz = Mathf.Sin(orbitAngle);
                flen = 1f;
            }

            bvx = (fx / flen) * def.butterflyFleeSpeed;
            bvz = (fz / flen) * def.butterflyFleeSpeed;
            bvy = 0.95f;
            if (Mathf.Abs(bvx) > 0.045f)
            {
                facingX = bvx >= 0f ? 1 : -1;
            }
        }

        private bool IsPointerActiveOnMap(out Vector3 mouseWorld)
        {
            mouseWorld = default;
            if (Current.ProgramState != ProgramState.Playing || map == null || Find.CurrentMap != map || !WorldRendererUtility.DrawingMap)
            {
                return false;
            }

            if (Find.WindowStack?.GetWindowAt(UI.MousePositionOnUIInverted) != null)
            {
                return false;
            }

            mouseWorld = UI.MouseMapPosition();
            return true;
        }

        private void DrawGlobeInteriorHalos(float drawAlt)
        {
            Color coreRgb = Color.Lerp(dayCoreColor, nightCoreColor, nightFactor);
            Color glassRgb = Color.Lerp(dayLightColor, nightLightColor, nightFactor);

            DrawSingleGlobe(drawAlt, def.globe1Offset, def.globe1Radius, 0f, coreRgb, glassRgb);
            DrawSingleGlobe(drawAlt, def.globe2Offset, def.globe2Radius, 1.7f, coreRgb, glassRgb);
        }

        private void DrawSingleGlobe(float drawAlt, Vector3 offset, Vector2 radius, float breathPhase, Color coreRgb, Color glassRgb)
        {
            float breath = 1f - def.globeBreathAmp * (0.5f - 0.5f * Mathf.Sin(clockSeconds * def.globeBreathSpeed + breathPhase));
            float master = lightFadeFactor * breath;
            float hdr = def.globeHdrMultiplier * (0.92f + 0.25f * nightFactor);

            var center = new Vector3(anchorPos.x + offset.x, drawAlt + 0.02f, anchorPos.z + offset.z);

            // Soft rear scatter behind translucent globe below flower petals
            additiveMesh.AddRadialGlowDisc(
                new Vector3(center.x, drawAlt - 0.01f, center.z - radius.y * 0.40f),
                Mathf.Max(radius.x, radius.y) * 4.8f,
                16,
                new Color(glassRgb.r, glassRgb.g, glassRgb.b, (0.14f + 0.16f * nightFactor) * master),
                new Color(glassRgb.r, glassRgb.g, glassRgb.b, 0f),
                hdr * 0.85f,
                1.5f,
                null);

            // Scalloped flower-cap-clipped inside-globe cavity core
            additiveMesh.AddLampGlobeCavityGlow(
                center,
                radius.x,
                radius.y,
                def.globeCapDipFactor,
                def.globeCapSideLiftFactor,
                def.globeBottomScale,
                14,
                new Color(Mathf.Lerp(coreRgb.r, 1f, 0.55f), Mathf.Lerp(coreRgb.g, 1f, 0.55f), Mathf.Lerp(coreRgb.b, 1f, 0.45f), (0.62f + 0.22f * nightFactor) * master),
                new Color(glassRgb.r, glassRgb.g, glassRgb.b, 0f),
                hdr * 1.28f,
                1.25f);

            // Faint downward glass refraction caustics under lower rim
            additiveMesh.AddRadialGlowDisc(
                new Vector3(center.x, drawAlt + 0.01f, center.z - radius.y * 1.05f),
                radius.x * 1.32f,
                14,
                new Color(glassRgb.r, glassRgb.g, glassRgb.b, (0.16f + 0.12f * nightFactor) * master),
                new Color(glassRgb.r, glassRgb.g, glassRgb.b, 0f),
                hdr * 0.9f,
                1.6f,
                null);
        }

        private void DrawCrystalButterfly(float drawAlt)
        {
            float cx = ButterflyDisplayX;
            float cz = ButterflyDisplayZ;
            float s = def.butterflyVisualScale;
            float dim = butterflyDim;
            float pitchRad = butterflyPhase == ButterflyPhase.Flee ? -0.34f : -0.14f;
            float cosP = Mathf.Cos(pitchRad);
            float sinP = Mathf.Sin(pitchRad);

            float swingFront = Mathf.Lerp(-0.36f, 1.08f, 0.5f + 0.5f * Mathf.Sin(wingPhase));
            float swingBack = Mathf.Lerp(-0.32f, 1.02f, 0.5f + 0.5f * Mathf.Sin(wingPhase - 0.38f));

            Color dimTint = new Color(0.11f, 0.30f, 0.29f);
            Color crystalBright = Color.Lerp(dimTint, butterflyBrightColor, dim);
            Color crystalMain = Color.Lerp(dimTint * 0.72f, butterflyMainColor, dim);
            Color crystalDeep = Color.Lerp(dimTint * 0.52f, butterflyDeepColor, dim);
            Color crystalFar = Color.Lerp(dimTint * 0.45f, Color.Lerp(butterflyDeepColor, butterflyMainColor, 0.22f), dim);
            Color ridgeCol = Color.Lerp(dimTint * 1.1f, butterflyBrightColor, dim);
            Color edgeCol = Color.Lerp(new Color(0.05f, 0.09f, 0.09f), butterflyEdgeColor, dim);
            Color bodyCol = Color.Lerp(new Color(0.09f, 0.13f, 0.13f), new Color(0.23f, 0.65f, 0.60f), dim);

            // Tight cyan jewel aura behind butterfly (dims & vanishes smoothly on flee)
            if (dim > 0.04f)
            {
                additiveMesh.AddRadialGlowDisc(
                    new Vector3(cx, drawAlt + 0.02f, cz),
                    14f * s * (0.55f + 0.45f * dim),
                    14,
                    new Color(crystalBright.r, crystalBright.g, crystalBright.b, 0.58f * butterflyAlpha * dim),
                    new Color(crystalMain.r, crystalMain.g, crystalMain.b, 0f),
                    def.butterflyHdrMultiplier * (0.7f + 0.6f * dim),
                    1.45f,
                    null);
            }

            float hdr = 1f + 0.42f * dim;
            float facetAlt = drawAlt + 0.04f;

            // 1. Far side crystal wing
            AddSideCrystalWing(cx, cz, facetAlt, s, cosP, sinP, swingBack, true, hdr, crystalBright, crystalMain, crystalFar, crystalDeep, ridgeCol, edgeCol);

            // 2. Horizontal diamond crystal body & forward antennae
            TransformPolygon(BodyPolygon, cx, cz, facetAlt, s, cosP, sinP, 0f, 0f, 1f);
            facetMesh.AddSolidPolygonFan(scratchPoly, new Color(bodyCol.r, bodyCol.g, bodyCol.b, 0.96f * butterflyAlpha), hdr);
            AddTransformedOutline(scratchPoly, true, 0.8f * s, new Color(edgeCol.r, edgeCol.g, edgeCol.b, 0.92f * butterflyAlpha), hdr);

            Vector3 a0 = TransformPoint(3.0f, -0.5f, cx, cz, facetAlt, s, cosP, sinP, 0f, 0f, 1f);
            Vector3 a1 = TransformPoint(6.4f, -3.2f, cx, cz, facetAlt, s, cosP, sinP, 0f, 0f, 1f);
            Vector3 a2 = TransformPoint(7.4f, -3.4f, cx, cz, facetAlt, s, cosP, sinP, 0f, 0f, 1f);
            Vector3 b0 = TransformPoint(3.1f, -0.3f, cx, cz, facetAlt, s, cosP, sinP, 0f, 0f, 1f);
            Vector3 b1 = TransformPoint(6.8f, -1.8f, cx, cz, facetAlt, s, cosP, sinP, 0f, 0f, 1f);
            Color antCol = new Color(crystalBright.r, crystalBright.g, crystalBright.b, 0.84f * butterflyAlpha);
            facetMesh.AddSolidLine(a0, a1, 0.65f * s, antCol, hdr);
            facetMesh.AddSolidLine(a1, a2, 0.65f * s, antCol, hdr);
            facetMesh.AddSolidLine(b0, b1, 0.65f * s, antCol, hdr);

            // 3. Near side sharp faceted crystal wing
            AddSideCrystalWing(cx, cz, facetAlt + 0.01f, s, cosP, sinP, swingFront, false, hdr, crystalBright, crystalMain, crystalFar, crystalDeep, ridgeCol, edgeCol);
        }

        private void AddSideCrystalWing(
            float cx,
            float cz,
            float altY,
            float s,
            float cosP,
            float sinP,
            float swingY,
            bool isBackWing,
            float hdr,
            Color crystalBright,
            Color crystalMain,
            Color crystalFar,
            Color crystalDeep,
            Color ridgeCol,
            Color edgeCol)
        {
            float ox = isBackWing ? -1.0f : 0.3f;
            float oy = isBackWing ? -0.65f : -0.45f;
            float alphaMul = (isBackWing ? 0.72f : 0.94f) * 0.90f * butterflyAlpha;
            Color hiCol = isBackWing ? crystalMain : crystalBright;
            Color midCol = isBackWing ? crystalFar : crystalMain;
            Color loCol = isBackWing ? crystalFar : crystalDeep;

            TransformPolygon(HindFacet1, cx, cz, altY, s, cosP, sinP, ox, oy, swingY);
            facetMesh.AddSolidPolygonFan(scratchPoly, new Color(midCol.r, midCol.g, midCol.b, alphaMul), hdr);
            TransformPolygon(HindFacet2, cx, cz, altY, s, cosP, sinP, ox, oy, swingY);
            facetMesh.AddSolidPolygonFan(scratchPoly, new Color(loCol.r, loCol.g, loCol.b, alphaMul), hdr);

            TransformPolygon(ForeFacet1, cx, cz, altY, s, cosP, sinP, ox, oy, swingY);
            facetMesh.AddSolidPolygonFan(scratchPoly, new Color(hiCol.r, hiCol.g, hiCol.b, alphaMul), hdr);
            TransformPolygon(ForeFacet2, cx, cz, altY, s, cosP, sinP, ox, oy, swingY);
            facetMesh.AddSolidPolygonFan(scratchPoly, new Color(midCol.r, midCol.g, midCol.b, alphaMul), hdr);
            TransformPolygon(ForeFacet3, cx, cz, altY, s, cosP, sinP, ox, oy, swingY);
            facetMesh.AddSolidPolygonFan(scratchPoly, new Color(loCol.r, loCol.g, loCol.b, alphaMul), hdr);

            // Sharp crystalline veins
            Color vCol = new Color(ridgeCol.r, ridgeCol.g, ridgeCol.b, (isBackWing ? 0.42f : 0.78f) * butterflyAlpha);
            Vector3 f0 = TransformPoint(0f, 0f, cx, cz, altY, s, cosP, sinP, ox, oy, swingY);
            Vector3 f1 = TransformPoint(-12.6f, -13.2f, cx, cz, altY, s, cosP, sinP, ox, oy, swingY);
            Vector3 h0 = TransformPoint(-0.6f, 0.2f, cx, cz, altY, s, cosP, sinP, ox, oy, swingY);
            Vector3 h1 = TransformPoint(-11.2f, -3.4f, cx, cz, altY, s, cosP, sinP, ox, oy, swingY);
            facetMesh.AddSolidLine(f0, f1, 0.72f * s, vCol, hdr);
            facetMesh.AddSolidLine(h0, h1, 0.72f * s, vCol, hdr);

            // Crisp wing facet border
            TransformPolygon(WingOutline, cx, cz, altY, s, cosP, sinP, ox, oy, swingY);
            AddTransformedOutline(scratchPoly, false, 0.85f * s, new Color(edgeCol.r, edgeCol.g, edgeCol.b, (isBackWing ? 0.65f : 0.90f) * butterflyAlpha), hdr);
        }

        private void TransformPolygon(
            Vector2[] localPts,
            float cx,
            float cz,
            float altY,
            float s,
            float cosP,
            float sinP,
            float wingOffsetX,
            float wingOffsetY,
            float swingY)
        {
            scratchPoly.Clear();
            for (int i = 0; i < localPts.Length; i++)
            {
                scratchPoly.Add(TransformPoint(localPts[i].x, localPts[i].y, cx, cz, altY, s, cosP, sinP, wingOffsetX, wingOffsetY, swingY));
            }
        }

        private void AddTransformedOutline(IReadOnlyList<Vector3> pts, bool closed, float width, Color color, float hdr)
        {
            for (int i = 0; i < pts.Count - 1; i++)
            {
                facetMesh.AddSolidLine(pts[i], pts[i + 1], width, color, hdr);
            }

            if (closed && pts.Count > 2)
            {
                facetMesh.AddSolidLine(pts[pts.Count - 1], pts[0], width, color, hdr);
            }
        }

        private Vector3 TransformPoint(
            float px,
            float py,
            float cx,
            float cz,
            float altY,
            float s,
            float cosP,
            float sinP,
            float wingOffsetX,
            float wingOffsetY,
            float swingY)
        {
            float lx = px + wingOffsetX;
            float ly = py * swingY + wingOffsetY;
            float rx = lx * cosP - ly * sinP;
            float ry = lx * sinP + ly * cosP;
            // Canvas Y-down maps to RimWorld -Z so north (+Z) points upward on screen.
            return new Vector3(cx + rx * facingX * s, altY, cz - ry * s);
        }

        private bool FollowAnchor(out NumVector3 worldPosition, out float rotationDegrees)
        {
            rotationDegrees = 0f;
            if (!Finished && Request.follow != null && Request.follow.Spawned && Request.follow.Map == map)
            {
                anchorPos = Request.follow.DrawPos;
                worldPosition = new NumVector3(anchorPos.x, 0f, anchorPos.z);
                return true;
            }

            worldPosition = new NumVector3(anchorPos.x, 0f, anchorPos.z);
            return false;
        }

        private bool FollowButterfly(out NumVector3 worldPosition, out float rotationDegrees)
        {
            rotationDegrees = 0f;
            if (!Finished && Request.follow != null && Request.follow.Spawned && Request.follow.Map == map)
            {
                worldPosition = new NumVector3(ButterflyDisplayX, 0f, ButterflyDisplayZ);
                return true;
            }

            worldPosition = new NumVector3(ButterflyDisplayX, 0f, ButterflyDisplayZ);
            return false;
        }
    }
}
