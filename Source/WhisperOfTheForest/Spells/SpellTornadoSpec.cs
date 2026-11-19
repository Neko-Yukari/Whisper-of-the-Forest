using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Spells
{
    public sealed class SpellTornadoSpec : IExposable, ISpellSpecialSpec
    {
        public float radius;
        public int durationTicks;
        public int pulseIntervalTicks;
        public float damagePerPulse;
        public float armorPenetration;
        public float igniteChancePerPulse;
        public int burnRefreshTicks;
        public int fadeTicks = 30;
        public string vfxDef;
        public float range;
        public SpellDamageSource logSource;

        public float Range => range;
        public void BeginChannel(SpellCast cast) { }
        public string Release(SpellCast cast) => TornadoCastService.Resolve(cast.map, cast.caster, cast.cell, this);

        public SpellTornadoSpec Copy() => new SpellTornadoSpec
        {
            radius = radius, durationTicks = durationTicks, pulseIntervalTicks = pulseIntervalTicks,
            damagePerPulse = damagePerPulse, armorPenetration = armorPenetration,
            igniteChancePerPulse = igniteChancePerPulse, burnRefreshTicks = burnRefreshTicks,
            fadeTicks = fadeTicks, vfxDef = vfxDef, range = range, logSource = logSource?.Copy()
        };

        public void ExposeData()
        {
            Scribe_Values.Look(ref radius, "radius");
            Scribe_Values.Look(ref durationTicks, "durationTicks");
            Scribe_Values.Look(ref pulseIntervalTicks, "pulseIntervalTicks");
            Scribe_Values.Look(ref damagePerPulse, "damagePerPulse");
            Scribe_Values.Look(ref armorPenetration, "armorPenetration");
            Scribe_Values.Look(ref igniteChancePerPulse, "igniteChancePerPulse");
            Scribe_Values.Look(ref burnRefreshTicks, "burnRefreshTicks");
            Scribe_Values.Look(ref fadeTicks, "fadeTicks", 30);
            Scribe_Values.Look(ref vfxDef, "vfxDef");
            Scribe_Values.Look(ref range, "range");
            Scribe_Deep.Look(ref logSource, "logSource");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && logSource == null)
            {
                logSource = SpellDamageSource.FromGlyph(SpellDefs.Glyph(SpellDefs.SuperTornado));
            }
        }

        public IEnumerable<string> ConfigErrors()
        {
            if (radius <= 0f || radius >= GenRadial.MaxRadialPatternRadius)
                yield return "tornado radius must fit the radial grid";
            if (pulseIntervalTicks <= 0 || durationTicks < pulseIntervalTicks || burnRefreshTicks <= 0 || fadeTicks < 0)
                yield return "tornado needs positive duration/interval/burn refresh and nonnegative fade";
            if (damagePerPulse <= 0f || armorPenetration < 0f)
                yield return "tornado needs positive damage and nonnegative armor penetration";
            if (igniteChancePerPulse < 0f || igniteChancePerPulse > 1f)
                yield return "tornado ignition chance must be between zero and one";
        }
    }
}
