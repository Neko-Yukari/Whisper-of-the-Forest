using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Vfx;

namespace WhisperOfTheForest.Spells
{
    /// <summary>How a placed spell field spends the payload it carries.</summary>
    public enum SpellFieldMode
    {
        /// <summary>A force field at the impact point: every interval, every pawn inside gets the payload.</summary>
        Linger,

        /// <summary>A seal on the ground: the first pawn stepping onto it gets the payload once.</summary>
        Rune
    }

    /// <summary>
    /// The temporary Thing behind Linger and Rune. It is the "later / elsewhere" half of the existing
    /// segment machinery: the cast freezes the payload steps here, exactly the way a dart carries its
    /// frozen plan, and the field runs that payload through the same executor when its moment comes.
    ///
    /// Saved with the map, so a field placed before a save keeps its radius, interval, lifetime and
    /// payload after loading.
    /// </summary>
    public class Thing_SpellField : Thing
    {
        private SpellFieldMode mode;
        private Thing caster;
        private List<SpellEffect> payload = new List<SpellEffect>();
        private float radius;
        private int intervalTicks;
        private int nextPulseTick;
        private int expiresTick;
        private VfxHandle vfx;

        internal IReadOnlyList<SpellEffect> Payload => payload;

        public void Configure(SpellFieldMode mode, Thing caster, IReadOnlyList<SpellEffect> steps,
            int lifetimeTicks, float radius, int intervalTicks)
        {
            this.mode = mode;
            this.caster = caster;
            payload = SpellExecutor.CopySteps(steps);
            this.radius = radius;
            this.intervalTicks = intervalTicks;
            expiresTick = Find.TickManager.TicksGame + lifetimeTicks;
            nextPulseTick = Find.TickManager.TicksGame + intervalTicks;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            vfx = VfxService.Play(new VfxRequest
            {
                map = map, follow = this, origin = DrawPos, width = radius * 2f,
                vfxDefName = mode == SpellFieldMode.Rune ? "WotF_Vfx_Rune" : "WotF_Vfx_Linger"
            });
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (now >= expiresTick)
            {
                Destroy();
                return;
            }

            if (mode == SpellFieldMode.Linger)
            {
                if (now >= nextPulseTick)
                {
                    nextPulseTick = now + (intervalTicks > 0 ? intervalTicks : 1);
                    Pulse();
                }

                return;
            }

            TriggerRune();
        }

        /// <summary>
        /// One Linger beat. Targets are collected first: the payload can kill pawns or spawn things,
        /// and iterating the live grid while it changes skips or repeats targets.
        /// </summary>
        private void Pulse()
        {
            var targets = new List<Pawn>();
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(Position, Map, radius, true))
            {
                if (thing is Pawn pawn && SpellTargets.CanTakeDamage(pawn))
                {
                    targets.Add(pawn);
                }
            }

            for (int i = 0; i < targets.Count; i++)
            {
                Pawn pawn = targets[i];
                if (pawn.Destroyed || !pawn.Spawned || pawn.Map != Map)
                {
                    continue;
                }

                string report = SpellExecutor.ExecuteStepList(payload, caster, pawn, pawn.Position, Map);
                SpellDiagnostics.Debug("linger at " + Position + " pulse on " + pawn.LabelShort + " " + report);
            }

            SpellDiagnostics.Debug("linger at " + Position + " pulsed on " + targets.Count + " pawn(s)");
        }

        private void TriggerRune()
        {
            List<Thing> things = Position.GetThingList(Map);
            for (int i = 0; i < things.Count; i++)
            {
                if (!(things[i] is Pawn pawn) || !SpellTargets.CanTakeDamage(pawn))
                {
                    continue;
                }

                string report = SpellExecutor.ExecuteStepList(payload, caster, pawn, pawn.Position, Map);
                SpellDiagnostics.Message("rune at " + Position + " triggered by " + pawn.LabelShort + " " + report);
                vfx.Trigger(VfxStates.Impact);
                Destroy();
                return;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref mode, "spellFieldMode", SpellFieldMode.Linger);
            Scribe_References.Look(ref caster, "caster");
            Scribe_Collections.Look(ref payload, "payload", LookMode.Deep);
            Scribe_Values.Look(ref radius, "radius", 0f);
            Scribe_Values.Look(ref intervalTicks, "intervalTicks", 0);
            Scribe_Values.Look(ref nextPulseTick, "nextPulseTick", 0);
            Scribe_Values.Look(ref expiresTick, "expiresTick", 0);

            SpellEffect.CheckLoaded(payload, "Thing_SpellField");

            if (payload == null)
            {
                payload = new List<SpellEffect>();
            }
        }
    }
}
