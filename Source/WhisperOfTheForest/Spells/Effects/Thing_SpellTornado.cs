using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Vfx;

namespace WhisperOfTheForest.Spells
{
    public sealed class Thing_SpellTornado : Thing
    {
        private Pawn caster;
        private SpellTornadoSpec spec;
        private int expiresTick;
        private int nextPulseTick;

        public void Configure(Pawn caster, SpellTornadoSpec spec)
        {
            this.caster = caster;
            this.spec = spec.Copy();
            expiresTick = Find.TickManager.TicksGame + spec.durationTicks;
            nextPulseTick = Find.TickManager.TicksGame + spec.pulseIntervalTicks;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            VfxService.Play(new VfxRequest
            {
                map = map, follow = this, origin = DrawPos, width = spec.radius * 2f,
                vfxDefName = "WotF_Vfx_Tornado"
            });
        }

        protected override void Tick()
        {
            base.Tick();
            int now = Find.TickManager.TicksGame;
            if (now >= nextPulseTick && nextPulseTick <= expiresTick)
            {
                Pulse(now);
                nextPulseTick += spec.pulseIntervalTicks;
            }
            if (now >= expiresTick + spec.fadeTicks) Destroy();
        }

        private void Pulse(int now)
        {
            var targets = new List<Thing>();
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(Position, Map, spec.radius, true))
                if (SpellTargets.CanTakeDamage(thing)) targets.Add(thing);

            foreach (Thing thing in targets)
            {
                if (!SpellTargets.CanTakeDamage(thing)) continue;
                // Bullet-derived force otherwise duplicates an internal hit onto outer body parts.
                SpellDamage.Deal(thing, spec.damagePerPulse, spec.armorPenetration, caster, spec.logSource,
                    allowDamagePropagation: false);
                if (thing is Pawn pawn && SpellTargets.CanTakeDamage(pawn)) RefreshScorch(pawn, now);
            }

            if (Rand.Chance(spec.igniteChancePerPulse))
            {
                IntVec3 cell = GenRadial.RadialCellsAround(Position, spec.radius, true)
                    .Where(c => c.InBounds(Map)).RandomElement();
                FireUtility.TryStartFireIn(cell, Map, Fire.MinFireSize, caster);
            }
            SpellDiagnostics.Debug("tornado at " + Position + " pulse tick=" + now + " targets=" + targets.Count);
        }

        private void RefreshScorch(Pawn pawn, int now)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamed("WotF_Hediff_Scorch");
            var scorch = (Hediff_SpellDot)pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (scorch == null)
            {
                scorch = (Hediff_SpellDot)HediffMaker.MakeHediff(def, pawn);
                scorch.damageDef = DamageDefOf.Burn;
                scorch.damagePerPulse = spec.damagePerPulse;
                scorch.armorPenetration = spec.armorPenetration;
                scorch.pulseIntervalTicks = spec.pulseIntervalTicks;
                scorch.instigator = caster;
                scorch.TryGetComp<HediffComp_Disappears>().SetDuration(spec.burnRefreshTicks);
                pawn.health.AddHediff(scorch);
            }
            else scorch.TryGetComp<HediffComp_Disappears>().ResetElapsedTicks();
            scorch.suppressedUntilTick = now + spec.pulseIntervalTicks;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref caster, "caster");
            Scribe_Deep.Look(ref spec, "spec");
            Scribe_Values.Look(ref expiresTick, "expiresTick");
            Scribe_Values.Look(ref nextPulseTick, "nextPulseTick");
        }
    }
}
