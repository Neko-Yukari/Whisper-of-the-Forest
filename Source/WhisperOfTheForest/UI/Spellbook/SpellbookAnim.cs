using UnityEngine;

namespace WhisperOfTheForest
{
    /// <summary>
    /// IMGUI-only page feedback: a glyph flying into the bar, a light band on a bookmark switch, a
    /// shake/red flash for a refused action and a sweep after a successful create.
    ///
    /// Everything is driven by <see cref="Time.realtimeSinceStartup"/>, so a paused game or a
    /// slowed-down UI still animates in real time, and no animation library is involved. The
    /// object only stores timestamps: drawing reads a normalised progress and paints an overlay, so
    /// no layout rectangle ever depends on it. A disabled or finished animation simply returns
    /// false, and nothing here writes to the log.
    /// </summary>
    internal sealed class SpellbookAnim
    {
        /// <summary>Glyph flies from the catalogue row into the bar.</summary>
        public const float AddSeconds = 0.15f;

        /// <summary>Soft band crossing the pages when a bookmark becomes the current slot.</summary>
        public const float FlipSeconds = 0.18f;

        /// <summary>Shake plus red flash when an action is refused.</summary>
        public const float ShakeSeconds = 0.12f;

        /// <summary>Light sweep across the bar after a successful create.</summary>
        public const float SweepSeconds = 0.35f;

        /// <summary>Bead shrinking and fading after it is removed from the bar.</summary>
        public const float RemoveSeconds = 0.12f;

        /// <summary>Far enough in the past that <see cref="Progress"/> always reports "finished".</summary>
        private const float Idle = -1000f;

        /// <summary>Player switch for all of the above; the page keeps working without it.</summary>
        public bool enabled = true;

        private float addStart = Idle;
        private Vector2 addFrom;
        private int addIndex = -1;

        private float removeStart = Idle;
        private Rect removeRect;
        private string removeDefName;

        private float flipStart = Idle;
        private int flipDirection = 1;

        private float shakeStart = Idle;
        private float sweepStart = Idle;

        private static float Now
        {
            get { return Time.realtimeSinceStartup; }
        }

        // ---------------------------------------------------------------- triggers

        public void OnAdd(Vector2 from, int index)
        {
            if (!enabled)
            {
                return;
            }

            addStart = Now;
            addFrom = from;
            addIndex = index;
        }

        public void OnRemove(Rect bead, string defName)
        {
            if (!enabled)
            {
                return;
            }

            removeStart = Now;
            removeRect = bead;
            removeDefName = defName;
        }

        /// <summary>Bookmarks are ordered top to bottom, so the band follows the slot direction.</summary>
        public void OnSlotSwitch(int fromSlot, int toSlot)
        {
            if (!enabled)
            {
                return;
            }

            addStart = Idle;
            addIndex = -1;
            removeStart = Idle;
            flipDirection = toSlot >= fromSlot ? 1 : -1;
            flipStart = Now;
        }

        public void Shake()
        {
            if (!enabled)
            {
                return;
            }

            shakeStart = Now;
        }

        public void Sweep()
        {
            if (!enabled)
            {
                return;
            }

            sweepStart = Now;
        }

        // ---------------------------------------------------------------- frame queries

        public bool TryAdd(out float progress, out Vector2 from, out int index)
        {
            progress = Progress(addStart, AddSeconds);
            from = addFrom;
            index = addIndex;
            return enabled && addIndex >= 0 && progress >= 0f;
        }

        public bool TryRemove(out float progress, out Rect bead, out string defName)
        {
            progress = Progress(removeStart, RemoveSeconds);
            bead = removeRect;
            defName = removeDefName;
            return enabled && progress >= 0f;
        }

        public bool TryFlip(out float progress, out int direction)
        {
            progress = Progress(flipStart, FlipSeconds);
            direction = flipDirection;
            return enabled && progress >= 0f;
        }

        public bool TryShake(out float progress)
        {
            progress = Progress(shakeStart, ShakeSeconds);
            return enabled && progress >= 0f;
        }

        public bool TrySweep(out float progress)
        {
            progress = Progress(sweepStart, SweepSeconds);
            return enabled && progress >= 0f;
        }

        // ---------------------------------------------------------------- easing

        private static float Progress(float start, float seconds)
        {
            if (seconds <= 0f)
            {
                return -1f;
            }

            float t = (Now - start) / seconds;
            return t >= 0f && t < 1f ? t : -1f;
        }

        /// <summary>Fast start, gentle landing.</summary>
        public static float EaseOut(float t)
        {
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        /// <summary>0 at both ends, 1 in the middle; keeps a band from popping in or out.</summary>
        public static float Pulse(float t)
        {
            return 1f - Mathf.Abs(t * 2f - 1f);
        }
    }
}
