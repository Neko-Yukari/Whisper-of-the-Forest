using System;
using System.Collections.Generic;
using RimLumen.Core.Api;
using RimLumen.Core.Contracts;
using UnityEngine;
using Verse;
using Verse.Sound;
using WhisperOfTheForest.Spells;
using CurveKey = RimLumen.Core.Contracts.CurveKey;
using NumVector3 = System.Numerics.Vector3;
using NumVector4 = System.Numerics.Vector4;

namespace WhisperOfTheForest.Vfx
{
    public sealed class LightningLumenVfxDef : VfxDef
    {
        public string lumenEffectId;
        public SoundDef strikeSound;
        public int strikeDelayTicks;
        public int strikeDurationTicks;
        public int totalDurationTicks;
        public float baseStrikeRadius;
        public float boltSkyHeightCells;
        public float boltCoreWidth;
        public float boltSheathWidth;
        public int boltSegments;
        public float boltJitterCells;
        public float hdrBoltMultiplier;
        public float hdrRingMultiplier;
        public float ribbonEdgePower;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (lumenEffectId.NullOrEmpty())
            {
                yield return "lumenEffectId is required";
            }

            if (strikeDelayTicks < 0 || strikeDurationTicks <= 0 || totalDurationTicks <= strikeDelayTicks + strikeDurationTicks)
            {
                yield return "0 <= strikeDelayTicks, strikeDurationTicks > 0 and totalDurationTicks > strikeDelayTicks + strikeDurationTicks are required";
            }

            if (baseStrikeRadius <= 0f || boltSkyHeightCells <= 0f || boltCoreWidth <= 0f || boltSheathWidth <= 0f)
            {
                yield return "baseStrikeRadius, boltSkyHeightCells, boltCoreWidth and boltSheathWidth must be positive";
            }

            if (boltSegments < 4 || boltJitterCells < 0f || hdrBoltMultiplier <= 0f || hdrRingMultiplier <= 0f || ribbonEdgePower <= 0f)
            {
                yield return "boltSegments >= 4, boltJitterCells >= 0, hdr multipliers > 0 and ribbonEdgePower > 0 are required";
            }
        }
    }

    public sealed class LingerLumenVfxDef : VfxDef
    {
        public SoundDef deploySound;
        public SoundDef pulseSound;
        public float defaultRadius;
        public float fadeDurationSeconds;
        public float pulseWaveDurationSeconds;
        public float boundaryThickness;
        public float innerArcRadiusFactor;
        public int innerArcCount;
        public float spinRadPerSec;
        public float mistRatePerArea;
        public float mistLifetimeMin;
        public float mistLifetimeMax;
        public float mistSizeMin;
        public float mistSizeMax;
        public float lightRadiusFactor;
        public float lightHeightCells;
        public float lightIntensity;
        public float hdrRingMultiplier;
        public float hdrMistMultiplier;
        public float ribbonEdgePower;

        public override bool SupportsState(string state) => state == VfxStates.Pulse;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (defaultRadius <= 0f || fadeDurationSeconds <= 0f || pulseWaveDurationSeconds <= 0f)
            {
                yield return "defaultRadius, fadeDurationSeconds and pulseWaveDurationSeconds must be positive";
            }

            if (boundaryThickness <= 0f || innerArcRadiusFactor <= 0f || innerArcCount < 2)
            {
                yield return "boundaryThickness > 0, innerArcRadiusFactor > 0 and innerArcCount >= 2 are required";
            }

            if (mistRatePerArea <= 0f || mistLifetimeMin <= 0f || mistLifetimeMax < mistLifetimeMin
                || mistSizeMin <= 0f || mistSizeMax < mistSizeMin)
            {
                yield return "mist rate, lifetime and size parameters must be positive";
            }

            if (lightRadiusFactor <= 0f || lightHeightCells < 1f || lightIntensity <= 0f)
            {
                yield return "lightRadiusFactor > 0, lightHeightCells >= 1 and lightIntensity > 0 are required";
            }

            if (hdrRingMultiplier <= 0f || hdrMistMultiplier <= 0f || ribbonEdgePower <= 0f)
            {
                yield return "hdr multipliers and ribbonEdgePower must be positive";
            }
        }
    }

    public sealed class RuneLumenVfxDef : VfxDef
    {
        public SoundDef placeSound;
        public float sealRadius;
        public float sealThickness;
        public float spinRadPerSec;
        public float fadeDurationSeconds;
        public float impactDurationSeconds;
        public float impactSpindleHeightCells;
        public float impactSpindleWidth;
        public float idleMoteRate;
        public int impactColumnBurstCount;
        public float lightRadius;
        public float lightHeightCells;
        public float idleLightIntensity;
        public float impactLightIntensity;
        public float hdrSealMultiplier;
        public float hdrImpactMultiplier;
        public float ribbonEdgePower;

        public override bool SupportsState(string state) => state == VfxStates.Impact;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (sealRadius <= 0f || sealThickness <= 0f || fadeDurationSeconds <= 0f || impactDurationSeconds <= 0f)
            {
                yield return "sealRadius, sealThickness, fadeDurationSeconds and impactDurationSeconds must be positive";
            }

            if (impactSpindleHeightCells <= 0f || impactSpindleWidth <= 0f || idleMoteRate < 0f || impactColumnBurstCount < 1)
            {
                yield return "impact spindle/column parameters must be positive";
            }

            if (lightRadius <= 0f || lightHeightCells < 1f || idleLightIntensity <= 0f || impactLightIntensity <= 0f)
            {
                yield return "rune light parameters must be positive with lightHeightCells >= 1";
            }

            if (hdrSealMultiplier <= 0f || hdrImpactMultiplier <= 0f || ribbonEdgePower <= 0f)
            {
                yield return "hdr multipliers and ribbonEdgePower must be positive";
            }
        }
    }

    public sealed class OrbitLumenVfxDef : VfxDef
    {
        public SoundDef spawnSound;
        public float coreRadius;
        public float haloRadius;
        public float spinRadPerSec;
        public float trailDurationSeconds;
        public float trailWidth;
        public float trailMinVertexDistance;
        public int trailMaxPoints;
        public float dustRatePerSecond;
        public int impactBurstCount;
        public float fadeDurationSeconds;
        public float impactDurationSeconds;
        public float lightRadius;
        public float lightHeightCells;
        public float lightIntensity;
        public float hdrCoreMultiplier;
        public float hdrTrailMultiplier;
        public float hdrImpactMultiplier;
        public float ribbonEdgePower;

        public override bool SupportsState(string state) => state == VfxStates.Impact;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (coreRadius <= 0f || haloRadius <= 0f || fadeDurationSeconds <= 0f || impactDurationSeconds <= 0f)
            {
                yield return "coreRadius, haloRadius, fadeDurationSeconds and impactDurationSeconds must be positive";
            }

            if (trailDurationSeconds <= 0f || trailWidth <= 0f || trailMinVertexDistance <= 0f || trailMaxPoints < 2)
            {
                yield return "trail parameters must be positive with trailMaxPoints >= 2";
            }

            if (dustRatePerSecond < 0f || impactBurstCount < 1)
            {
                yield return "dustRatePerSecond >= 0 and impactBurstCount >= 1 are required";
            }

            if (lightRadius <= 0f || lightHeightCells < 1f || lightIntensity <= 0f)
            {
                yield return "orbit light parameters must be positive with lightHeightCells >= 1";
            }

            if (hdrCoreMultiplier <= 0f || hdrTrailMultiplier <= 0f || hdrImpactMultiplier <= 0f || ribbonEdgePower <= 0f)
            {
                yield return "hdr multipliers and ribbonEdgePower must be positive";
            }
        }
    }

    public sealed class TornadoLumenVfxDef : VfxDef
    {
        public string lumenEffectId;
        public SoundDef spawnSound;
        public SoundDef loopSound;
        public SoundDef endSound;
        public float referenceRadius;
        public float collapseDurationSeconds;
        public int heightSteps;
        public int crossSteps;
        public float baseRadiusFactor;
        public float waistRadiusFactor;
        public float topRadiusFactor;
        public float funnelHeightFactor;
        public float swayAmplitudeFactor;
        public int flameTongueCount;
        public string groundBoundaryColorHex;
        public float hdrFunnelMultiplier;
        public float ribbonEdgePower;

        public override bool SupportsState(string state) => state == VfxStates.Expire;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (lumenEffectId.NullOrEmpty())
            {
                yield return "lumenEffectId is required";
            }

            if (referenceRadius <= 0f || collapseDurationSeconds <= 0f)
            {
                yield return "referenceRadius and collapseDurationSeconds must be positive";
            }

            if (heightSteps < 4 || crossSteps < 4 || flameTongueCount < 0
                || baseRadiusFactor <= 0f || waistRadiusFactor <= 0f
                || topRadiusFactor <= 0f || funnelHeightFactor <= 0f || swayAmplitudeFactor < 0f)
            {
                yield return "tornado funnel geometry parameters must be positive (steps >= 4, flameTongueCount >= 0)";
            }

            if (!VfxColors.IsValid(groundBoundaryColorHex))
            {
                yield return "groundBoundaryColorHex must be " + VfxColors.Format;
            }

            if (hdrFunnelMultiplier <= 0f || ribbonEdgePower <= 0f)
            {
                yield return "hdrFunnelMultiplier and ribbonEdgePower must be positive";
            }
        }
    }

    internal static class SpellFieldOrbitLumenCatalog
    {
        private static readonly HashSet<string> registeredIds = new HashSet<string>(StringComparer.Ordinal);

        public static string EnsureLingerEffect(LingerLumenVfxDef def, SpellThemeDef theme, float radius)
        {
            int radiusDecis = Mathf.Max(5, Mathf.RoundToInt(radius * 10f));
            string effectId = $"NekoYukari.WhisperOfTheForest:DynamicLinger_{def.defName}_{theme.defName}_{radiusDecis}";
            if (registeredIds.Contains(effectId))
            {
                return effectId;
            }

            RimLumenApi.RegisterEffect(BuildLingerAsset(effectId, def, theme, radiusDecis * 0.1f));
            registeredIds.Add(effectId);
            return effectId;
        }

        public static EffectAsset BuildLingerAsset(string effectId, LingerLumenVfxDef def, SpellThemeDef theme, float r)
        {
            float area = Mathf.PI * r * r;
            float rate = Mathf.Clamp(area * def.mistRatePerArea, 8f, 90f);
            int maxParticles = Mathf.Clamp(Mathf.CeilToInt(rate * def.mistLifetimeMax * 1.4f), 32, 220);

            Color core = theme.CoreColorUnity;
            Color pri = theme.PrimaryColorUnity;
            Color sec = theme.SecondaryColorUnity;
            Color light = theme.LightColorUnity;
            float hdr = theme.hdrIntensity;

            return new EffectAsset
            {
                EffectId = effectId,
                DefaultDuration = 2f,
                Nodes = new[]
                {
                    new EffectNodeSpec
                    {
                        Name = "field_mist",
                        SpacePlane = LumenSpacePlane.GroundXZ,
                        LocalPosition = new NumVector3(0f, 0.04f, 0f),
                        Emitter = new EmitterSpec
                        {
                            Name = "field_mist",
                            PreferredTier = LumenParticleTier.L2_GpuCompute,
                            MaxParticles = maxParticles,
                            Duration = 2f,
                            Looping = true,
                            WorldSpaceSimulation = false,
                            RateOverTime = rate,
                            Shape = LumenShapeKind.CircleXZ,
                            ShapeExtents = new NumVector3(r * 0.94f, 0f, r * 0.94f),
                            RadiusThickness = 0.92f,
                            StartLifetimeMin = def.mistLifetimeMin,
                            StartLifetimeMax = def.mistLifetimeMax,
                            StartSpeedMin = 0.04f,
                            StartSpeedMax = 0.18f,
                            StartSizeMin = def.mistSizeMin,
                            StartSizeMax = def.mistSizeMax,
                            BlendMode = LumenBlendMode.Additive,
                            BillboardMode = LumenBillboardMode.Camera,
                            SoftParticleFadeDistance = 0.08f,
                            HdrIntensity = def.hdrMistMultiplier * hdr,
                            SizeOverLifetime = new ScalarCurve(new[]
                            {
                                new CurveKey(0f, 0.35f),
                                new CurveKey(0.28f, 1f),
                                new CurveKey(0.78f, 0.85f),
                                new CurveKey(1f, 0.08f)
                            }),
                            ColorOverLifetime = new ColorGradient(new[]
                            {
                                new ColorStop(0f, ToVec4(sec, 0f)),
                                new ColorStop(0.2f, ToVec4(pri, 0.72f)),
                                new ColorStop(0.62f, ToVec4(core, 0.85f)),
                                new ColorStop(1f, ToVec4(sec, 0f))
                            }),
                            ForceFields = new[]
                            {
                                new ForceFieldSpec(ForceFieldKind.GravityDrag, new NumVector3(0f, -1f, 0f), new NumVector3(3.6f, 0f, 0f), 0f, 0f),
                                new ForceFieldSpec(ForceFieldKind.Vortex, new NumVector3(0f, 1f, 0f), new NumVector3(0.4f, 0.35f, 0f), r * 1.15f, 2.2f),
                                new ForceFieldSpec(ForceFieldKind.CurlTurbulence, NumVector3.Zero, NumVector3.Zero, 0f, 0.45f, 1.8f)
                            },
                            EnvironmentChannels = LumenEnvironmentChannel.None
                        }
                    },
                    new EffectNodeSpec
                    {
                        Name = "pulse_burst",
                        SpacePlane = LumenSpacePlane.GroundXZ,
                        LocalPosition = new NumVector3(0f, 0.05f, 0f),
                        Emitter = new EmitterSpec
                        {
                            Name = "pulse_burst",
                            PreferredTier = LumenParticleTier.L2_GpuCompute,
                            MaxParticles = 64,
                            Duration = def.pulseWaveDurationSeconds,
                            Looping = false,
                            WorldSpaceSimulation = false,
                            RateOverTime = 0f,
                            Bursts = new[] { new ParticleBurstSpec(0f, Mathf.Clamp(Mathf.RoundToInt(r * 10f), 16, 48)) },
                            Shape = LumenShapeKind.CircleEdgeXZ,
                            ShapeExtents = new NumVector3(r * 0.28f, 0f, r * 0.28f),
                            SphericalDirectionAmount = 1f,
                            StartLifetimeMin = def.pulseWaveDurationSeconds * 0.65f,
                            StartLifetimeMax = def.pulseWaveDurationSeconds,
                            StartSpeedMin = r * 1.35f,
                            StartSpeedMax = r * 2.1f,
                            StartSizeMin = def.mistSizeMin * 0.9f,
                            StartSizeMax = def.mistSizeMax * 1.1f,
                            BlendMode = LumenBlendMode.Additive,
                            BillboardMode = LumenBillboardMode.Camera,
                            SoftParticleFadeDistance = 0.06f,
                            HdrIntensity = def.hdrMistMultiplier * 1.35f * hdr,
                            SizeOverLifetime = new ScalarCurve(new[]
                            {
                                new CurveKey(0f, 0.9f),
                                new CurveKey(0.4f, 1.05f),
                                new CurveKey(1f, 0f)
                            }),
                            ColorOverLifetime = new ColorGradient(new[]
                            {
                                new ColorStop(0f, ToVec4(core, 0.92f)),
                                new ColorStop(0.45f, ToVec4(pri, 0.72f)),
                                new ColorStop(1f, ToVec4(sec, 0f))
                            }),
                            ForceFields = new[]
                            {
                                new ForceFieldSpec(ForceFieldKind.GravityDrag, new NumVector3(0f, -1f, 0f), new NumVector3(3.2f, 0f, 0f), 0f, 0f)
                            },
                            EnvironmentChannels = LumenEnvironmentChannel.None
                        }
                    },
                    new EffectNodeSpec
                    {
                        Name = "field_light",
                        LocalPosition = new NumVector3(0f, def.lightHeightCells, 0f),
                        PointLight = new PointLightSpec(
                            Mathf.Clamp(r * def.lightRadiusFactor, 1.2f, 38f),
                            ClampRgb01(light),
                            ScalarCurve.Constant(def.lightIntensity))
                    }
                }
            };
        }

        public static string EnsureRuneEffect(RuneLumenVfxDef def, SpellThemeDef theme)
        {
            string effectId = $"NekoYukari.WhisperOfTheForest:DynamicRune_{def.defName}_{theme.defName}";
            if (registeredIds.Contains(effectId))
            {
                return effectId;
            }

            RimLumenApi.RegisterEffect(BuildRuneAsset(effectId, def, theme));
            registeredIds.Add(effectId);
            return effectId;
        }

        public static EffectAsset BuildRuneAsset(string effectId, RuneLumenVfxDef def, SpellThemeDef theme)
        {
            Color core = theme.CoreColorUnity;
            Color pri = theme.PrimaryColorUnity;
            Color sec = theme.SecondaryColorUnity;
            Color light = theme.LightColorUnity;
            float hdr = theme.hdrIntensity;

            return new EffectAsset
            {
                EffectId = effectId,
                DefaultDuration = 2f,
                Nodes = new[]
                {
                    new EffectNodeSpec
                    {
                        Name = "idle_motes",
                        SpacePlane = LumenSpacePlane.Vertical3D,
                        LocalPosition = new NumVector3(0f, 0.03f, 0f),
                        Emitter = new EmitterSpec
                        {
                            Name = "idle_motes",
                            PreferredTier = LumenParticleTier.L2_GpuCompute,
                            MaxParticles = 48,
                            Duration = 2f,
                            Looping = true,
                            WorldSpaceSimulation = false,
                            RateOverTime = def.idleMoteRate,
                            Shape = LumenShapeKind.CircleEdgeXZ,
                            ShapeExtents = new NumVector3(def.sealRadius * 0.88f, 0f, def.sealRadius * 0.88f),
                            StartLifetimeMin = 0.75f,
                            StartLifetimeMax = 1.25f,
                            StartSpeedMin = 0.08f,
                            StartSpeedMax = 0.25f,
                            StartSizeMin = 0.035f,
                            StartSizeMax = 0.068f,
                            BlendMode = LumenBlendMode.Additive,
                            BillboardMode = LumenBillboardMode.Camera,
                            SoftParticleFadeDistance = 0.06f,
                            HdrIntensity = def.hdrSealMultiplier * hdr,
                            SizeOverLifetime = new ScalarCurve(new[]
                            {
                                new CurveKey(0f, 0.3f),
                                new CurveKey(0.3f, 1f),
                                new CurveKey(1f, 0f)
                            }),
                            ColorOverLifetime = new ColorGradient(new[]
                            {
                                new ColorStop(0f, ToVec4(sec, 0f)),
                                new ColorStop(0.25f, ToVec4(pri, 0.78f)),
                                new ColorStop(0.7f, ToVec4(core, 0.85f)),
                                new ColorStop(1f, ToVec4(sec, 0f))
                            }),
                            ForceFields = new[]
                            {
                                new ForceFieldSpec(ForceFieldKind.GravityDrag, new NumVector3(0f, 1f, 0f), new NumVector3(2.4f, 0f, 0f), 0f, 0.65f),
                                new ForceFieldSpec(ForceFieldKind.Vortex, new NumVector3(0f, 1f, 0f), new NumVector3(0.5f, 0.6f, 0f), def.sealRadius * 1.4f, 2.5f)
                            },
                            EnvironmentChannels = LumenEnvironmentChannel.None
                        }
                    },
                    new EffectNodeSpec
                    {
                        Name = "idle_light",
                        LocalPosition = new NumVector3(0f, def.lightHeightCells, 0f),
                        PointLight = new PointLightSpec(
                            def.lightRadius,
                            ClampRgb01(light),
                            ScalarCurve.Constant(def.idleLightIntensity))
                    },
                    new EffectNodeSpec
                    {
                        Name = "impact_column",
                        SpacePlane = LumenSpacePlane.Vertical3D,
                        LocalPosition = new NumVector3(0f, 0.04f, 0f),
                        Emitter = new EmitterSpec
                        {
                            Name = "impact_column",
                            PreferredTier = LumenParticleTier.L2_GpuCompute,
                            MaxParticles = Math.Max(32, def.impactColumnBurstCount + 12),
                            Duration = def.impactDurationSeconds,
                            Looping = false,
                            WorldSpaceSimulation = true,
                            RateOverTime = 0f,
                            Bursts = new[] { new ParticleBurstSpec(0f, def.impactColumnBurstCount) },
                            Shape = LumenShapeKind.Cone,
                            ConeAngleDegrees = 12f,
                            ShapeExtents = new NumVector3(def.sealRadius * 0.55f, 0f, def.sealRadius * 0.55f),
                            StartLifetimeMin = def.impactDurationSeconds * 0.55f,
                            StartLifetimeMax = def.impactDurationSeconds * 0.95f,
                            StartSpeedMin = 3.2f,
                            StartSpeedMax = 6.8f,
                            StartSizeMin = 0.048f,
                            StartSizeMax = 0.098f,
                            BlendMode = LumenBlendMode.Additive,
                            BillboardMode = LumenBillboardMode.StretchedVelocity,
                            VelocityStretchFactor = 0.055f,
                            LengthScale = 1.25f,
                            SoftParticleFadeDistance = 0.06f,
                            HdrIntensity = def.hdrImpactMultiplier * hdr,
                            SizeOverLifetime = new ScalarCurve(new[]
                            {
                                new CurveKey(0f, 1f),
                                new CurveKey(0.45f, 0.78f),
                                new CurveKey(1f, 0f)
                            }),
                            ColorOverLifetime = new ColorGradient(new[]
                            {
                                new ColorStop(0f, ToVec4(core, 0.98f)),
                                new ColorStop(0.4f, ToVec4(pri, 0.84f)),
                                new ColorStop(1f, ToVec4(sec, 0f))
                            }),
                            ForceFields = new[]
                            {
                                new ForceFieldSpec(ForceFieldKind.GravityDrag, new NumVector3(0f, -1f, 0f), new NumVector3(4.2f, 0f, 0f), 0f, 0f),
                                new ForceFieldSpec(ForceFieldKind.CurlTurbulence, NumVector3.Zero, NumVector3.Zero, 0f, 0.85f, 2.2f)
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
                            Radius = def.sealRadius * 2.4f,
                            Strength = 0.048f,
                            Thickness = 0.28f,
                            Speed = 6.2f,
                            Duration = 0.28f,
                            StrengthOverTime = new ScalarCurve(new[]
                            {
                                new CurveKey(0f, 1f),
                                new CurveKey(0.15f, 0.55f),
                                new CurveKey(0.28f, 0f)
                            })
                        }
                    },
                    new EffectNodeSpec
                    {
                        Name = "impact_light",
                        LocalPosition = new NumVector3(0f, def.lightHeightCells, 0f),
                        PointLight = new PointLightSpec(
                            def.lightRadius * 1.65f,
                            ClampRgb01(light),
                            new ScalarCurve(new[]
                            {
                                new CurveKey(0f, def.impactLightIntensity),
                                new CurveKey(def.impactDurationSeconds * 0.3f, def.impactLightIntensity * 0.55f),
                                new CurveKey(def.impactDurationSeconds * 0.85f, 0f)
                            }))
                    }
                },
                InitialState = "Idle",
                States = new[]
                {
                    new EffectStateSpec { Name = "Idle", Nodes = new[] { "idle_motes", "idle_light" }, Duration = 2f, Loop = true },
                    new EffectStateSpec { Name = VfxStates.Impact, Nodes = new[] { "impact_column", "impact_wave", "impact_light" }, Duration = def.impactDurationSeconds, Loop = false }
                }
            };
        }

        public static string EnsureOrbitEffect(OrbitLumenVfxDef def, SpellThemeDef theme)
        {
            string effectId = $"NekoYukari.WhisperOfTheForest:DynamicOrbit_{def.defName}_{theme.defName}";
            if (registeredIds.Contains(effectId))
            {
                return effectId;
            }

            RimLumenApi.RegisterEffect(BuildOrbitAsset(effectId, def, theme));
            registeredIds.Add(effectId);
            return effectId;
        }

        public static EffectAsset BuildOrbitAsset(string effectId, OrbitLumenVfxDef def, SpellThemeDef theme)
        {
            Color core = theme.CoreColorUnity;
            Color pri = theme.PrimaryColorUnity;
            Color sec = theme.SecondaryColorUnity;
            Color light = theme.LightColorUnity;
            float hdr = theme.hdrIntensity;

            return new EffectAsset
            {
                EffectId = effectId,
                DefaultDuration = 2f,
                Nodes = new[]
                {
                    new EffectNodeSpec
                    {
                        Name = "orbit_trail",
                        SpacePlane = LumenSpacePlane.Vertical3D,
                        LocalPosition = new NumVector3(0f, 0.04f, 0f),
                        Ribbon = new RibbonSpec
                        {
                            Mode = LumenRibbonMode.Trail,
                            Lifetime = def.trailDurationSeconds,
                            MinVertexDistance = def.trailMinVertexDistance,
                            MaxPoints = def.trailMaxPoints,
                            Width = def.trailWidth,
                            WidthOverLength = new ScalarCurve(new[]
                            {
                                new CurveKey(0f, 1f),
                                new CurveKey(0.4f, 0.62f),
                                new CurveKey(1f, 0f)
                            }),
                            ColorOverLength = new ColorGradient(new[]
                            {
                                new ColorStop(0f, ToVec4(core, 0.94f)),
                                new ColorStop(0.32f, ToVec4(pri, 0.80f)),
                                new ColorStop(0.78f, ToVec4(sec, 0.36f)),
                                new ColorStop(1f, ToVec4(sec, 0f))
                            }),
                            HdrIntensity = def.hdrTrailMultiplier * hdr,
                            BlendMode = LumenBlendMode.Additive,
                            Alignment = LumenRibbonAlignment.View,
                            SoftParticleFadeDistance = 0.06f
                        }
                    },
                    new EffectNodeSpec
                    {
                        Name = "orbit_stardust",
                        SpacePlane = LumenSpacePlane.Vertical3D,
                        LocalPosition = new NumVector3(0f, 0.04f, 0f),
                        Emitter = new EmitterSpec
                        {
                            Name = "orbit_stardust",
                            PreferredTier = LumenParticleTier.L2_GpuCompute,
                            MaxParticles = 48,
                            Duration = 1f,
                            Looping = true,
                            WorldSpaceSimulation = true,
                            RateOverTime = def.dustRatePerSecond,
                            Shape = LumenShapeKind.Sphere,
                            ShapeExtents = new NumVector3(0.08f, 0.08f, 0.08f),
                            StartLifetimeMin = 0.22f,
                            StartLifetimeMax = 0.42f,
                            StartSpeedMin = 0.08f,
                            StartSpeedMax = 0.32f,
                            StartSizeMin = 0.036f,
                            StartSizeMax = 0.072f,
                            BlendMode = LumenBlendMode.Additive,
                            BillboardMode = LumenBillboardMode.Camera,
                            SoftParticleFadeDistance = 0.06f,
                            HdrIntensity = def.hdrTrailMultiplier * hdr,
                            SizeOverLifetime = new ScalarCurve(new[]
                            {
                                new CurveKey(0f, 0.8f),
                                new CurveKey(0.35f, 1f),
                                new CurveKey(1f, 0f)
                            }),
                            ColorOverLifetime = new ColorGradient(new[]
                            {
                                new ColorStop(0f, ToVec4(core, 0.88f)),
                                new ColorStop(0.45f, ToVec4(pri, 0.68f)),
                                new ColorStop(1f, ToVec4(sec, 0f))
                            }),
                            ForceFields = new[]
                            {
                                new ForceFieldSpec(ForceFieldKind.GravityDrag, new NumVector3(0f, -1f, 0f), new NumVector3(5.0f, 0f, 0f), 0f, 0f),
                                new ForceFieldSpec(ForceFieldKind.CurlTurbulence, NumVector3.Zero, NumVector3.Zero, 0f, 0.65f, 2.4f)
                            },
                            EnvironmentChannels = LumenEnvironmentChannel.None
                        }
                    },
                    new EffectNodeSpec
                    {
                        Name = "orbit_light",
                        LocalPosition = new NumVector3(0f, def.lightHeightCells, 0f),
                        PointLight = new PointLightSpec(
                            def.lightRadius,
                            ClampRgb01(light),
                            ScalarCurve.Constant(def.lightIntensity))
                    },
                    new EffectNodeSpec
                    {
                        Name = "impact_sparks",
                        SpacePlane = LumenSpacePlane.Vertical3D,
                        LocalPosition = new NumVector3(0f, 0.06f, 0f),
                        Emitter = new EmitterSpec
                        {
                            Name = "impact_sparks",
                            PreferredTier = LumenParticleTier.L2_GpuCompute,
                            MaxParticles = Math.Max(16, def.impactBurstCount + 8),
                            Duration = def.impactDurationSeconds,
                            Looping = false,
                            WorldSpaceSimulation = true,
                            RateOverTime = 0f,
                            Bursts = new[] { new ParticleBurstSpec(0f, def.impactBurstCount) },
                            Shape = LumenShapeKind.SphereShell,
                            ShapeExtents = new NumVector3(0.08f, 0.08f, 0.08f),
                            SphericalDirectionAmount = 1f,
                            StartLifetimeMin = 0.18f,
                            StartLifetimeMax = 0.38f,
                            StartSpeedMin = 2.0f,
                            StartSpeedMax = 5.0f,
                            StartSizeMin = 0.042f,
                            StartSizeMax = 0.085f,
                            BlendMode = LumenBlendMode.Additive,
                            BillboardMode = LumenBillboardMode.StretchedVelocity,
                            VelocityStretchFactor = 0.05f,
                            LengthScale = 1.2f,
                            SoftParticleFadeDistance = 0.06f,
                            HdrIntensity = def.hdrImpactMultiplier * hdr,
                            SizeOverLifetime = new ScalarCurve(new[]
                            {
                                new CurveKey(0f, 1f),
                                new CurveKey(0.5f, 0.7f),
                                new CurveKey(1f, 0f)
                            }),
                            ColorOverLifetime = new ColorGradient(new[]
                            {
                                new ColorStop(0f, ToVec4(core, 0.96f)),
                                new ColorStop(0.4f, ToVec4(pri, 0.8f)),
                                new ColorStop(1f, ToVec4(sec, 0f))
                            }),
                            ForceFields = new[]
                            {
                                new ForceFieldSpec(ForceFieldKind.GravityDrag, new NumVector3(0f, -1f, 0f), new NumVector3(5.6f, 0f, 0f), 0f, 1.2f)
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
                            Radius = 1.3f,
                            Strength = 0.036f,
                            Thickness = 0.24f,
                            Speed = 5.8f,
                            Duration = 0.24f,
                            StrengthOverTime = new ScalarCurve(new[]
                            {
                                new CurveKey(0f, 1f),
                                new CurveKey(0.12f, 0.55f),
                                new CurveKey(0.24f, 0f)
                            })
                        }
                    },
                    new EffectNodeSpec
                    {
                        Name = "impact_light",
                        LocalPosition = new NumVector3(0f, def.lightHeightCells, 0f),
                        PointLight = new PointLightSpec(
                            def.lightRadius * 1.45f,
                            ClampRgb01(light),
                            new ScalarCurve(new[]
                            {
                                new CurveKey(0f, def.lightIntensity * 2.4f),
                                new CurveKey(def.impactDurationSeconds * 0.4f, def.lightIntensity * 0.9f),
                                new CurveKey(def.impactDurationSeconds, 0f)
                            }))
                    }
                },
                InitialState = "Orbit",
                States = new[]
                {
                    new EffectStateSpec { Name = "Orbit", Nodes = new[] { "orbit_trail", "orbit_stardust", "orbit_light" }, Duration = 2f, Loop = true },
                    new EffectStateSpec { Name = VfxStates.Impact, Nodes = new[] { "impact_sparks", "impact_wave", "impact_light" }, Duration = def.impactDurationSeconds, Loop = false }
                }
            };
        }

        private static NumVector4 ToVec4(Color c, float alpha) => new NumVector4(c.r, c.g, c.b, alpha);

        private static NumVector3 ClampRgb01(Color c) =>
            new NumVector3(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b));
    }

    public sealed class LightningLumenVfxWorker : VfxWorker
    {
        private LightningLumenVfxDef def;
        private SpellThemeDef theme;
        private Map map;
        private int startTick;
        private bool struck;
        private float effectiveRadius;
        private float scaleFactor;
        private Vector3 strikePos;

        private Color coreColor;
        private Color primaryColor;
        private Color secondaryColor;

        private readonly List<Vector3> boltPoints = new List<Vector3>(32);
        private LumenRibbonMesh boltMesh;
        private ILumenEffectHandle lumenHandle;

        protected override void OnStart()
        {
            def = Def as LightningLumenVfxDef;
            if (def == null)
            {
                throw new InvalidOperationException("LightningLumenVfxWorker requires LightningLumenVfxDef");
            }

            map = Request.map;
            theme = Request.theme
                ?? SpellDefs.Glyph(SpellDefs.EffectLightning)?.theme
                ?? SpellDefs.Tuning?.defaultTheme;
            if (theme == null)
            {
                throw new InvalidOperationException("LightningLumenVfxWorker could not resolve a SpellThemeDef");
            }

            startTick = Find.TickManager.TicksGame;
            effectiveRadius = Request.width > 0.01f ? Request.width * 0.5f : def.baseStrikeRadius;
            scaleFactor = Mathf.Clamp(effectiveRadius / def.baseStrikeRadius, 0.75f, 3.0f);

            float altY = Altitudes.AltitudeFor(AltitudeLayer.MoteOverhead) + 0.05f;
            strikePos = new Vector3(Request.origin.x, altY, Request.origin.z);

            coreColor = theme.CoreColorUnity;
            primaryColor = theme.PrimaryColorUnity;
            secondaryColor = theme.SecondaryColorUnity;

            boltMesh = new LumenRibbonMesh("WotF_LightningBolt");
            if (def.strikeDelayTicks == 0)
            {
                TriggerMainStrike();
            }
        }

        public override void Update(float deltaSeconds)
        {
            if (Finished)
            {
                return;
            }

            int elapsedTicks = Find.TickManager.TicksGame - startTick;
            if (!struck && elapsedTicks >= def.strikeDelayTicks)
            {
                TriggerMainStrike();
            }

            if (elapsedTicks >= def.totalDurationTicks && (lumenHandle == null || !lumenHandle.IsAlive))
            {
                Finished = true;
            }
            else if (elapsedTicks >= def.totalDurationTicks + 20)
            {
                Finished = true;
            }
        }

        public override void DrawWorld()
        {
            if (Finished || boltMesh == null)
            {
                return;
            }

            int elapsedTicks = Find.TickManager.TicksGame - startTick;
            boltMesh.Clear();

            if (!struck)
            {
                float u = def.strikeDelayTicks > 0 ? Mathf.Clamp01(elapsedTicks / (float)def.strikeDelayTicks) : 1f;
                float ringR = Mathf.Lerp(effectiveRadius * 1.15f, effectiveRadius * 0.42f, u);
                boltMesh.AddTiltedEllipseRing(
                    strikePos,
                    Vector3.right,
                    Vector3.forward,
                    ringR,
                    LumenRibbonMesh.Ortho60AspectZ,
                    32,
                    0.06f,
                    new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.65f * u),
                    theme.hdrIntensity * def.hdrRingMultiplier,
                    def.ribbonEdgePower);
            }
            else
            {
                int strikeElapsed = elapsedTicks - def.strikeDelayTicks;
                if (strikeElapsed <= def.strikeDurationTicks + 8)
                {
                    float t = Mathf.Clamp01(strikeElapsed / (float)Math.Max(1, def.strikeDurationTicks));
                    float boltAlpha = Mathf.Pow(1f - t, 1.25f);
                    if (boltAlpha > 0.02f)
                    {
                        BuildBoltSpline(Find.TickManager.TicksGame / 2 + (int)(strikePos.x * 17f + strikePos.z * 31f));
                        float hdr = theme.hdrIntensity * def.hdrBoltMultiplier * Mathf.Lerp(1.35f, 0.7f, t);
                        boltMesh.AddPolyline(
                            boltPoints,
                            boltPoints.Count,
                            def.boltSheathWidth * Mathf.Lerp(1f, 1.25f, scaleFactor - 1f),
                            new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.72f * boltAlpha),
                            hdr * 0.9f,
                            def.ribbonEdgePower,
                            false,
                            null);
                        boltMesh.AddPolyline(
                            boltPoints,
                            boltPoints.Count,
                            def.boltCoreWidth,
                            new Color(coreColor.r, coreColor.g, coreColor.b, 0.98f * boltAlpha),
                            hdr * 1.4f,
                            def.ribbonEdgePower * 0.85f,
                            false,
                            null);
                    }

                    float ringT = Mathf.Clamp01(strikeElapsed / (float)(def.strikeDurationTicks + 8));
                    float ringAlpha = Mathf.Pow(1f - ringT, 1.4f);
                    float ringR = Mathf.Lerp(effectiveRadius * 0.35f, effectiveRadius, Mathf.Sqrt(ringT));
                    boltMesh.AddTiltedEllipseRing(
                        strikePos,
                        Vector3.right,
                        Vector3.forward,
                        ringR,
                        LumenRibbonMesh.Ortho60AspectZ,
                        40,
                        0.09f * scaleFactor,
                        new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0.78f * ringAlpha),
                        theme.hdrIntensity * def.hdrRingMultiplier,
                        def.ribbonEdgePower);
                    boltMesh.AddRadialGlowDisc(
                        strikePos,
                        effectiveRadius * 0.55f * (1f - 0.45f * ringT),
                        22,
                        new Color(coreColor.r, coreColor.g, coreColor.b, 0.88f * ringAlpha),
                        new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0f),
                        theme.hdrIntensity * def.hdrBoltMultiplier,
                        def.ribbonEdgePower,
                        null);
                }
            }

            boltMesh.Upload();
            if (boltMesh.HasGeometry)
            {
                LumenShaders.DrawLumen(boltMesh.Mesh, LumenShaders.RibbonOver);
            }
        }

        protected override void Release()
        {
            if (lumenHandle != null)
            {
                lumenHandle.Stop(allowFadeOut: false);
                lumenHandle = null;
            }

            if (boltMesh != null)
            {
                boltMesh.Dispose();
                boltMesh = null;
            }
        }

        private void TriggerMainStrike()
        {
            struck = true;
            if (map == Find.CurrentMap && def.strikeSound != null)
            {
                def.strikeSound.PlayOneShot(new TargetInfo(strikePos.ToIntVec3(), map));
            }

            if (RimLumenApi.IsReady)
            {
                lumenHandle = RimLumenApi.Play(new LumenPlayRequest
                {
                    EffectId = def.lumenEffectId,
                    MapIndex = map != null ? map.uniqueID : -1,
                    OriginWorld = new NumVector3(strikePos.x, 0f, strikePos.z),
                    Scale = scaleFactor,
                    RealTime = false
                });
            }
        }

        private void BuildBoltSpline(int seed)
        {
            boltPoints.Clear();
            float skyZOffset = def.boltSkyHeightCells * LumenRibbonMesh.Ortho60HeightZ;
            int segs = def.boltSegments;
            uint rng = (uint)seed * 747796405u + 2891336453u;

            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                float envelope = Mathf.Sin(t * Mathf.PI);
                rng = rng * 747796405u + 2891336453u;
                float jx = ((rng & 0xFFFFu) / 32767.5f - 1f) * def.boltJitterCells * envelope;
                rng = rng * 747796405u + 2891336453u;
                float jz = ((rng & 0xFFFFu) / 32767.5f - 1f) * def.boltJitterCells * 0.45f * envelope;

                float x = strikePos.x + jx;
                float z = Mathf.Lerp(strikePos.z + skyZOffset, strikePos.z, t) + jz;
                boltPoints.Add(new Vector3(x, strikePos.y, z));
            }
        }
    }

    public sealed class LingerLumenVfxWorker : VfxWorker
    {
        private LingerLumenVfxDef def;
        private SpellThemeDef theme;
        private Map map;
        private float radius;
        private int startTick;
        private int lastPulseTick = -99999;
        private bool fading;
        private int fadeStartTick;
        private Vector3 lastPos;

        private Color coreColor;
        private Color primaryColor;
        private Color secondaryColor;

        private LumenRibbonMesh fieldMesh;
        private ILumenEffectHandle lumenHandle;

        protected override void OnStart()
        {
            def = Def as LingerLumenVfxDef;
            if (def == null)
            {
                throw new InvalidOperationException("LingerLumenVfxWorker requires LingerLumenVfxDef");
            }

            map = Request.map;
            theme = Request.theme ?? SpellDefs.Tuning?.defaultTheme;
            if (theme == null)
            {
                throw new InvalidOperationException("LingerLumenVfxWorker could not resolve a SpellThemeDef");
            }

            radius = Request.width > 0.01f ? Request.width * 0.5f : def.defaultRadius;
            startTick = Find.TickManager.TicksGame;
            float altY = Altitudes.AltitudeFor(AltitudeLayer.MoteLow);
            Vector3 origin = Request.follow != null && Request.follow.Spawned ? Request.follow.DrawPos : Request.origin;
            lastPos = new Vector3(origin.x, altY, origin.z);

            coreColor = theme.CoreColorUnity;
            primaryColor = theme.PrimaryColorUnity;
            secondaryColor = theme.SecondaryColorUnity;

            fieldMesh = new LumenRibbonMesh("WotF_LingerField_" + theme.defName);
            if (map == Find.CurrentMap && def.deploySound != null)
            {
                def.deploySound.PlayOneShot(new TargetInfo(lastPos.ToIntVec3(), map));
            }

            if (RimLumenApi.IsReady)
            {
                string effectId = SpellFieldOrbitLumenCatalog.EnsureLingerEffect(def, theme, radius);
                lumenHandle = RimLumenApi.Play(new LumenPlayRequest
                {
                    EffectId = effectId,
                    MapIndex = map != null ? map.uniqueID : -1,
                    OriginWorld = new NumVector3(lastPos.x, 0f, lastPos.z),
                    Follow = FollowField,
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

            if (!fading)
            {
                if (Request.follow != null && Request.follow.Spawned)
                {
                    Vector3 p = Request.follow.DrawPos;
                    lastPos = new Vector3(p.x, lastPos.y, p.z);
                }

                return;
            }

            float fadeElapsed = SecondsSince(fadeStartTick);
            if (fadeElapsed >= def.fadeDurationSeconds && (lumenHandle == null || !lumenHandle.IsAlive))
            {
                Finished = true;
            }
            else if (fadeElapsed >= def.fadeDurationSeconds + def.mistLifetimeMax + 0.25f)
            {
                Finished = true;
            }
        }

        public override void Trigger(string state)
        {
            if (Finished || state != VfxStates.Pulse || fading)
            {
                return;
            }

            lastPulseTick = Find.TickManager.TicksGame;
            if (map == Find.CurrentMap && def.pulseSound != null)
            {
                def.pulseSound.PlayOneShot(new TargetInfo(lastPos.ToIntVec3(), map));
            }

            lumenHandle?.Trigger("pulse_burst");
        }

        public override void FollowLost()
        {
            if (fading)
            {
                return;
            }

            fading = true;
            fadeStartTick = Find.TickManager.TicksGame;
            lumenHandle?.Stop(allowFadeOut: true);
        }

        public override void DrawWorld()
        {
            if (Finished || fieldMesh == null)
            {
                return;
            }

            float alpha = 1f;
            if (fading)
            {
                alpha = 1f - Mathf.Clamp01(SecondsSince(fadeStartTick) / def.fadeDurationSeconds);
                if (alpha <= 0.005f)
                {
                    return;
                }
            }

            float elapsed = SecondsSince(startTick);
            float fadeIn = Mathf.Clamp01(elapsed / 0.22f);
            alpha *= fadeIn;
            float spin = elapsed * def.spinRadPerSec;
            float hdr = theme.hdrIntensity * def.hdrRingMultiplier;

            fieldMesh.Clear();

            // Soft outer boundary halo + crisp ground energy perimeter ring matching gameplay radius
            fieldMesh.AddTiltedEllipseRing(
                lastPos,
                Vector3.right,
                Vector3.forward,
                radius,
                1f,
                56,
                def.boundaryThickness * 2.8f,
                new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0.28f * alpha),
                hdr * 0.85f,
                def.ribbonEdgePower * 1.2f);
            fieldMesh.AddTiltedEllipseRing(
                lastPos,
                Vector3.right,
                Vector3.forward,
                radius,
                1f,
                56,
                def.boundaryThickness,
                new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.86f * alpha),
                hdr * 1.15f,
                def.ribbonEdgePower);

            // Rotating segmented inner runic arcs
            fieldMesh.AddSegmentedEllipseArcs(
                lastPos,
                radius * def.innerArcRadiusFactor,
                1f,
                def.innerArcCount,
                0.62f,
                spin,
                12,
                def.boundaryThickness * 0.78f,
                new Color(coreColor.r, coreColor.g, coreColor.b, 0.72f * alpha),
                hdr * 1.1f,
                def.ribbonEdgePower,
                null);

            // Expanding radial pulse wave on each Linger beat
            float pulseElapsed = SecondsSince(lastPulseTick);
            if (lastPulseTick >= 0 && pulseElapsed < def.pulseWaveDurationSeconds)
            {
                float pt = Mathf.Clamp01(pulseElapsed / def.pulseWaveDurationSeconds);
                float waveR = Mathf.Lerp(0.15f, radius, Mathf.Sqrt(pt));
                float waveA = Mathf.Pow(1f - pt, 1.35f) * alpha;
                fieldMesh.AddTiltedEllipseRing(
                    lastPos,
                    Vector3.right,
                    Vector3.forward,
                    waveR,
                    1f,
                    48,
                    def.boundaryThickness * 1.65f,
                    new Color(coreColor.r, coreColor.g, coreColor.b, 0.88f * waveA),
                    hdr * 1.45f,
                    def.ribbonEdgePower);
            }

            fieldMesh.Upload();
            if (fieldMesh.HasGeometry)
            {
                LumenShaders.DrawLumen(fieldMesh.Mesh, LumenShaders.RibbonDepth);
            }
        }

        protected override void Release()
        {
            if (lumenHandle != null)
            {
                lumenHandle.Stop(allowFadeOut: false);
                lumenHandle = null;
            }

            if (fieldMesh != null)
            {
                fieldMesh.Dispose();
                fieldMesh = null;
            }
        }

        private bool FollowField(out NumVector3 worldPosition, out float rotationDegrees)
        {
            rotationDegrees = 0f;
            if (!fading && Request.follow != null && Request.follow.Spawned && Request.follow.Map == map)
            {
                Vector3 p = Request.follow.DrawPos;
                lastPos = new Vector3(p.x, lastPos.y, p.z);
                worldPosition = new NumVector3(lastPos.x, 0f, lastPos.z);
                return true;
            }

            worldPosition = new NumVector3(lastPos.x, 0f, lastPos.z);
            return false;
        }

        private static float SecondsSince(int tick)
        {
            long delta = (long)Find.TickManager.TicksGame - tick;
            return delta <= 0 ? 0f : delta / (float)GenTicks.TicksPerRealSecond;
        }
    }

    public sealed class RuneLumenVfxWorker : VfxWorker
    {
        private enum Phase
        {
            Idle,
            Impacting,
            Fading
        }

        private RuneLumenVfxDef def;
        private SpellThemeDef theme;
        private Map map;
        private Phase phase = Phase.Idle;
        private int startTick;
        private int transitionTick;
        private Vector3 sealPos;

        private Color coreColor;
        private Color primaryColor;
        private Color secondaryColor;

        private LumenRibbonMesh sealMesh;
        private ILumenEffectHandle lumenHandle;

        protected override void OnStart()
        {
            def = Def as RuneLumenVfxDef;
            if (def == null)
            {
                throw new InvalidOperationException("RuneLumenVfxWorker requires RuneLumenVfxDef");
            }

            map = Request.map;
            theme = Request.theme ?? SpellDefs.Tuning?.defaultTheme;
            if (theme == null)
            {
                throw new InvalidOperationException("RuneLumenVfxWorker could not resolve a SpellThemeDef");
            }

            startTick = Find.TickManager.TicksGame;
            float altY = Altitudes.AltitudeFor(AltitudeLayer.MoteLow);
            Vector3 origin = Request.follow != null && Request.follow.Spawned ? Request.follow.DrawPos : Request.origin;
            sealPos = new Vector3(origin.x, altY, origin.z);

            coreColor = theme.CoreColorUnity;
            primaryColor = theme.PrimaryColorUnity;
            secondaryColor = theme.SecondaryColorUnity;

            sealMesh = new LumenRibbonMesh("WotF_RuneSeal_" + theme.defName);
            if (map == Find.CurrentMap && def.placeSound != null)
            {
                def.placeSound.PlayOneShot(new TargetInfo(sealPos.ToIntVec3(), map));
            }

            if (RimLumenApi.IsReady)
            {
                string effectId = SpellFieldOrbitLumenCatalog.EnsureRuneEffect(def, theme);
                lumenHandle = RimLumenApi.Play(new LumenPlayRequest
                {
                    EffectId = effectId,
                    MapIndex = map != null ? map.uniqueID : -1,
                    OriginWorld = new NumVector3(sealPos.x, 0f, sealPos.z),
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

            if (phase == Phase.Impacting)
            {
                float elapsed = SecondsSince(transitionTick);
                if (elapsed >= def.impactDurationSeconds && (lumenHandle == null || !lumenHandle.IsAlive))
                {
                    Finished = true;
                }
                else if (elapsed >= def.impactDurationSeconds + 0.25f)
                {
                    Finished = true;
                }

                return;
            }

            if (phase == Phase.Fading)
            {
                float elapsed = SecondsSince(transitionTick);
                if (elapsed >= def.fadeDurationSeconds && (lumenHandle == null || !lumenHandle.IsAlive))
                {
                    Finished = true;
                }
                else if (elapsed >= def.fadeDurationSeconds + 0.9f)
                {
                    Finished = true;
                }
            }
        }

        public override void Trigger(string state)
        {
            if (Finished || state != VfxStates.Impact || phase == Phase.Impacting)
            {
                return;
            }

            phase = Phase.Impacting;
            transitionTick = Find.TickManager.TicksGame;
            if (map == Find.CurrentMap && theme.castReleaseSound != null)
            {
                theme.castReleaseSound.PlayOneShot(new TargetInfo(sealPos.ToIntVec3(), map));
            }

            if (lumenHandle != null && lumenHandle.IsAlive)
            {
                lumenHandle.GoTo(VfxStates.Impact);
            }
        }

        public override void FollowLost()
        {
            if (phase != Phase.Idle)
            {
                return;
            }

            phase = Phase.Fading;
            transitionTick = Find.TickManager.TicksGame;
            lumenHandle?.Stop(allowFadeOut: true);
        }

        public override void DrawWorld()
        {
            if (Finished || sealMesh == null)
            {
                return;
            }

            sealMesh.Clear();
            float elapsed = SecondsSince(startTick);

            if (phase == Phase.Impacting)
            {
                float t = Mathf.Clamp01(SecondsSince(transitionTick) / def.impactDurationSeconds);
                float alpha = Mathf.Pow(1f - t, 1.35f);
                if (alpha > 0.01f)
                {
                    float hdr = theme.hdrIntensity * def.hdrImpactMultiplier;
                    float ringExpand = Mathf.Lerp(1f, 1.45f, Mathf.Sqrt(t));
                    sealMesh.AddTiltedEllipseRing(
                        sealPos,
                        Vector3.right,
                        Vector3.forward,
                        def.sealRadius * ringExpand,
                        1f,
                        36,
                        def.sealThickness * 1.8f,
                        new Color(coreColor.r, coreColor.g, coreColor.b, 0.94f * alpha),
                        hdr * 1.25f,
                        def.ribbonEdgePower);
                    var columnCenter = new Vector3(
                        sealPos.x,
                        Altitudes.AltitudeFor(AltitudeLayer.MoteOverhead),
                        sealPos.z + def.impactSpindleHeightCells * 0.5f * (1f - 0.2f * t));
                    sealMesh.AddVerticalSpindle(
                        columnCenter,
                        def.impactSpindleHeightCells * 0.5f,
                        def.impactSpindleWidth * (1f - 0.65f * t),
                        16,
                        new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0.25f * alpha),
                        new Color(coreColor.r, coreColor.g, coreColor.b, 0.95f * alpha),
                        hdr * 1.35f,
                        def.ribbonEdgePower,
                        null);
                }
            }
            else
            {
                float alpha = phase == Phase.Fading
                    ? 1f - Mathf.Clamp01(SecondsSince(transitionTick) / def.fadeDurationSeconds)
                    : Mathf.Clamp01(elapsed / 0.2f);
                if (alpha > 0.01f)
                {
                    float breath = 0.85f + 0.15f * Mathf.Sin(elapsed * 2.6f);
                    float spin = elapsed * def.spinRadPerSec;
                    float hdr = theme.hdrIntensity * def.hdrSealMultiplier * breath;

                    sealMesh.AddRadialGlowDisc(
                        sealPos,
                        def.sealRadius * 1.15f,
                        24,
                        new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0.26f * alpha),
                        new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0f),
                        hdr * 0.85f,
                        def.ribbonEdgePower,
                        null);
                    sealMesh.AddTiltedEllipseRing(
                        sealPos,
                        Vector3.right,
                        Vector3.forward,
                        def.sealRadius,
                        1f,
                        36,
                        def.sealThickness,
                        new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.88f * alpha),
                        hdr,
                        def.ribbonEdgePower);
                    sealMesh.AddSegmentedEllipseArcs(
                        sealPos,
                        def.sealRadius * 0.74f,
                        1f,
                        6,
                        0.68f,
                        spin,
                        8,
                        def.sealThickness * 0.85f,
                        new Color(coreColor.r, coreColor.g, coreColor.b, 0.82f * alpha),
                        hdr * 1.15f,
                        def.ribbonEdgePower,
                        null);
                    BuildInscribedStar(sealPos, def.sealRadius * 0.68f, -spin * 0.75f, def.sealThickness * 0.65f, alpha, hdr);
                }
            }

            sealMesh.Upload();
            if (sealMesh.HasGeometry)
            {
                Material mat = phase == Phase.Impacting ? LumenShaders.RibbonOver : LumenShaders.RibbonDepth;
                LumenShaders.DrawLumen(sealMesh.Mesh, mat);
            }
        }

        protected override void Release()
        {
            if (lumenHandle != null)
            {
                lumenHandle.Stop(allowFadeOut: false);
                lumenHandle = null;
            }

            if (sealMesh != null)
            {
                sealMesh.Dispose();
                sealMesh = null;
            }
        }

        private void BuildInscribedStar(Vector3 center, float r, float angleOffset, float width, float alpha, float hdr)
        {
            var col = new Color(coreColor.r, coreColor.g, coreColor.b, 0.68f * alpha);
            for (int tri = 0; tri < 2; tri++)
            {
                float baseA = angleOffset + tri * (Mathf.PI / 3f);
                for (int edge = 0; edge < 3; edge++)
                {
                    float a0 = baseA + edge * (Mathf.PI * 2f / 3f);
                    float a1 = baseA + (edge + 1) * (Mathf.PI * 2f / 3f);
                    var p0 = new Vector3(center.x + Mathf.Cos(a0) * r, center.y, center.z + Mathf.Sin(a0) * r);
                    var p1 = new Vector3(center.x + Mathf.Cos(a1) * r, center.y, center.z + Mathf.Sin(a1) * r);
                    sealMesh.AddTaperedSegment(p0, p1, width, width, col, col, hdr, def.ribbonEdgePower, null);
                }
            }
        }

        private static float SecondsSince(int tick)
        {
            long delta = (long)Find.TickManager.TicksGame - tick;
            return delta <= 0 ? 0f : delta / (float)GenTicks.TicksPerRealSecond;
        }
    }

    public sealed class OrbitLumenVfxWorker : VfxWorker
    {
        private enum Phase
        {
            Orbiting,
            Impacting,
            Fading
        }

        private OrbitLumenVfxDef def;
        private SpellThemeDef theme;
        private Map map;
        private Phase phase = Phase.Orbiting;
        private int startTick;
        private int transitionTick;
        private Vector3 lastOrbPos;

        private Color coreColor;
        private Color primaryColor;
        private Color secondaryColor;

        private LumenRibbonMesh orbMesh;
        private ILumenEffectHandle lumenHandle;

        protected override void OnStart()
        {
            def = Def as OrbitLumenVfxDef;
            if (def == null)
            {
                throw new InvalidOperationException("OrbitLumenVfxWorker requires OrbitLumenVfxDef");
            }

            map = Request.map;
            theme = Request.theme ?? SpellDefs.Tuning?.defaultTheme;
            if (theme == null)
            {
                throw new InvalidOperationException("OrbitLumenVfxWorker could not resolve a SpellThemeDef");
            }

            startTick = Find.TickManager.TicksGame;
            float altY = Altitudes.AltitudeFor(AltitudeLayer.Projectile) + 0.04f;
            Vector3 origin = Request.follow != null && Request.follow.Spawned ? Request.follow.DrawPos : Request.origin;
            lastOrbPos = new Vector3(origin.x, altY, origin.z);

            coreColor = theme.CoreColorUnity;
            primaryColor = theme.PrimaryColorUnity;
            secondaryColor = theme.SecondaryColorUnity;

            orbMesh = new LumenRibbonMesh("WotF_OrbitOrb_" + theme.defName);
            if (map == Find.CurrentMap && def.spawnSound != null)
            {
                def.spawnSound.PlayOneShot(new TargetInfo(lastOrbPos.ToIntVec3(), map));
            }

            if (RimLumenApi.IsReady)
            {
                string effectId = SpellFieldOrbitLumenCatalog.EnsureOrbitEffect(def, theme);
                lumenHandle = RimLumenApi.Play(new LumenPlayRequest
                {
                    EffectId = effectId,
                    MapIndex = map != null ? map.uniqueID : -1,
                    OriginWorld = new NumVector3(lastOrbPos.x, 0f, lastOrbPos.z),
                    Follow = FollowOrb,
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

            if (phase == Phase.Orbiting)
            {
                RefreshOrbPosition();
                return;
            }

            float targetDuration = phase == Phase.Impacting ? def.impactDurationSeconds : def.fadeDurationSeconds;
            float elapsed = SecondsSince(transitionTick);
            if (elapsed >= targetDuration && (lumenHandle == null || !lumenHandle.IsAlive))
            {
                Finished = true;
            }
            else if (elapsed >= targetDuration + def.trailDurationSeconds + 0.25f)
            {
                Finished = true;
            }
        }

        public override void Trigger(string state)
        {
            if (Finished || state != VfxStates.Impact || phase == Phase.Impacting)
            {
                return;
            }

            RefreshOrbPosition();
            phase = Phase.Impacting;
            transitionTick = Find.TickManager.TicksGame;
            if (map == Find.CurrentMap && theme.castReleaseSound != null)
            {
                theme.castReleaseSound.PlayOneShot(new TargetInfo(lastOrbPos.ToIntVec3(), map));
            }

            if (lumenHandle != null && lumenHandle.IsAlive)
            {
                lumenHandle.GoTo(VfxStates.Impact);
            }
        }

        public override void FollowLost()
        {
            if (phase != Phase.Orbiting)
            {
                return;
            }

            phase = Phase.Fading;
            transitionTick = Find.TickManager.TicksGame;
            lumenHandle?.Stop(allowFadeOut: true);
        }

        public override void DrawWorld()
        {
            if (Finished || orbMesh == null)
            {
                return;
            }

            if (phase == Phase.Orbiting)
            {
                RefreshOrbPosition();
            }

            orbMesh.Clear();
            float elapsed = SecondsSince(startTick);

            if (phase == Phase.Impacting)
            {
                float t = Mathf.Clamp01(SecondsSince(transitionTick) / def.impactDurationSeconds);
                float alpha = Mathf.Pow(1f - t, 1.4f);
                if (alpha > 0.01f)
                {
                    float expand = t < 0.22f ? Mathf.Lerp(0.7f, 1.35f, t / 0.22f) : Mathf.Lerp(1.35f, 0.25f, (t - 0.22f) / 0.78f);
                    float hdr = theme.hdrIntensity * def.hdrImpactMultiplier;
                    orbMesh.AddRadialGlowDisc(
                        lastOrbPos,
                        def.haloRadius * 1.6f * expand,
                        18,
                        new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.55f * alpha),
                        new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0f),
                        hdr * 0.9f,
                        def.ribbonEdgePower,
                        null);
                    orbMesh.AddRadialGlowDisc(
                        lastOrbPos,
                        def.coreRadius * 1.45f * expand,
                        16,
                        new Color(coreColor.r, coreColor.g, coreColor.b, 0.96f * alpha),
                        new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0f),
                        hdr * 1.4f,
                        def.ribbonEdgePower * 0.85f,
                        null);
                }
            }
            else
            {
                float scale = phase == Phase.Fading
                    ? 1f - Mathf.Clamp01(SecondsSince(transitionTick) / def.fadeDurationSeconds)
                    : Mathf.Clamp01(elapsed / 0.14f);
                if (scale > 0.01f)
                {
                    float spin = elapsed * def.spinRadPerSec;
                    float hdr = theme.hdrIntensity * def.hdrCoreMultiplier;

                    orbMesh.AddSegmentedEllipseArcs(
                        lastOrbPos,
                        def.haloRadius * scale,
                        LumenRibbonMesh.Ortho60AspectZ,
                        3,
                        0.58f,
                        spin,
                        8,
                        0.042f,
                        new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.78f * scale),
                        hdr * 0.95f,
                        def.ribbonEdgePower,
                        null);
                    orbMesh.AddRadialGlowDisc(
                        lastOrbPos,
                        def.haloRadius * 0.95f * scale,
                        16,
                        new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.45f * scale),
                        new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0f),
                        hdr * 0.9f,
                        def.ribbonEdgePower,
                        null);
                    orbMesh.AddRadialGlowDisc(
                        lastOrbPos,
                        def.coreRadius * scale,
                        14,
                        new Color(coreColor.r, coreColor.g, coreColor.b, 0.96f * scale),
                        new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0f),
                        hdr * 1.35f,
                        def.ribbonEdgePower * 0.85f,
                        null);
                }
            }

            orbMesh.Upload();
            if (orbMesh.HasGeometry)
            {
                LumenShaders.DrawLumen(orbMesh.Mesh, LumenShaders.RibbonOver);
            }
        }

        protected override void Release()
        {
            if (lumenHandle != null)
            {
                lumenHandle.Stop(allowFadeOut: false);
                lumenHandle = null;
            }

            if (orbMesh != null)
            {
                orbMesh.Dispose();
                orbMesh = null;
            }
        }

        private void RefreshOrbPosition()
        {
            if (Request.follow != null && Request.follow.Spawned && Request.follow.Map == map)
            {
                Vector3 p = Request.follow.DrawPos;
                lastOrbPos = new Vector3(p.x, lastOrbPos.y, p.z);
            }
        }

        private bool FollowOrb(out NumVector3 worldPosition, out float rotationDegrees)
        {
            rotationDegrees = 0f;
            if (phase == Phase.Orbiting && Request.follow != null && Request.follow.Spawned && Request.follow.Map == map)
            {
                RefreshOrbPosition();
                worldPosition = new NumVector3(lastOrbPos.x, 0f, lastOrbPos.z);
                return true;
            }

            worldPosition = new NumVector3(lastOrbPos.x, 0f, lastOrbPos.z);
            return false;
        }

        private static float SecondsSince(int tick)
        {
            long delta = (long)Find.TickManager.TicksGame - tick;
            return delta <= 0 ? 0f : delta / (float)GenTicks.TicksPerRealSecond;
        }
    }

    public sealed class TornadoLumenVfxWorker : VfxWorker
    {
        private TornadoLumenVfxDef def;
        private Map map;
        private float radius;
        private float scaleFactor;
        private int startTick;
        private bool collapsing;
        private int collapseStartTick;
        private Vector3 basePos;

        private Color groundBoundaryColor;

        private LumenRibbonMesh funnelBodyMesh;
        private LumenRibbonMesh boundaryMesh;
        private ILumenEffectHandle lumenHandle;
        private Sustainer loopSustainer;

        protected override void OnStart()
        {
            def = Def as TornadoLumenVfxDef;
            if (def == null)
            {
                throw new InvalidOperationException("TornadoLumenVfxWorker requires TornadoLumenVfxDef");
            }

            map = Request.map;
            radius = Request.width > 0.01f ? Request.width * 0.5f : def.referenceRadius;
            scaleFactor = Mathf.Clamp(radius / def.referenceRadius, 0.5f, 3.0f);
            startTick = Find.TickManager.TicksGame;

            float altY = Altitudes.AltitudeFor(AltitudeLayer.MoteOverhead);
            Vector3 origin = Request.follow != null && Request.follow.Spawned ? Request.follow.DrawPos : Request.origin;
            basePos = new Vector3(origin.x, altY, origin.z);

            groundBoundaryColor = VfxColors.ParseUnity(def.groundBoundaryColorHex);

            funnelBodyMesh = new LumenRibbonMesh("WotF_CordiumTornadoBody");
            boundaryMesh = new LumenRibbonMesh("WotF_CordiumTornadoBoundary");
            if (map == Find.CurrentMap)
            {
                var targetInfo = Request.follow != null && Request.follow.Spawned
                    ? new TargetInfo(Request.follow)
                    : new TargetInfo(basePos.ToIntVec3(), map);
                if (def.spawnSound != null)
                {
                    def.spawnSound.PlayOneShot(targetInfo);
                }

                if (def.loopSound != null)
                {
                    loopSustainer = def.loopSound.TrySpawnSustainer(SoundInfo.InMap(targetInfo, MaintenanceType.PerFrame));
                }
            }

            if (RimLumenApi.IsReady)
            {
                lumenHandle = RimLumenApi.Play(new LumenPlayRequest
                {
                    EffectId = def.lumenEffectId,
                    MapIndex = map != null ? map.uniqueID : -1,
                    OriginWorld = new NumVector3(basePos.x, 0f, basePos.z),
                    Scale = scaleFactor,
                    Follow = FollowTornado,
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

            if (!collapsing)
            {
                if (Request.follow != null && Request.follow.Spawned)
                {
                    Vector3 p = Request.follow.DrawPos;
                    basePos = new Vector3(p.x, basePos.y, p.z);
                }

                if (loopSustainer != null && !loopSustainer.Ended)
                {
                    loopSustainer.Maintain();
                }

                return;
            }

            float elapsed = SecondsSince(collapseStartTick);
            if (elapsed >= def.collapseDurationSeconds && (lumenHandle == null || !lumenHandle.IsAlive))
            {
                Finished = true;
            }
            else if (elapsed >= def.collapseDurationSeconds + 3.8f)
            {
                Finished = true;
            }
        }

        public override void Trigger(string state)
        {
            if (Finished || state != VfxStates.Expire || collapsing)
            {
                return;
            }

            BeginCollapse();
        }

        public override void FollowLost()
        {
            if (!collapsing)
            {
                BeginCollapse();
            }
        }

        public override void DrawWorld()
        {
            if (Finished || funnelBodyMesh == null || boundaryMesh == null)
            {
                return;
            }

            float elapsed = SecondsSince(startTick);
            float spawnRamp = Mathf.Clamp01(elapsed / 0.28f);
            float collapseProgress = collapsing
                ? Mathf.Clamp01(SecondsSince(collapseStartTick) / def.collapseDurationSeconds)
                : 0f;
            float collapseFactor = 1f - collapseProgress;
            float alpha = spawnRamp * collapseFactor;
            if (alpha <= 0.01f)
            {
                return;
            }

            funnelBodyMesh.Clear();
            boundaryMesh.Clear();

            float baseR = radius * def.baseRadiusFactor * Mathf.Lerp(0.45f, 1f, collapseFactor);
            float waistR = radius * def.waistRadiusFactor * Mathf.Lerp(0.35f, 1f, collapseFactor);
            float topR = radius * def.topRadiusFactor * Mathf.Lerp(0.35f, 1f, collapseFactor);
            float heightY = radius * def.funnelHeightFactor * Mathf.Lerp(0.45f, 1f, spawnRamp);
            float swayAmp = radius * def.swayAmplitudeFactor;

            // Ground swirling magma collar & procedural multi-octave FBM + ridged noise slender column
            funnelBodyMesh.AddCordiumGroundEye(
                basePos,
                baseR * 2.8f,
                5,
                24,
                elapsed,
                collapseProgress,
                alpha * 0.92f,
                def.hdrFunnelMultiplier);
            funnelBodyMesh.AddCordiumTornadoFunnel3D(
                basePos,
                baseR,
                waistR,
                topR,
                heightY,
                swayAmp,
                def.heightSteps,
                def.crossSteps,
                def.flameTongueCount,
                elapsed,
                collapseProgress,
                alpha,
                def.hdrFunnelMultiplier);

            // Scorched ground perimeter warning arcs showing full gameplay radius
            float spin = elapsed * 3.4f;
            boundaryMesh.AddSegmentedEllipseArcs(
                basePos,
                radius * 0.96f * collapseFactor,
                LumenRibbonMesh.Ortho60AspectZ,
                5,
                0.62f,
                spin,
                10,
                0.075f * scaleFactor,
                new Color(groundBoundaryColor.r, groundBoundaryColor.g, groundBoundaryColor.b, 0.50f * alpha),
                def.hdrFunnelMultiplier * 0.72f,
                def.ribbonEdgePower,
                null);

            funnelBodyMesh.Upload();
            if (funnelBodyMesh.HasGeometry)
            {
                LumenShaders.DrawLumen(funnelBodyMesh.Mesh, LumenShaders.Tornado);
            }

            boundaryMesh.Upload();
            if (boundaryMesh.HasGeometry)
            {
                LumenShaders.DrawLumen(boundaryMesh.Mesh, LumenShaders.RibbonOver);
            }
        }

        protected override void Release()
        {
            StopLoopSustainer();
            if (lumenHandle != null)
            {
                lumenHandle.Stop(allowFadeOut: false);
                lumenHandle = null;
            }

            if (funnelBodyMesh != null)
            {
                funnelBodyMesh.Dispose();
                funnelBodyMesh = null;
            }

            if (boundaryMesh != null)
            {
                boundaryMesh.Dispose();
                boundaryMesh = null;
            }
        }

        private void BeginCollapse()
        {
            collapsing = true;
            collapseStartTick = Find.TickManager.TicksGame;
            StopLoopSustainer();
            if (map == Find.CurrentMap && def.endSound != null)
            {
                def.endSound.PlayOneShot(new TargetInfo(basePos.ToIntVec3(), map));
            }

            lumenHandle?.Stop(allowFadeOut: true);
        }

        private void StopLoopSustainer()
        {
            if (loopSustainer != null)
            {
                if (!loopSustainer.Ended)
                {
                    loopSustainer.End();
                }

                loopSustainer = null;
            }
        }

        private bool FollowTornado(out NumVector3 worldPosition, out float rotationDegrees)
        {
            rotationDegrees = 0f;
            if (!collapsing && Request.follow != null && Request.follow.Spawned && Request.follow.Map == map)
            {
                Vector3 p = Request.follow.DrawPos;
                basePos = new Vector3(p.x, basePos.y, p.z);
                worldPosition = new NumVector3(basePos.x, 0f, basePos.z);
                return true;
            }

            worldPosition = new NumVector3(basePos.x, 0f, basePos.z);
            return false;
        }

        private static float SecondsSince(int tick)
        {
            long delta = (long)Find.TickManager.TicksGame - tick;
            return delta <= 0 ? 0f : delta / (float)GenTicks.TicksPerRealSecond;
        }
    }
}
