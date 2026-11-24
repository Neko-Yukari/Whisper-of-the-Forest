using System;
using System.Collections.Generic;
using RimLumen.Core.Api;
using RimLumen.Core.Contracts;
using UnityEngine;
using Verse;
using Verse.Sound;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Vfx;
using CurveKey = RimLumen.Core.Contracts.CurveKey;
using NumVector3 = System.Numerics.Vector3;
using NumVector4 = System.Numerics.Vector4;

namespace WhisperOfTheForest.Missiles
{
    public sealed class ArcaneMissileRimLumenVfxDef : ArcaneMissileVisualDef
    {
        public float headCoreRadiusFactor;
        public float headHaloRadiusFactor;
        public float headStretchLengthFactor;
        public float trailWidthFactor;
        public float trailMinVertexDistance;
        public int trailMaxPoints;
        public float wakeRatePerSecond;
        public float wakeLifetimeMin;
        public float wakeLifetimeMax;
        public float wakeSizeMinFactor;
        public float wakeSizeMaxFactor;
        public float wakeDrag;
        public float wakeTurbulence;
        public float lightRadius;
        public float lightHeightCells;
        public float lightIntensity;
        public float hdrHeadMultiplier;
        public float hdrTrailMultiplier;
        public float hdrWakeMultiplier;
        public float ribbonEdgePower;
        public float drainFadeSeconds;
        public float drainDurationSeconds;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (headCoreRadiusFactor <= 0f || headHaloRadiusFactor <= 0f || headStretchLengthFactor <= 0f)
            {
                yield return "headCoreRadiusFactor, headHaloRadiusFactor and headStretchLengthFactor must be positive";
            }

            if (trailWidthFactor <= 0f || trailMinVertexDistance <= 0f || trailMaxPoints < 2)
            {
                yield return "trailWidthFactor > 0, trailMinVertexDistance > 0 and trailMaxPoints >= 2 are required";
            }

            if (wakeRatePerSecond < 0f || wakeLifetimeMin <= 0f || wakeLifetimeMax < wakeLifetimeMin)
            {
                yield return "wakeRatePerSecond >= 0 and 0 < wakeLifetimeMin <= wakeLifetimeMax are required";
            }

            if (wakeSizeMinFactor <= 0f || wakeSizeMaxFactor < wakeSizeMinFactor || wakeDrag < 0f || wakeTurbulence < 0f)
            {
                yield return "wake size factors must be positive and wakeDrag/wakeTurbulence non-negative";
            }

            if (lightRadius <= 0f || lightHeightCells < 1f || lightIntensity <= 0f)
            {
                yield return "lightRadius > 0, lightHeightCells >= 1 and lightIntensity > 0 are required";
            }

            if (hdrHeadMultiplier <= 0f || hdrTrailMultiplier <= 0f || hdrWakeMultiplier <= 0f || ribbonEdgePower <= 0f)
            {
                yield return "hdr multipliers and ribbonEdgePower must be positive";
            }

            if (drainFadeSeconds <= 0f || drainDurationSeconds < drainFadeSeconds)
            {
                yield return "0 < drainFadeSeconds <= drainDurationSeconds is required";
            }
        }
    }

    public sealed class ArcaneMissileImpactRimLumenVfxDef : VfxDef
    {
        public SoundDef touchSound;
        public float flashDurationSeconds;
        public float holdDurationSeconds;
        public float flashPeakRadiusFactor;
        public int sparkBurstCount;
        public float sparkSpeedMin;
        public float sparkSpeedMax;
        public float sparkLifetimeMin;
        public float sparkLifetimeMax;
        public float sparkSizeMinFactor;
        public float sparkSizeMaxFactor;
        public float sparkDrag;
        public int emberBurstCount;
        public float emberSpeedMin;
        public float emberSpeedMax;
        public float emberLifetimeMin;
        public float emberLifetimeMax;
        public float emberSizeMinFactor;
        public float emberSizeMaxFactor;
        public float emberDrag;
        public float emberTurbulence;
        public float distortionRadius;
        public float distortionStrength;
        public float distortionThickness;
        public float distortionSpeed;
        public float distortionDurationSeconds;
        public float lightRadius;
        public float lightHeightCells;
        public float lightPeakIntensity;
        public float lightDurationSeconds;
        public float hdrFlashMultiplier;
        public float hdrSparkMultiplier;
        public float hdrEmberMultiplier;
        public float ribbonEdgePower;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (flashDurationSeconds <= 0f || holdDurationSeconds < flashDurationSeconds || flashPeakRadiusFactor <= 0f)
            {
                yield return "flashDurationSeconds > 0, holdDurationSeconds >= flashDurationSeconds and flashPeakRadiusFactor > 0 are required";
            }

            if (sparkBurstCount < 1 || sparkSpeedMin <= 0f || sparkSpeedMax < sparkSpeedMin)
            {
                yield return "sparkBurstCount >= 1 and 0 < sparkSpeedMin <= sparkSpeedMax are required";
            }

            if (sparkLifetimeMin <= 0f || sparkLifetimeMax < sparkLifetimeMin
                || sparkSizeMinFactor <= 0f || sparkSizeMaxFactor < sparkSizeMinFactor || sparkDrag < 0f)
            {
                yield return "spark lifetime/size ranges must be positive and sparkDrag non-negative";
            }

            if (emberBurstCount < 1 || emberSpeedMin < 0f || emberSpeedMax < emberSpeedMin
                || emberLifetimeMin <= 0f || emberLifetimeMax < emberLifetimeMin
                || emberSizeMinFactor <= 0f || emberSizeMaxFactor < emberSizeMinFactor
                || emberDrag < 0f || emberTurbulence < 0f)
            {
                yield return "ember burst count, lifetime/size ranges must be positive and speed/drag/turbulence non-negative";
            }

            if (distortionRadius <= 0f || distortionStrength <= 0f || distortionThickness <= 0f
                || distortionSpeed <= 0f || distortionDurationSeconds <= 0f)
            {
                yield return "distortion parameters must all be positive";
            }

            if (lightRadius <= 0f || lightHeightCells < 1f || lightPeakIntensity <= 0f || lightDurationSeconds <= 0f)
            {
                yield return "impact light parameters must be positive with lightHeightCells >= 1";
            }

            if (hdrFlashMultiplier <= 0f || hdrSparkMultiplier <= 0f || hdrEmberMultiplier <= 0f || ribbonEdgePower <= 0f)
            {
                yield return "hdr multipliers and ribbonEdgePower must be positive";
            }
        }
    }

    internal static class ArcaneMissileLumenCatalog
    {
        private static readonly HashSet<string> registeredIds = new HashSet<string>(StringComparer.Ordinal);

        public static string EnsureFlightEffect(ArcaneMissileRimLumenVfxDef def, SpellThemeDef theme, ArcaneMissileSettings settings)
        {
            int trailCentis = Mathf.Max(5, Mathf.RoundToInt(settings.trailTime * 100f));
            int sizeCentis = Mathf.Max(10, Mathf.RoundToInt(settings.missileSize * 100f));
            string effectId = $"NekoYukari.WhisperOfTheForest:DynamicMissile_{def.defName}_{theme.defName}_{trailCentis}_{sizeCentis}";
            if (registeredIds.Contains(effectId))
            {
                return effectId;
            }

            RimLumenApi.RegisterEffect(BuildFlightAsset(effectId, def, theme, settings));
            registeredIds.Add(effectId);
            return effectId;
        }

        public static EffectAsset BuildFlightAsset(
            string effectId,
            ArcaneMissileRimLumenVfxDef def,
            SpellThemeDef theme,
            ArcaneMissileSettings settings)
        {
            Color core = theme.CoreColorUnity;
            Color pri = theme.PrimaryColorUnity;
            Color sec = theme.SecondaryColorUnity;
            Color light = theme.LightColorUnity;
            float size = settings.missileSize;
            float hdr = theme.hdrIntensity;

            return new EffectAsset
            {
                EffectId = effectId,
                DefaultDuration = 2f,
                Nodes = new[]
                {
                    new EffectNodeSpec
                    {
                        Name = "missile_trail",
                        SpacePlane = LumenSpacePlane.Vertical3D,
                        LocalPosition = new NumVector3(0f, 0.04f, 0f),
                        Ribbon = new RibbonSpec
                        {
                            Mode = LumenRibbonMode.Trail,
                            Lifetime = Mathf.Max(0.06f, settings.trailTime),
                            MinVertexDistance = def.trailMinVertexDistance,
                            MaxPoints = def.trailMaxPoints,
                            Width = def.trailWidthFactor * size,
                            WidthOverLength = new ScalarCurve(new[]
                            {
                                new CurveKey(0f, 0.35f),
                                new CurveKey(0.06f, 1f),
                                new CurveKey(0.35f, 0.62f),
                                new CurveKey(0.75f, 0.24f),
                                new CurveKey(1f, 0f)
                            }),
                            ColorOverLength = new ColorGradient(new[]
                            {
                                new ColorStop(0f, ToVec4(core, 0.28f)),
                                new ColorStop(0.06f, ToVec4(core, 0.96f)),
                                new ColorStop(0.26f, ToVec4(pri, 0.84f)),
                                new ColorStop(0.72f, ToVec4(sec, 0.42f)),
                                new ColorStop(1f, ToVec4(sec, 0f))
                            }),
                            HdrIntensity = def.hdrTrailMultiplier * hdr,
                            BlendMode = LumenBlendMode.Additive,
                            Alignment = LumenRibbonAlignment.View,
                            SoftParticleFadeDistance = 0.08f
                        }
                    },
                    new EffectNodeSpec
                    {
                        Name = "wake_motes",
                        SpacePlane = LumenSpacePlane.Vertical3D,
                        LocalPosition = new NumVector3(0f, 0.04f, 0f),
                        Emitter = new EmitterSpec
                        {
                            Name = "wake_motes",
                            PreferredTier = LumenParticleTier.L2_GpuCompute,
                            MaxParticles = 96,
                            Duration = 1f,
                            Looping = true,
                            WorldSpaceSimulation = true,
                            RateOverTime = def.wakeRatePerSecond,
                            Shape = LumenShapeKind.Sphere,
                            ShapeExtents = new NumVector3(0.09f * size, 0.07f * size, 0.09f * size),
                            StartLifetimeMin = def.wakeLifetimeMin,
                            StartLifetimeMax = def.wakeLifetimeMax,
                            StartSpeedMin = 0.12f,
                            StartSpeedMax = 0.48f,
                            StartSizeMin = def.wakeSizeMinFactor * size,
                            StartSizeMax = def.wakeSizeMaxFactor * size,
                            BlendMode = LumenBlendMode.Additive,
                            BillboardMode = LumenBillboardMode.Camera,
                            SoftParticleFadeDistance = 0.06f,
                            HdrIntensity = def.hdrWakeMultiplier * hdr,
                            SizeOverLifetime = new ScalarCurve(new[]
                            {
                                new CurveKey(0f, 0.85f),
                                new CurveKey(0.35f, 1f),
                                new CurveKey(1f, 0.08f)
                            }),
                            ColorOverLifetime = new ColorGradient(new[]
                            {
                                new ColorStop(0f, ToVec4(core, 0.88f)),
                                new ColorStop(0.35f, ToVec4(pri, 0.72f)),
                                new ColorStop(0.8f, ToVec4(sec, 0.34f)),
                                new ColorStop(1f, ToVec4(sec, 0f))
                            }),
                            ForceFields = new[]
                            {
                                new ForceFieldSpec(ForceFieldKind.GravityDrag, new NumVector3(0f, -1f, 0f), new NumVector3(def.wakeDrag, 0f, 0f), 0f, 0f),
                                new ForceFieldSpec(ForceFieldKind.CurlTurbulence, NumVector3.Zero, NumVector3.Zero, 0f, def.wakeTurbulence, 2.6f)
                            },
                            EnvironmentChannels = LumenEnvironmentChannel.None
                        }
                    },
                    new EffectNodeSpec
                    {
                        Name = "flight_light",
                        LocalPosition = new NumVector3(0f, def.lightHeightCells, 0f),
                        PointLight = new PointLightSpec(
                            def.lightRadius,
                            ClampRgb01(light),
                            ScalarCurve.Constant(def.lightIntensity))
                    }
                }
            };
        }

        public static string EnsureImpactEffect(ArcaneMissileImpactRimLumenVfxDef def, SpellThemeDef theme, float missileSize)
        {
            int sizeCentis = Mathf.Max(10, Mathf.RoundToInt(missileSize * 100f));
            string effectId = $"NekoYukari.WhisperOfTheForest:DynamicMissileImpact_{def.defName}_{theme.defName}_{sizeCentis}";
            if (registeredIds.Contains(effectId))
            {
                return effectId;
            }

            RimLumenApi.RegisterEffect(BuildImpactAsset(effectId, def, theme, missileSize));
            registeredIds.Add(effectId);
            return effectId;
        }

        public static EffectAsset BuildImpactAsset(
            string effectId,
            ArcaneMissileImpactRimLumenVfxDef def,
            SpellThemeDef theme,
            float missileSize)
        {
            Color core = theme.CoreColorUnity;
            Color pri = theme.PrimaryColorUnity;
            Color sec = theme.SecondaryColorUnity;
            Color light = theme.LightColorUnity;
            Color midBright = Color.Lerp(core, pri, 0.55f);
            float hdr = theme.hdrIntensity;

            return new EffectAsset
            {
                EffectId = effectId,
                DefaultDuration = def.holdDurationSeconds,
                Nodes = new[]
                {
                    new EffectNodeSpec
                    {
                        Name = "impact_sparks",
                        SpacePlane = LumenSpacePlane.Vertical3D,
                        LocalPosition = new NumVector3(0f, 0.06f, 0f),
                        Emitter = new EmitterSpec
                        {
                            Name = "impact_sparks",
                            PreferredTier = LumenParticleTier.L2_GpuCompute,
                            MaxParticles = Math.Max(16, def.sparkBurstCount + 8),
                            Duration = def.holdDurationSeconds,
                            Looping = false,
                            WorldSpaceSimulation = true,
                            RateOverTime = 0f,
                            Bursts = new[] { new ParticleBurstSpec(0f, def.sparkBurstCount) },
                            Shape = LumenShapeKind.Sphere,
                            ShapeExtents = new NumVector3(0.10f * missileSize, 0.08f * missileSize, 0.10f * missileSize),
                            SphericalDirectionAmount = 0.85f,
                            StartLifetimeMin = def.sparkLifetimeMin,
                            StartLifetimeMax = def.sparkLifetimeMax,
                            StartSpeedMin = def.sparkSpeedMin,
                            StartSpeedMax = def.sparkSpeedMax,
                            StartSizeMin = def.sparkSizeMinFactor * missileSize,
                            StartSizeMax = def.sparkSizeMaxFactor * missileSize,
                            BlendMode = LumenBlendMode.Additive,
                            BillboardMode = LumenBillboardMode.StretchedVelocity,
                            VelocityStretchFactor = 0.035f,
                            LengthScale = 1.15f,
                            SoftParticleFadeDistance = 0.06f,
                            HdrIntensity = def.hdrSparkMultiplier * hdr,
                            SizeOverLifetime = new ScalarCurve(new[]
                            {
                                new CurveKey(0f, 1f),
                                new CurveKey(0.45f, 0.74f),
                                new CurveKey(1f, 0f)
                            }),
                            ColorOverLifetime = new ColorGradient(new[]
                            {
                                new ColorStop(0f, ToVec4(core, 0.96f)),
                                new ColorStop(0.32f, ToVec4(midBright, 0.86f)),
                                new ColorStop(0.75f, ToVec4(pri, 0.48f)),
                                new ColorStop(1f, ToVec4(sec, 0f))
                            }),
                            ForceFields = new[]
                            {
                                new ForceFieldSpec(ForceFieldKind.GravityDrag, new NumVector3(0f, -1f, 0f), new NumVector3(def.sparkDrag, 0f, 0f), 0f, 0.9f),
                                new ForceFieldSpec(ForceFieldKind.CurlTurbulence, NumVector3.Zero, NumVector3.Zero, 0f, 1.0f, 2.5f)
                            },
                            EnvironmentChannels = LumenEnvironmentChannel.None
                        }
                    },
                    new EffectNodeSpec
                    {
                        Name = "impact_embers",
                        SpacePlane = LumenSpacePlane.Vertical3D,
                        LocalPosition = new NumVector3(0f, 0.06f, 0f),
                        Emitter = new EmitterSpec
                        {
                            Name = "impact_embers",
                            PreferredTier = LumenParticleTier.L2_GpuCompute,
                            MaxParticles = Math.Max(16, def.emberBurstCount + 8),
                            Duration = def.holdDurationSeconds,
                            Looping = false,
                            WorldSpaceSimulation = true,
                            RateOverTime = 0f,
                            Bursts = new[] { new ParticleBurstSpec(0f, def.emberBurstCount) },
                            Shape = LumenShapeKind.Sphere,
                            ShapeExtents = new NumVector3(0.18f * missileSize, 0.14f * missileSize, 0.18f * missileSize),
                            SphericalDirectionAmount = 0.72f,
                            StartLifetimeMin = def.emberLifetimeMin,
                            StartLifetimeMax = def.emberLifetimeMax,
                            StartSpeedMin = def.emberSpeedMin,
                            StartSpeedMax = def.emberSpeedMax,
                            StartSizeMin = def.emberSizeMinFactor * missileSize,
                            StartSizeMax = def.emberSizeMaxFactor * missileSize,
                            BlendMode = LumenBlendMode.Additive,
                            BillboardMode = LumenBillboardMode.Camera,
                            SoftParticleFadeDistance = 0.06f,
                            HdrIntensity = def.hdrEmberMultiplier * hdr,
                            SizeOverLifetime = new ScalarCurve(new[]
                            {
                                new CurveKey(0f, 0.75f),
                                new CurveKey(0.25f, 1f),
                                new CurveKey(0.75f, 0.55f),
                                new CurveKey(1f, 0f)
                            }),
                            ColorOverLifetime = new ColorGradient(new[]
                            {
                                new ColorStop(0f, ToVec4(core, 0.92f)),
                                new ColorStop(0.35f, ToVec4(pri, 0.78f)),
                                new ColorStop(0.78f, ToVec4(sec, 0.40f)),
                                new ColorStop(1f, ToVec4(sec, 0f))
                            }),
                            ForceFields = new[]
                            {
                                new ForceFieldSpec(ForceFieldKind.GravityDrag, new NumVector3(0f, 1f, 0f), new NumVector3(def.emberDrag, 0f, 0f), 0f, 0.45f),
                                new ForceFieldSpec(ForceFieldKind.CurlTurbulence, NumVector3.Zero, NumVector3.Zero, 0f, def.emberTurbulence, 2.8f)
                            },
                            EnvironmentChannels = LumenEnvironmentChannel.None
                        }
                    },
                    new EffectNodeSpec
                    {
                        Name = "impact_wave",
                        SpacePlane = LumenSpacePlane.Vertical3D,
                        LocalPosition = new NumVector3(0f, 0.08f, 0f),
                        Distortion = new DistortionSpec
                        {
                            Kind = LumenDistortionKind.RingWave,
                            Radius = def.distortionRadius,
                            Strength = def.distortionStrength,
                            Thickness = def.distortionThickness,
                            Speed = def.distortionSpeed,
                            Duration = def.distortionDurationSeconds,
                            StrengthOverTime = new ScalarCurve(new[]
                            {
                                new CurveKey(0f, 1f),
                                new CurveKey(def.distortionDurationSeconds * 0.5f, 0.52f),
                                new CurveKey(def.distortionDurationSeconds, 0f)
                            })
                        }
                    },
                    new EffectNodeSpec
                    {
                        Name = "impact_light",
                        LocalPosition = new NumVector3(0f, def.lightHeightCells, 0f),
                        PointLight = new PointLightSpec(
                            def.lightRadius,
                            ClampRgb01(light),
                            new ScalarCurve(new[]
                            {
                                new CurveKey(0f, def.lightPeakIntensity),
                                new CurveKey(def.lightDurationSeconds * 0.3f, def.lightPeakIntensity * 0.65f),
                                new CurveKey(def.lightDurationSeconds, 0f)
                            }))
                    }
                }
            };
        }

        internal static NumVector4 ToVec4(Color c, float alpha) => new NumVector4(c.r, c.g, c.b, alpha);

        internal static NumVector3 ClampRgb01(Color c) =>
            new NumVector3(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b));
    }

    /// <summary>
    /// Native RimLumen backend for one flying arcane missile: glowing core spearhead + HDR tapered
    /// Ribbon(Trail) + high-drag L2 wake motes + subtle moving PointLight, colored by the spell's theme.
    /// </summary>
    public sealed class ArcaneMissileRimLumenVfxWorker : VfxWorker
    {
        private ArcaneMissileRimLumenVfxDef def;
        private ArcaneMissileSettings settings;
        private SpellThemeDef theme;
        private Projectile_ArcaneMissiles projectile;
        private Map map;

        private bool draining;
        private int drainStartTick;
        private float lastX;
        private float lastZ;
        private float headingX;
        private float headingZ = 1f;

        private Color coreColor;
        private Color primaryColor;
        private Color secondaryColor;

        private LumenRibbonMesh headMesh;
        private ILumenEffectHandle lumenHandle;

        protected override void OnStart()
        {
            def = Def as ArcaneMissileRimLumenVfxDef;
            if (def == null)
            {
                throw new InvalidOperationException("ArcaneMissileRimLumenVfxWorker requires ArcaneMissileRimLumenVfxDef");
            }

            var request = Request as ArcaneMissileVfxRequest;
            projectile = request?.follow as Projectile_ArcaneMissiles;
            if (projectile?.State == null || request.settings == null)
            {
                throw new ArgumentException("ArcaneMissileRimLumenVfxWorker requires ArcaneMissileVfxRequest with live projectile and settings");
            }

            settings = request.settings;
            map = request.map;
            theme = settings.theme ?? SpellDefs.Tuning?.defaultTheme;
            if (theme == null)
            {
                throw new InvalidOperationException("ArcaneMissileRimLumenVfxWorker could not resolve a SpellThemeDef");
            }

            coreColor = theme.CoreColorUnity;
            primaryColor = theme.PrimaryColorUnity;
            secondaryColor = theme.SecondaryColorUnity;

            RefreshMissilePose();
            headMesh = new LumenRibbonMesh("WotF_ArcaneMissile_" + theme.defName);

            if (RimLumenApi.IsReady)
            {
                string effectId = ArcaneMissileLumenCatalog.EnsureFlightEffect(def, theme, settings);
                lumenHandle = RimLumenApi.Play(new LumenPlayRequest
                {
                    EffectId = effectId,
                    MapIndex = map != null ? map.uniqueID : -1,
                    OriginWorld = new NumVector3(lastX, 0f, lastZ),
                    RotationDegrees = Mathf.Atan2(headingX, headingZ) * Mathf.Rad2Deg,
                    Follow = FollowMissile,
                    RealTime = false
                });
            }
        }

        public override void Update(float deltaSeconds)
        {
            if (Finished)
            {
                return;
            }

            if (!draining)
            {
                RefreshMissilePose();
                return;
            }

            float elapsed = SecondsSince(drainStartTick);
            if (lumenHandle != null && lumenHandle.IsAlive)
            {
                float fadeU = Mathf.Clamp01(elapsed / def.drainFadeSeconds);
                float fade = Mathf.Pow(1f - fadeU, 1.8f);
                lumenHandle.SetParam("intensity", fade);
            }

            if (elapsed >= def.drainDurationSeconds)
            {
                Finished = true;
            }
        }

        public override void FollowLost()
        {
            if (draining)
            {
                return;
            }

            draining = true;
            drainStartTick = Find.TickManager.TicksGame;
            lumenHandle?.Stop(allowFadeOut: true);
        }

        public override void DrawWorld()
        {
            if (Finished || draining || headMesh == null || projectile?.State == null || !projectile.Spawned)
            {
                return;
            }

            RefreshMissilePose();
            headMesh.Clear();

            float altitudeY = Altitudes.AltitudeFor(AltitudeLayer.Projectile) + 0.04f;
            var headCenter = new Vector3(lastX, altitudeY, lastZ);
            float size = settings.missileSize;
            float hdr = theme.hdrIntensity * def.hdrHeadMultiplier;

            float tailLen = def.headStretchLengthFactor * size;
            var tailEnd = new Vector3(lastX - headingX * tailLen, altitudeY, lastZ - headingZ * tailLen);
            var noseTip = new Vector3(lastX + headingX * (tailLen * 0.42f), altitudeY, lastZ + headingZ * (tailLen * 0.42f));

            // Smooth rounded teardrop comet sheath (tapers to zero width & alpha at both nose and tail)
            AddCometSheath(
                noseTip,
                headCenter,
                tailEnd,
                def.headHaloRadiusFactor * size * 1.28f,
                hdr * 0.9f);
            headMesh.AddRadialGlowDisc(
                headCenter,
                def.headHaloRadiusFactor * size,
                16,
                new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.52f),
                new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0f),
                hdr * 0.95f,
                def.ribbonEdgePower,
                null);
            headMesh.AddRadialGlowDisc(
                headCenter,
                def.headCoreRadiusFactor * size,
                14,
                new Color(coreColor.r, coreColor.g, coreColor.b, 0.96f),
                new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0f),
                hdr * 1.35f,
                def.ribbonEdgePower * 0.85f,
                null);

            headMesh.Upload();
            if (headMesh.HasGeometry)
            {
                LumenShaders.DrawLumen(headMesh.Mesh, LumenShaders.RibbonOver);
            }
        }

        private void AddCometSheath(Vector3 noseTip, Vector3 headCenter, Vector3 tailEnd, float peakWidth, float hdr)
        {
            const float peakAlpha = 0.58f;
            Color noseTint = Color.Lerp(coreColor, primaryColor, 0.55f);

            const int noseSteps = 3;
            for (int i = 0; i < noseSteps; i++)
            {
                float u0 = i / (float)noseSteps;
                float u1 = (i + 1) / (float)noseSteps;
                Vector3 p0 = Vector3.Lerp(noseTip, headCenter, u0);
                Vector3 p1 = Vector3.Lerp(noseTip, headCenter, u1);
                float w0 = peakWidth * Mathf.Sqrt(u0);
                float w1 = peakWidth * Mathf.Sqrt(u1);
                Color c0 = Color.Lerp(noseTint, primaryColor, u0);
                Color c1 = Color.Lerp(noseTint, primaryColor, u1);
                c0.a = peakAlpha * Mathf.Sin(u0 * Mathf.PI * 0.5f);
                c1.a = peakAlpha * Mathf.Sin(u1 * Mathf.PI * 0.5f);
                headMesh.AddTaperedSegment(p0, p1, w0, w1, c0, c1, hdr, def.ribbonEdgePower, null);
            }

            const int tailSteps = 4;
            for (int i = 0; i < tailSteps; i++)
            {
                float u0 = i / (float)tailSteps;
                float u1 = (i + 1) / (float)tailSteps;
                Vector3 p0 = Vector3.Lerp(headCenter, tailEnd, u0);
                Vector3 p1 = Vector3.Lerp(headCenter, tailEnd, u1);
                float w0 = peakWidth * Mathf.Pow(1f - u0, 1.2f);
                float w1 = peakWidth * Mathf.Pow(1f - u1, 1.2f);
                Color c0 = Color.Lerp(primaryColor, secondaryColor, u0);
                Color c1 = Color.Lerp(primaryColor, secondaryColor, u1);
                c0.a = peakAlpha * Mathf.Pow(1f - u0, 1.5f);
                c1.a = peakAlpha * Mathf.Pow(1f - u1, 1.5f);
                headMesh.AddTaperedSegment(p0, p1, w0, w1, c0, c1, hdr, def.ribbonEdgePower, null);
            }
        }

        protected override void Release()
        {
            if (lumenHandle != null)
            {
                lumenHandle.Stop(allowFadeOut: false);
                lumenHandle = null;
            }

            if (headMesh != null)
            {
                headMesh.Dispose();
                headMesh = null;
            }
        }

        private void RefreshMissilePose()
        {
            ArcaneMissile state = projectile?.State;
            if (state == null)
            {
                return;
            }

            ArcaneMissileVfxPosition.Of(state, out float x, out _, out float z);
            lastX = x;
            lastZ = z;
            headingX = state.headingX;
            headingZ = state.headingZ;
        }

        private bool FollowMissile(out NumVector3 worldPosition, out float rotationDegrees)
        {
            if (!draining && projectile?.State != null && projectile.Spawned && projectile.Map == map)
            {
                RefreshMissilePose();
                worldPosition = new NumVector3(lastX, 0f, lastZ);
                rotationDegrees = Mathf.Atan2(headingX, headingZ) * Mathf.Rad2Deg;
                return true;
            }

            worldPosition = new NumVector3(lastX, 0f, lastZ);
            rotationDegrees = Mathf.Atan2(headingX, headingZ) * Mathf.Rad2Deg;
            return false;
        }

        private static float SecondsSince(int tick)
        {
            long delta = (long)Find.TickManager.TicksGame - tick;
            return delta <= 0 ? 0f : delta / (float)GenTicks.TicksPerRealSecond;
        }
    }

    /// <summary>
    /// Native RimLumen detonation burst when an arcane missile hits a target or is intercepted by a shield.
    /// </summary>
    public sealed class ArcaneMissileRimLumenImpactVfxWorker : VfxWorker
    {
        private ArcaneMissileImpactRimLumenVfxDef def;
        private ArcaneMissileSettings settings;
        private SpellThemeDef theme;
        private int startTick;
        private Vector3 impactPos;

        private Color coreColor;
        private Color primaryColor;
        private Color secondaryColor;

        private LumenRibbonMesh flashMesh;
        private ILumenEffectHandle lumenHandle;

        protected override void OnStart()
        {
            def = Def as ArcaneMissileImpactRimLumenVfxDef;
            if (def == null)
            {
                throw new InvalidOperationException("ArcaneMissileRimLumenImpactVfxWorker requires ArcaneMissileImpactRimLumenVfxDef");
            }

            var request = Request as ArcaneMissileVfxRequest;
            if (request?.settings == null)
            {
                throw new ArgumentException("ArcaneMissileRimLumenImpactVfxWorker requires ArcaneMissileVfxRequest with settings");
            }

            settings = request.settings;
            theme = settings.theme ?? SpellDefs.Tuning?.defaultTheme;
            if (theme == null)
            {
                throw new InvalidOperationException("ArcaneMissileRimLumenImpactVfxWorker could not resolve a SpellThemeDef");
            }

            startTick = Find.TickManager.TicksGame;
            float altitudeY = Altitudes.AltitudeFor(AltitudeLayer.MoteOverhead);
            impactPos = new Vector3(request.origin.x, altitudeY, request.origin.z);

            coreColor = theme.CoreColorUnity;
            primaryColor = theme.PrimaryColorUnity;
            secondaryColor = theme.SecondaryColorUnity;

            flashMesh = new LumenRibbonMesh("WotF_ArcaneImpact_" + theme.defName);

            if (request.map == Find.CurrentMap && def.touchSound != null)
            {
                def.touchSound.PlayOneShot(new TargetInfo(impactPos.ToIntVec3(), request.map));
            }

            if (RimLumenApi.IsReady)
            {
                string effectId = ArcaneMissileLumenCatalog.EnsureImpactEffect(def, theme, settings.missileSize);
                lumenHandle = RimLumenApi.Play(new LumenPlayRequest
                {
                    EffectId = effectId,
                    MapIndex = request.map != null ? request.map.uniqueID : -1,
                    OriginWorld = new NumVector3(impactPos.x, 0f, impactPos.z),
                    RealTime = false
                });
            }
        }

        public override void Update(float deltaSeconds)
        {
            if (Finished)
            {
                return;
            }

            float elapsed = SecondsSince(startTick);
            if (elapsed >= def.holdDurationSeconds && (lumenHandle == null || !lumenHandle.IsAlive))
            {
                Finished = true;
            }
            else if (elapsed >= def.holdDurationSeconds + 0.25f)
            {
                Finished = true;
            }
        }

        public override void DrawWorld()
        {
            if (Finished || flashMesh == null)
            {
                return;
            }

            float elapsed = SecondsSince(startTick);
            if (elapsed >= def.flashDurationSeconds)
            {
                return;
            }

            flashMesh.Clear();
            float size = settings.missileSize;
            float t = Mathf.Clamp01(elapsed / def.flashDurationSeconds);
            float expand = t < 0.20f
                ? Mathf.Lerp(0.65f, 1f, t / 0.20f)
                : Mathf.Pow(1f - (t - 0.20f) / 0.80f, 1.65f);
            float radius = def.flashPeakRadiusFactor * size * expand;
            float alpha = Mathf.Pow(1f - t, 1.45f);
            float hdr = theme.hdrIntensity * def.hdrFlashMultiplier;

            flashMesh.AddRadialGlowDisc(
                impactPos,
                radius * 1.45f,
                20,
                new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.50f * alpha),
                new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0f),
                hdr * 0.88f,
                def.ribbonEdgePower,
                null);
            flashMesh.AddRadialGlowDisc(
                impactPos,
                radius * 0.66f,
                16,
                new Color(coreColor.r, coreColor.g, coreColor.b, 0.94f * alpha),
                new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0f),
                hdr * 1.32f,
                def.ribbonEdgePower * 0.85f,
                null);

            flashMesh.Upload();
            if (flashMesh.HasGeometry)
            {
                LumenShaders.DrawLumen(flashMesh.Mesh, LumenShaders.RibbonOver);
            }
        }

        protected override void Release()
        {
            if (lumenHandle != null)
            {
                lumenHandle.Stop(allowFadeOut: false);
                lumenHandle = null;
            }

            if (flashMesh != null)
            {
                flashMesh.Dispose();
                flashMesh = null;
            }
        }

        private static float SecondsSince(int tick)
        {
            long delta = (long)Find.TickManager.TicksGame - tick;
            return delta <= 0 ? 0f : delta / (float)GenTicks.TicksPerRealSecond;
        }
    }
}
