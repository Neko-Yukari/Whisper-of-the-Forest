using System;
using RimUnity.ScreenEfx;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>
    /// The railgun as RimUnity world geometry.
    ///
    /// The shot itself is unchanged: the spell asks for an effect, the worker plays a gathering orb for
    /// the channel, then shows the whole beam in one frame, throws short jagged bolts along it and sends shock
    /// rings down its length while a selective screen pass dims everything that is not bright.
    ///
    /// All timing comes from <c>TicksGame</c>, so the effect freezes on pause and follows game speed; it
    /// never reads or writes the map camera except to place the pre-squashed ring mode.
    /// </summary>
    public class RailgunRimVfxWorker : VfxWorker
    {
        private const float MinVisible = 0.001f;

        private RailgunRimVfxDef def;
        private Map map;
        private Thing follow;
        private Vector3 aim;

        private int startTick;
        private int chargeTicks;
        private bool fired;
        private int fireTick;

        private Vector3 beamOrigin;
        private float dirX;
        private float dirZ;
        private float beamLength;
        private int arcSeed;

        private RimUnity.Color coreColor;
        private RimUnity.Color glowColor;
        private RimUnity.Color arcColor;
        private RimUnity.Color ringColor;
        private RimUnity.Color chargeColor;

        private RimUnity.Material material;
        private RimUnity.Material darkenTemplate;
        private VfxLine core;
        private VfxLine glow;
        private VfxLine orb;
        private VfxLine[] bolts;
        private int[] boltSegments;
        private float[] boltLength;
        private float[] boltSeed;
        private int[] boltCycle;
        private VfxLine[] rings;
        private bool[] ringFlash;
        private bool built;

        private EfxHandle darkenHandle;
        private int heldLayer = -1;

        private float TotalSeconds
        {
            get
            {
                float beam = def.beamHoldSeconds + def.beamFadeSeconds;
                float glowEnd = def.beamHoldSeconds + def.glowLingerSeconds + def.beamFadeSeconds;
                // The longest a ring can live: its main pulse plus the optional re-ionisation flash.
                float ringLife = def.ringMainPulseSeconds + def.ringFlashDelaySeconds + def.ringFlashSeconds;
                float lastAcross = beamLength * def.ringCount / (def.ringCount + 1f);
                float shock = ShockSpeedCellsPerSecond;
                float lastArrival = shock > 0f ? lastAcross / shock : 0f;
                float ringEnd = lastArrival + ringLife;
                float darkenEnd = def.darkenInSeconds + def.darkenHoldSeconds + def.darkenOutSeconds;
                return Mathf.Max(Mathf.Max(beam, glowEnd), Mathf.Max(ringEnd, darkenEnd));
            }
        }

        /// <summary>
        /// The front covers the beam within <c>beamHoldSeconds * ringShockArrivalFraction</c>, so even the
        /// longest map diagonal is reached before the beam starts to fade and a short shot takes exactly
        /// as long as a long one. Zero until the shot length is known.
        /// </summary>
        private float ShockSpeedCellsPerSecond
        {
            get
            {
                float window = def.beamHoldSeconds * def.ringShockArrivalFraction;
                return beamLength > 0f && window > 0f ? beamLength / window : 0f;
            }
        }

        protected override void OnStart()
        {
            def = Def as RailgunRimVfxDef;
            if (def == null)
            {
                throw new InvalidOperationException(
                    "RailgunRimVfxWorker needs a RailgunRimVfxDef, got " + (Def != null ? Def.defName : "no def"));
            }

            map = Request.map;
            follow = Request.follow;
            aim = Request.target;
            chargeTicks = Mathf.Max(0, Request.chargeTicks);
            startTick = Find.TickManager.TicksGame;
            arcSeed = Gen.HashCombineInt(startTick, map.uniqueID);

            coreColor = VfxColors.Parse(def.coreColor);
            glowColor = VfxColors.Parse(def.glowColor);
            arcColor = VfxColors.Parse(def.arcColor);
            ringColor = VfxColors.Parse(def.ringColor);
            chargeColor = VfxColors.Parse(def.chargeColor);

            VfxBloomLayers.Acquire(def.bloomLayer);
            heldLayer = def.bloomLayer;

            if (chargeTicks <= 0)
            {
                Fire();
            }
        }

        public override void Update(float deltaSeconds)
        {
            if (Finished)
            {
                return;
            }

            if (!fired && Find.TickManager.TicksGame - startTick >= chargeTicks)
            {
                Fire();
            }

            // The effect's own clock never pauses for a map switch, so the shot ends on time even while
            // the player is looking at the world map.
            float elapsed = fired ? SecondsSince(fireTick) : 0f;
            if (fired && elapsed >= TotalSeconds)
            {
                Finished = true;
                return;
            }

            if (map != Find.CurrentMap)
            {
                HideAll();
                return;
            }

            EnsureBuilt();
            if (fired)
            {
                UpdateShot(elapsed);
            }
            else
            {
                UpdateCharge();
            }
        }

        protected override void Release()
        {
            HideAll();
            DestroyBolts();
            DestroyRings();
            if (built)
            {
                DestroyLines();
            }

            if (heldLayer >= 0)
            {
                VfxBloomLayers.Release(heldLayer);
                heldLayer = -1;
            }
        }

        /// <summary>
        /// Locks the geometry at the tick the charge ends. The origin is where the caster stands now, the
        /// aim point is the one locked at cast time, and the length is shortened to the map edge.
        /// </summary>
        private void Fire()
        {
            fired = true;
            fireTick = Find.TickManager.TicksGame;

            Vector3 start = follow != null && follow.Spawned ? follow.DrawPos : Request.origin;
            float dx = aim.x - start.x;
            float dz = aim.z - start.z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            if (distance > 0.0001f)
            {
                dirX = dx / distance;
                dirZ = dz / distance;
            }
            else
            {
                dirX = Request.direction.x;
                dirZ = Request.direction.z;
                float fallback = Mathf.Sqrt(dirX * dirX + dirZ * dirZ);
                if (fallback > 0.0001f)
                {
                    dirX /= fallback;
                    dirZ /= fallback;
                }
                else
                {
                    dirX = 1f;
                    dirZ = 0f;
                }
            }

            beamOrigin = new Vector3(start.x, def.beamHeight, start.z);
            beamLength = RailgunVfxDraw.TryClipToMap(map, start.x, start.z, dirX, dirZ, Request.length, out float clipped)
                ? clipped
                : Request.length;

            BuildBolts();
            BuildRings();

            VfxDiagnostics.Info("railgun shot built: length=" + beamLength.ToString("F1")
                + " bolts=" + (bolts != null ? bolts.Length : 0) + " rings=" + (rings != null ? rings.Length : 0));
        }

        private void EnsureBuilt()
        {
            if (built)
            {
                return;
            }

            EnsureMaterial();

            core = new VfxLine("WotF railgun core", def.bloomLayer, material, 2);
            glow = new VfxLine("WotF railgun glow", def.bloomLayer, material, 2);
            orb = new VfxLine("WotF railgun charge", def.bloomLayer, material, 2);
            orb.SetCapVertices(8);

            core.SetWidth(def.coreWidthCells);
            glow.SetWidth(def.glowWidthCells);

            built = true;
        }

        /// <summary>
        /// Creates the rings only when the shot is fired, exactly like the bolts: nothing that belongs to
        /// the shot exists while the caster is still charging, and the rings are shown from the fire tick
        /// on instead of being born dormant and woken later.
        /// </summary>
        private void BuildRings()
        {
            DestroyRings();
            if (def.ringCount <= 0)
            {
                return;
            }

            EnsureMaterial();
            rings = new VfxLine[def.ringCount];

            // Which rings get a re-ionisation flash is fixed by the cast seed, never by a per-frame roll,
            // so pausing or reloading cannot change the result. Each ring needs its own independent
            // draw: Gen.HashCombineInt(seed, i) only varies the low bits for consecutive i, so its low
            // bits (and any % on them) give every ring almost the same number — an all-or-nothing shot.
            ringFlash = new bool[def.ringCount];
            var pattern = new System.Text.StringBuilder(rings.Length);
            int flashes = 0;
            for (int i = 0; i < rings.Length; i++)
            {
                rings[i] = new VfxLine("WotF railgun ring " + i, def.bloomLayer, material, def.ringSegments + 1);
                ringFlash[i] = RingRoll01(arcSeed, i) < def.ringFlashChance;
                pattern.Append(ringFlash[i] ? '1' : '0');
                if (ringFlash[i])
                {
                    flashes++;
                }
            }

            VfxDiagnostics.Info("railgun rings: count=" + rings.Length + " flashChance=" + def.ringFlashChance
                + " flashes=" + flashes + " pattern=" + pattern);
        }

        /// <summary>
        /// One independent, deterministic draw in [0, 1) per ring. A full 32-bit avalanche mix (not a
        /// plain hash combine) spreads neighbouring indices across the whole range, and the high bits are
        /// used so the result does not depend on low-bit structure at all.
        /// </summary>
        private static float RingRoll01(int seed, int index)
        {
            uint x = unchecked((uint)seed) ^ unchecked((uint)index * 0x9E3779B9u);
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return (x >> 8) * (1f / 16777216f);
        }

        /// <summary>
        /// Builds the individual bolts for the shot's actual length. Each bolt owns a fixed stretch of the
        /// beam and its own point count; only its shape and its place on the beam are re-rolled, so a long
        /// shot keeps the same fine zigzag without allocating new geometry.
        /// </summary>
        private void BuildBolts()
        {
            DestroyBolts();
            if (def.arcCount <= 0)
            {
                return;
            }

            EnsureMaterial();
            var random = new System.Random(arcSeed);
            bolts = new VfxLine[def.arcCount];
            boltSegments = new int[def.arcCount];
            boltLength = new float[def.arcCount];
            boltSeed = new float[def.arcCount];
            boltCycle = new int[def.arcCount];

            for (int i = 0; i < bolts.Length; i++)
            {
                float length = Mathf.Min(beamLength,
                    Mathf.Lerp(def.arcMinLengthCells, def.arcMaxLengthCells, (float)random.NextDouble()));
                int segments = Mathf.Clamp(Mathf.CeilToInt(length * def.arcSegmentsPerCell), 2, def.arcMaxSegments);
                boltLength[i] = length;
                boltSegments[i] = segments;
                boltSeed[i] = (float)random.NextDouble() * 1000f;
                boltCycle[i] = int.MinValue;
                bolts[i] = new VfxLine("WotF railgun bolt " + i, def.bloomLayer, material, segments + 1);
                bolts[i].SetWidth(def.arcThicknessCells);
                bolts[i].SetActive(false);
            }

            VfxDiagnostics.Debug("railgun bolts built: count=" + bolts.Length + " length=" + beamLength.ToString("F1"));
        }

        private void DestroyRings()
        {
            if (rings == null)
            {
                return;
            }

            for (int i = 0; i < rings.Length; i++)
            {
                rings[i].Destroy();
            }

            rings = null;
            ringFlash = null;
        }

        private void DestroyBolts()
        {
            if (bolts == null)
            {
                return;
            }

            for (int i = 0; i < bolts.Length; i++)
            {
                bolts[i].Destroy();
            }

            bolts = null;
            boltSegments = null;
            boltLength = null;
            boltSeed = null;
            boltCycle = null;
        }

        private void EnsureMaterial()
        {
            if (material == null)
            {
                material = RimUnity.Assets.AssetLoader.Load<RimUnity.Material>(def.modContentPack, def.materialPath);
            }
        }

        private void DestroyLines()
        {
            core.Destroy();
            glow.Destroy();
            orb.Destroy();

            core = null;
            glow = null;
            orb = null;
            built = false;
        }

        private void HideAll()
        {
            if (built)
            {
                core.SetActive(false);
                glow.SetActive(false);
                orb.SetActive(false);
            }

            if (rings != null)
            {
                for (int i = 0; i < rings.Length; i++)
                {
                    rings[i].SetActive(false);
                }
            }

            if (bolts != null)
            {
                for (int i = 0; i < bolts.Length; i++)
                {
                    bolts[i].SetActive(false);
                }
            }

            StopDarken();
        }

        private void UpdateCharge()
        {
            float progress = chargeTicks <= 0
                ? 1f
                : Mathf.Clamp01((Find.TickManager.TicksGame - startTick) / (float)chargeTicks);
            float pulse = 0.5f + 0.5f * Mathf.Sin(SecondsSince(startTick) * Mathf.PI * 2f * def.chargePulseHz);
            float radius = def.chargeRadiusCells * (0.35f + 0.65f * progress) * (0.8f + 0.2f * pulse);
            float alpha = 0.25f + 0.75f * progress;

            Vector3 center = follow != null && follow.Spawned ? follow.DrawPos : Request.origin;
            center.y = def.beamHeight;

            SetSegment(orb, center + Vector3.up * 0.02f, center - Vector3.up * 0.02f);
            orb.SetWidth(radius * 2f);
            orb.SetColor(Glow(chargeColor, def.chargeIntensity, alpha));
            orb.SetActive(true);

            core.SetActive(false);
            glow.SetActive(false);
            if (bolts != null)
            {
                for (int i = 0; i < bolts.Length; i++)
                {
                    bolts[i].SetActive(false);
                }
            }

            if (rings != null)
            {
                for (int i = 0; i < rings.Length; i++)
                {
                    rings[i].SetActive(false);
                }
            }

            StopDarken();
        }

        private void UpdateShot(float elapsed)
        {
            float beamAlpha = BeamAlpha(elapsed);
            float glowAlpha = GlowAlpha(elapsed);

            // The beam is whole from the first frame; only its brightness changes afterwards.
            Vector3 end = beamOrigin + new Vector3(dirX, 0f, dirZ) * beamLength;
            SetSegment(core, beamOrigin, end);
            SetSegment(glow, beamOrigin, end);

            core.SetColor(Glow(coreColor, def.coreIntensity, beamAlpha));
            core.SetActive(beamAlpha > MinVisible);

            glow.SetColor(Glow(glowColor, def.glowIntensity, glowAlpha));
            glow.SetActive(glowAlpha > MinVisible);

            orb.SetActive(false);
            UpdateBolts(elapsed, beamAlpha);
            UpdateRings(elapsed);
            UpdateDarken(elapsed);
        }

        /// <summary>
        /// Each bolt lives its own short cycle: it appears at a random stretch of the beam, keeps that
        /// shape for <c>arcLifeSeconds</c>, vanishes for <c>arcGapSeconds</c>, then reappears elsewhere
        /// with a new shape. Nothing wraps the whole beam, so the lightning reads as separate strikes
        /// rather than a helix.
        /// </summary>
        private void UpdateBolts(float elapsed, float beamAlpha)
        {
            if (bolts == null || bolts.Length == 0)
            {
                return;
            }

            bool beamOn = beamAlpha > MinVisible;
            float cycleSeconds = def.arcLifeSeconds + def.arcGapSeconds;

            for (int i = 0; i < bolts.Length; i++)
            {
                float clock = elapsed + boltSeed[i];
                int cycle = Mathf.FloorToInt(clock / cycleSeconds);
                float phase = clock - cycle * cycleSeconds;
                if (!beamOn || phase >= def.arcLifeSeconds)
                {
                    bolts[i].SetActive(false);
                    continue;
                }

                if (cycle != boltCycle[i])
                {
                    boltCycle[i] = cycle;
                    ShapeBolt(i, cycle);
                }

                // Its own fade in and out across the strike, so bolts do not all pop at once.
                float life = phase / def.arcLifeSeconds;
                bolts[i].SetColor(Glow(arcColor, def.arcIntensity, beamAlpha * Mathf.Sin(life * Mathf.PI)));
                bolts[i].SetActive(true);
            }
        }

        /// <summary>Places one bolt: a random stretch of the beam, a random side, a jagged radial path.</summary>
        private void ShapeBolt(int index, int cycle)
        {
            var random = new System.Random(Gen.HashCombineInt(Gen.HashCombineInt(arcSeed, index), cycle));
            float sideX = -dirZ;
            float sideZ = dirX;
            float length = boltLength[index];
            float startAlong = (float)random.NextDouble() * Mathf.Max(0f, beamLength - length);
            float baseAngle = (float)(random.NextDouble() * Math.PI * 2.0);
            float jitterRadians = def.arcAngleJitterDegrees * Mathf.Deg2Rad;
            int segments = boltSegments[index];

            for (int s = 0; s <= segments; s++)
            {
                float along = startAlong + length * s / segments;
                float angle = baseAngle + ((float)random.NextDouble() * 2f - 1f) * jitterRadians;
                float radius = def.arcRadiusCells + ((float)random.NextDouble() * 2f - 1f) * def.arcJitterCells;
                float cos = Mathf.Cos(angle) * radius;
                float sin = Mathf.Sin(angle) * radius;

                bolts[index].SetPoint(s, new RimUnity.Vector3(
                    beamOrigin.x + dirX * along + sideX * cos,
                    beamOrigin.y + sin,
                    beamOrigin.z + dirZ * along + sideZ * cos));
            }
        }

        /// <summary>
        /// A shock front runs along the beam and always arrives within
        /// <c>beamHoldSeconds * ringShockArrivalFraction</c>, so a short shot and a whole-map shot take the
        /// same time and the wave is done before the beam fades. A ring only lights up once
        /// the front passes its own point, stays exactly there, and expands with one long decaying main
        /// pulse. Some rings then get a short, dimmer re-ionisation flash at the same spot — a real shock
        /// does not guarantee a second pulse, so it is decided once per ring from the cast seed.
        ///
        /// The pulse fades in over <c>ringPulseRise</c> and out over <c>ringPulseFall</c>, so a ring is
        /// dim while it is still inside the beam's glow and brightest once it has expanded past it.
        /// </summary>
        private void UpdateRings(float elapsed)
        {
            if (rings == null || rings.Length == 0)
            {
                return;
            }

            RimUnity.Vector3 minor;
            RimUnity.Vector3 major;
            if (!RingBasis(out minor, out major))
            {
                HideRings();
                return;
            }

            float ratio = def.ringMode == RailgunRingMode.Ellipse30 ? def.ringEllipseRatio : 1f;
            float main = def.ringMainPulseSeconds;
            float flashDelay = def.ringFlashDelaySeconds;
            float flash = def.ringFlashSeconds;
            float speed = ShockSpeedCellsPerSecond;

            for (int i = 0; i < rings.Length; i++)
            {
                // Rings sit at fixed fractions of the beam. The front reaches the i-th one after
                // across / speed seconds, so appearance advances outward while the ring itself never moves.
                float fraction = (i + 1f) / (rings.Length + 1f);
                float across = beamLength * fraction;
                float age = elapsed - across / speed;
                if (age < 0f)
                {
                    rings[i].SetActive(false);
                    continue;
                }

                float progress;
                float endRadius;
                float intensity;
                if (age < main)
                {
                    progress = age / main;
                    endRadius = def.ringMainEndRadiusCells;
                    intensity = def.ringIntensity * def.ringMainIntensityScale;
                }
                else if (ringFlash[i] && age < main + flashDelay + flash)
                {
                    float sinceFlash = age - main - flashDelay;
                    if (sinceFlash < 0f)
                    {
                        // The quiet beat between the main pulse and a possible re-ionisation flash.
                        rings[i].SetActive(false);
                        continue;
                    }

                    progress = sinceFlash / flash;
                    endRadius = def.ringFlashRadiusCells;
                    intensity = def.ringIntensity * def.ringFlashIntensityScale;
                }
                else
                {
                    rings[i].SetActive(false);
                    continue;
                }

                float radius = Mathf.Lerp(def.ringStartRadiusCells, endRadius, progress);

                // The ring is anchored where the front first reached it; only radius and brightness change.
                var center = new RimUnity.Vector3(
                    beamOrigin.x + dirX * across,
                    beamOrigin.y,
                    beamOrigin.z + dirZ * across);

                for (int s = 0; s <= def.ringSegments; s++)
                {
                    float turn = s / (float)def.ringSegments * Mathf.PI * 2f;
                    float cos = Mathf.Cos(turn) * radius;
                    float sin = Mathf.Sin(turn) * radius * ratio;
                    rings[i].SetPoint(s, new RimUnity.Vector3(
                        center.x + major.x * cos + minor.x * sin,
                        center.y + major.y * cos + minor.y * sin,
                        center.z + major.z * cos + minor.z * sin));
                }

                rings[i].SetWidth(def.ringThicknessCells);
                rings[i].SetColor(Glow(ringColor, intensity, PulseAlpha(progress)));
                rings[i].SetActive(true);
            }
        }

        /// <summary>Rises over <c>ringPulseRise</c>, holds, then falls over <c>ringPulseFall</c>.</summary>
        private float PulseAlpha(float progress)
        {
            float rise = Mathf.Clamp01(def.ringPulseRise);
            float fall = Mathf.Clamp01(def.ringPulseFall);
            float up = rise <= 0f ? 1f : SmoothStep(progress / rise);
            float down = fall <= 0f ? 1f : SmoothStep((1f - progress) / fall);
            return Mathf.Clamp01(Mathf.Min(up, down));
        }

        private static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private void HideRings()
        {
            if (rings == null)
            {
                return;
            }

            for (int i = 0; i < rings.Length; i++)
            {
                rings[i].SetActive(false);
            }
        }

        /// <summary>
        /// The two in-plane axes of a ring. Perpendicular mode uses the plane across the beam; the 30°
        /// mode puts the short axis along the beam's on-screen direction so the ellipse always reads the
        /// way a ring seen at thirty degrees does.
        /// </summary>
        private bool RingBasis(out RimUnity.Vector3 minor, out RimUnity.Vector3 major)
        {
            var axis = new RimUnity.Vector3(dirX, 0f, dirZ);

            if (def.ringMode == RailgunRingMode.WorldPerpendicular)
            {
                minor = RimUnity.Vector3.Cross(axis, RimUnity.Vector3.up).normalized;
                major = RimUnity.Vector3.Cross(axis, minor).normalized;
                return true;
            }

            Camera camera = Find.Camera;
            if (camera == null)
            {
                minor = default(RimUnity.Vector3);
                major = default(RimUnity.Vector3);
                return false;
            }

            Vector3 forward = camera.transform.forward;
            var view = new RimUnity.Vector3(forward.x, forward.y, forward.z);
            RimUnity.Vector3 projected = axis - view * RimUnity.Vector3.Dot(axis, view);
            if (projected.sqrMagnitude < 0.0001f)
            {
                Vector3 right = camera.transform.right;
                projected = new RimUnity.Vector3(right.x, right.y, right.z);
            }

            minor = projected.normalized;
            major = RimUnity.Vector3.Cross(minor, view).normalized;
            return true;
        }

        private void UpdateDarken(float elapsed)
        {
            float strength = DarkenStrength(elapsed);
            if (strength <= MinVisible || map != Find.CurrentMap)
            {
                StopDarken();
                return;
            }

            if (darkenHandle != null)
            {
                return;
            }

            if (darkenTemplate == null)
            {
                darkenTemplate = RimUnity.Assets.AssetLoader.Load<RimUnity.Material>(
                    def.modContentPack, def.darkenMaterialPath);
            }

            var clip = new RailgunDarkenClip(darkenTemplate, CurrentDarkenStrength, def.darkenKeepLow, def.darkenKeepHigh);
            darkenHandle = EfxPlayer.Play(clip);
        }

        private float CurrentDarkenStrength()
        {
            return fired ? DarkenStrength(SecondsSince(fireTick)) : 0f;
        }

        private float DarkenStrength(float elapsed)
        {
            if (def.darkenStrength <= 0f)
            {
                return 0f;
            }

            float holdStart = def.darkenInSeconds;
            float holdEnd = holdStart + def.darkenHoldSeconds;
            float outEnd = holdEnd + def.darkenOutSeconds;

            if (elapsed <= holdStart)
            {
                return def.darkenStrength * (holdStart <= 0f ? 1f : elapsed / holdStart);
            }

            if (elapsed <= holdEnd)
            {
                return def.darkenStrength;
            }

            if (elapsed < outEnd)
            {
                return def.darkenStrength * (1f - (elapsed - holdEnd) / def.darkenOutSeconds);
            }

            return 0f;
        }

        private void StopDarken()
        {
            if (darkenHandle == null)
            {
                return;
            }

            darkenHandle.Stop();
            darkenHandle = null;
        }

        private float BeamAlpha(float elapsed)
        {
            if (elapsed <= def.beamHoldSeconds)
            {
                return 1f;
            }

            float fade = elapsed - def.beamHoldSeconds;
            if (fade >= def.beamFadeSeconds)
            {
                return 0f;
            }

            float k = 1f - fade / def.beamFadeSeconds;
            return k * k;
        }

        private float GlowAlpha(float elapsed)
        {
            float hold = def.beamHoldSeconds + def.glowLingerSeconds;
            if (elapsed <= hold)
            {
                return 1f;
            }

            float fade = elapsed - hold;
            if (fade >= def.beamFadeSeconds)
            {
                return 0f;
            }

            float k = 1f - fade / def.beamFadeSeconds;
            return k * k;
        }

        private static void SetSegment(VfxLine line, Vector3 from, Vector3 to)
        {
            line.SetPoint(0, new RimUnity.Vector3(from.x, from.y, from.z));
            line.SetPoint(1, new RimUnity.Vector3(to.x, to.y, to.z));
        }

        /// <summary>Colour with a brightness multiplier and a separate opacity for the additive mask.</summary>
        private static RimUnity.Color Glow(RimUnity.Color color, float intensity, float alpha)
        {
            return new RimUnity.Color(
                color.r * intensity,
                color.g * intensity,
                color.b * intensity,
                Mathf.Clamp01(alpha));
        }

        private static float SecondsSince(int tick)
        {
            long delta = (long)Find.TickManager.TicksGame - tick;
            return delta <= 0 ? 0f : delta / (float)GenTicks.TicksPerRealSecond;
        }
    }
}
