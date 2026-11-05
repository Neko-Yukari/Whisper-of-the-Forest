using RimWorld;
using Verse;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// A wound that keeps damaging its pawn for a while: the sustained form of Harm and the
    /// non-spreading burn behind the Sensitive variant of Ignite. The Def picks the damage type and
    /// look; the spell writes the per-pulse amount, the interval and the instigator into the instance
    /// (and they are scribed, so a save mid-burn keeps ticking the same way).
    /// </summary>
    public class Hediff_SpellDot : HediffWithComps
    {
        public DamageDef damageDef;
        public float damagePerPulse;
        public float armorPenetration;
        public int pulseIntervalTicks;
        public int nextPulseTick;
        public Pawn instigator;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            if (nextPulseTick <= 0)
            {
                nextPulseTick = Find.TickManager.TicksGame + pulseIntervalTicks;
            }
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (pawn == null || pawn.Dead || damageDef == null || damagePerPulse <= 0f)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (now < nextPulseTick)
            {
                return;
            }

            nextPulseTick = now + pulseIntervalTicks;
            pawn.TakeDamage(new DamageInfo(
                damageDef,
                damagePerPulse,
                armorPenetration: armorPenetration,
                angle: -1f,
                instigator: instigator,
                intendedTarget: pawn,
                instigatorGuilty: true,
                spawnFilth: false));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref damageDef, "damageDef");
            Scribe_Values.Look(ref damagePerPulse, "damagePerPulse", 0f);
            Scribe_Values.Look(ref armorPenetration, "armorPenetration", 0f);
            Scribe_Values.Look(ref pulseIntervalTicks, "pulseIntervalTicks", 0);
            Scribe_Values.Look(ref nextPulseTick, "nextPulseTick", 0);
            Scribe_References.Look(ref instigator, "instigator");
        }
    }
}
