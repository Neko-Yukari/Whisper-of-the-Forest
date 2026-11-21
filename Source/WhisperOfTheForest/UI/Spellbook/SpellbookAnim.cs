using UnityEngine;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Subtle IMGUI feedback for the spellbook: an arced glyph fly-in with landing bloom, a soft
    /// remove fade, a cascading mana ripple along the vine on save, spine crease shadow on slot
    /// switch, and a brief 2px nudge on refused actions.
    ///
    /// Driven by <see cref="Time.realtimeSinceStartup"/> so paused games still animate smoothly.
    /// No layout rectangle depends on it; finished animations simply return false.
    /// </summary>
    internal sealed class SpellbookAnim
    {
        public const float AddFlightSeconds = 0.16f;
        public const float AddBloomSeconds = 0.22f;
        public const float RemoveSeconds = 0.14f;
        public const float PageSwitchSeconds = 0.18f;
        public const float NudgeSeconds = 0.14f;
        public const float RippleStepSeconds = 0.035f;
        public const float RippleBeadSeconds = 0.24f;

        private const float Idle = -1000f;

        private float addStart = Idle;
        private Vector2 addFrom;
        private int addIndex = -1;

        private float removeStart = Idle;
        private Rect removeSocket;
        private string removeDefName;

        private float switchStart = Idle;
        private float nudgeStart = Idle;

        private float rippleStart = Idle;
        private int rippleCount;

        private static float Now => Time.realtimeSinceStartup;

        // ---------------------------------------------------------------- triggers

        public void OnAdd(Vector2 from, int index)
        {
            addStart = Now;
            addFrom = from;
            addIndex = index;
        }

        public void OnRemove(Rect socketRect, string defName)
        {
            removeStart = Now;
            removeSocket = socketRect;
            removeDefName = defName;
        }

        public void OnSlotSwitch(int fromSlot, int toSlot)
        {
            addStart = Idle;
            addIndex = -1;
            removeStart = Idle;
            rippleStart = Idle;
            rippleCount = 0;

            if (fromSlot == toSlot)
            {
                return;
            }

            switchStart = Now;
        }

        public void Nudge()
        {
            nudgeStart = Now;
        }

        public void OnCreate(int glyphCount)
        {
            if (glyphCount <= 0)
            {
                return;
            }

            rippleCount = glyphCount;
            rippleStart = Now;
        }

        // ---------------------------------------------------------------- frame queries

        public bool IsAddingIndex(int index)
        {
            return index == addIndex && addIndex >= 0 && Progress(addStart, AddFlightSeconds) >= 0f;
        }

        public bool TryAddFlight(out float progress, out Vector2 from, out int index)
        {
            progress = Progress(addStart, AddFlightSeconds);
            from = addFrom;
            index = addIndex;
            return addIndex >= 0 && progress >= 0f;
        }

        public bool TryAddLanding(out float progress, out int index)
        {
            progress = Progress(addStart + AddFlightSeconds, AddBloomSeconds);
            index = addIndex;
            return addIndex >= 0 && progress >= 0f;
        }

        public bool TryRemove(out float progress, out Rect socketRect, out string defName)
        {
            progress = Progress(removeStart, RemoveSeconds);
            socketRect = removeSocket;
            defName = removeDefName;
            return progress >= 0f;
        }

        public bool TryPageCrease(out float progress)
        {
            progress = Progress(switchStart, PageSwitchSeconds);
            return progress >= 0f;
        }

        public bool TryNudge(out float offsetX)
        {
            float t = Progress(nudgeStart, NudgeSeconds);
            if (t < 0f)
            {
                offsetX = 0f;
                return false;
            }

            offsetX = Mathf.Sin(t * Mathf.PI * 3f) * 2.5f * (1f - t);
            return true;
        }

        public bool TryBeadRipple(int index, out float progress)
        {
            if (index < 0 || index >= rippleCount)
            {
                progress = -1f;
                return false;
            }

            progress = Progress(rippleStart + index * RippleStepSeconds, RippleBeadSeconds);
            return progress >= 0f;
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

        public static float EaseOut(float t)
        {
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        public static float Pulse(float t)
        {
            return Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
        }
    }
}
