using System.Collections.Generic;
using UnityEngine;

namespace WhisperOfTheForest.Vfx
{
    public sealed class SpellPrefabVfxDef : PrefabVfxDef
    {
        // Unity's BuildSpells reads these values; changing them requires export and a game restart.
        public List<SpellParticleLayer> particleLayers;
        public List<string> states;
        public int strikeDelayTicks;
        public int strikeDurationTicks;
        public Verse.SoundDef strikeSound;

        public int MaxParticleLifetimeTicks
        {
            get
            {
                int maximum = 0;
                foreach (SpellParticleLayer layer in particleLayers)
                    maximum = System.Math.Max(maximum, layer.lifetimeTicks);
                return maximum;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (particleLayers == null || particleLayers.Count == 0 || particleLayers.Count > 3)
            {
                yield return "a spell prefab needs one to three particle layers";
                yield break;
            }
            if (strikeDelayTicks < 0 || strikeDurationTicks < 0)
                yield return "strike timing must not be negative";
            if (strikeDurationTicks > 0 && strikeSound == null)
                yield return "strikeSound must resolve for a timed strike";
            var declaredStates = new HashSet<string>();
            if (states != null)
                foreach (string state in states)
                    if (!VfxStates.Contains(state) || !declaredStates.Add(state))
                        yield return defName + ": invalid or duplicate state '" + state + "'";
            var names = new HashSet<string>();
            foreach (SpellParticleLayer layer in particleLayers)
            {
                if (string.IsNullOrEmpty(layer.name) || !names.Add(layer.name))
                    yield return "particle layer names must be present and unique";
                if (string.IsNullOrEmpty(layer.color)) yield return "particle layer needs a color";
                if (layer.lifetimeTicks <= 0 || layer.size.x <= 0f || layer.size.y <= 0f || layer.size.z <= 0f)
                    yield return "particle lifetime and size must be positive";
                if (layer.ejectionChance < 0f || layer.ejectionChance > 1f || layer.ejectionSpeed < 0f)
                    yield return "invalid particle ejection parameters";
                if (layer.rate < 0f || layer.burstCount < 0 || layer.radius < 0f)
                    yield return "particle rate, burst count and radius must not be negative";
            }
        }
    }

    public sealed class SpellParticleLayer
    {
        public string name;
        public string color;
        public Vector3 size;
        public int lifetimeTicks;
        public float rate;
        public int burstCount;
        public float speed;
        public float radius;
        public float rotationSpeed;
        public float ejectionChance;
        public float ejectionSpeed;
    }
}
