using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Spells
{
    public sealed class CompProperties_TornadoSkyTint : CompProperties_AffectsSky
    {
        public int detonationRiseTicks;
        public int detonationDecayTicks;
        public float peakLerpFactor;
        public float sustainLerpFactor;
        public float pulseWaveAmplitude;

        public CompProperties_TornadoSkyTint()
        {
            compClass = typeof(CompTornadoSkyTint);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (detonationRiseTicks <= 0 || detonationDecayTicks <= 0)
            {
                yield return "detonationRiseTicks and detonationDecayTicks must be positive";
            }

            if (peakLerpFactor <= 0f || peakLerpFactor > 1f)
            {
                yield return "peakLerpFactor must be in (0, 1]";
            }

            if (sustainLerpFactor <= 0f || sustainLerpFactor > peakLerpFactor)
            {
                yield return "sustainLerpFactor must be in (0, peakLerpFactor]";
            }

            if (pulseWaveAmplitude < 0f || sustainLerpFactor + pulseWaveAmplitude > 1f)
            {
                yield return "pulseWaveAmplitude must be non-negative and sustainLerpFactor + pulseWaveAmplitude <= 1";
            }
        }
    }

    /// <summary>
    /// Tints the entire map's sky with a Project Wingman Cordium crimson-orange atmosphere while a
    /// flame tornado is active: brief bright detonation surge at spawn, sustained magma glow during
    /// active pulses, and smooth dissipation across <see cref="Thing_SpellTornado.FadeTicks"/>.
    /// </summary>
    public sealed class CompTornadoSkyTint : CompAffectsSky
    {
        public new CompProperties_TornadoSkyTint Props => (CompProperties_TornadoSkyTint)props;

        public override float LerpFactor
        {
            get
            {
                if (!(parent is Thing_SpellTornado tornado) || !tornado.Spawned)
                {
                    return 0f;
                }

                CompProperties_TornadoSkyTint p = Props;
                return EvaluateLerpFactor(
                    Find.TickManager.TicksGame,
                    tornado.SpawnTick,
                    tornado.ExpiresTick,
                    tornado.FadeTicks,
                    tornado.PulseIntervalTicks,
                    p.detonationRiseTicks,
                    p.detonationDecayTicks,
                    p.peakLerpFactor,
                    p.sustainLerpFactor,
                    p.pulseWaveAmplitude);
            }
        }

        public static float EvaluateLerpFactor(
            int nowTick,
            int spawnTick,
            int expiresTick,
            int fadeTicks,
            int pulseIntervalTicks,
            int detonationRiseTicks,
            int detonationDecayTicks,
            float peakLerpFactor,
            float sustainLerpFactor,
            float pulseWaveAmplitude)
        {
            int endTick = expiresTick + Math.Max(0, fadeTicks);
            if (nowTick < spawnTick || nowTick >= endTick)
            {
                return 0f;
            }

            int age = nowTick - spawnTick;
            float baseFactor;
            if (age < detonationRiseTicks)
            {
                float u = age / (float)Math.Max(1, detonationRiseTicks);
                baseFactor = Mathf.Lerp(0f, peakLerpFactor, u * u * (3f - 2f * u));
            }
            else if (age < detonationRiseTicks + detonationDecayTicks)
            {
                float u = (age - detonationRiseTicks) / (float)Math.Max(1, detonationDecayTicks);
                baseFactor = Mathf.Lerp(peakLerpFactor, sustainLerpFactor, u * u * (3f - 2f * u));
            }
            else
            {
                float phase = pulseIntervalTicks > 0
                    ? (age / (float)pulseIntervalTicks) * Mathf.PI * 2f
                    : 0f;
                baseFactor = sustainLerpFactor + pulseWaveAmplitude * Mathf.Sin(phase);
            }

            if (nowTick >= expiresTick)
            {
                if (fadeTicks <= 0)
                {
                    return 0f;
                }

                float fadeLeft = 1f - (nowTick - expiresTick) / (float)fadeTicks;
                baseFactor *= Mathf.Clamp01(fadeLeft);
            }

            return Mathf.Clamp01(baseFactor);
        }
    }
}
