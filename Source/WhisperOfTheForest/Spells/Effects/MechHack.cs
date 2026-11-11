using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Spells
{
    // The post-hit channel is a dedicated Job, not the pre-release SpellCast phase.
    // Biotech is a hard dependency, so EMPResistance always exists.

    public static class MechHackService
    {
        internal static HediffDef AssimilationDef => DefDatabase<HediffDef>.GetNamed("WotF_Hediff_MechAssimilation");
        internal static HediffDef ControlledDef => DefDatabase<HediffDef>.GetNamed("WotF_Hediff_MechControlled");
        internal static JobDef ChannelDef => DefDatabase<JobDef>.GetNamed("WotF_ChannelMechHack");

        // Requires: target spawned.
        // Ensures: accepted only for a mechanoid hostile to the caster that is not already assimilating or controlled.
        public static AcceptanceReport CanHack(Pawn caster, Pawn target)
        {
            if (target.Dead || !target.RaceProps.IsMechanoid || !target.HostileTo(caster))
                return "WotF_Hack_HostileMechOnly".Translate();
            if (target.health.hediffSet.HasHediff(AssimilationDef) || IsTemporarilyControlled(target))
                return "WotF_Hack_AlreadyAffected".Translate();
            return true;
        }

        // Requires: strength > 0.
        // Ensures: baseTicks × (target.kindDef.combatPower ÷ strength) × (1 + target EMPResistance),
        //          clamped to [minTicks, maxTicks].
        public static int AssimilationTicks(Pawn target, float strength, float baseTicks, int minTicks, int maxTicks)
        {
            float ticks = baseTicks * (target.kindDef.combatPower / strength)
                * (1f + target.GetStatValue(DefDatabase<StatDef>.GetNamed("EMPResistance")));
            return (int)Math.Ceiling(Math.Max(minTicks, Math.Min(maxTicks, ticks)));
        }

        // Ensures: when the caster is spawned on map and able to act, starts one channel Job on the caster
        //          (interrupting its current job, which also ends any earlier hack channel) and begins
        //          assimilation, tied to that Job's loadID, on every target CanHack accepts. Returns the
        //          number started; 0 and no Job when the caster cannot channel or no target is accepted.
        //          Every number comes from the compiled HackEffect, never from a Def lookup.
        public static int Apply(HackEffect effect, Pawn caster, Thing primary, IntVec3 cell, Map map)
        {
            if (caster == null || !caster.Spawned || caster.Map != map || caster.Dead
                || caster.Downed || caster.stances.stunner.Stunned) return 0;

            var targets = new List<Pawn>();
            if (effect.UseArea)
            {
                foreach (Pawn target in map.mapPawns.AllPawnsSpawned)
                    if (target.Position.InHorDistOf(cell, effect.TargetRadius) && CanHack(caster, target))
                        targets.Add(target);
            }
            else
            {
                Pawn target = primary as Pawn;
                if (primary == null && cell.InBounds(map))
                    target = cell.GetThingList(map).OfType<Pawn>().FirstOrDefault(p => CanHack(caster, p));
                if (target != null && target.Spawned && CanHack(caster, target)) targets.Add(target);
            }
            if (targets.Count == 0) return 0;

            Job channel = JobMaker.MakeJob(ChannelDef);
            channel.playerForced = true;
            caster.jobs.StartJob(channel, JobCondition.InterruptForced);
            foreach (Pawn target in targets)
            {
                Hediff assimilation = HediffMaker.MakeHediff(AssimilationDef, target);
                assimilation.TryGetComp<HediffComp_MechAssimilation>().Begin(caster, channel.loadID,
                    AssimilationTicks(target, effect.HackStrength, effect.BaseTicks, effect.MinTicks, effect.MaxTicks),
                    effect.ControlTicks);
                target.health.AddHediff(assimilation);
            }
            SpellDiagnostics.Message("hack channel " + channel.loadID + " started targets=" + targets.Count);
            return targets.Count;
        }

        // Ensures: true while the caster's current Job is the hack channel Job with this loadID.
        //          The id is what keeps an assimilation from reviving under a later, unrelated channel.
        public static bool IsChanneling(Pawn caster, int channelJobId)
        {
            return caster?.CurJob?.def == ChannelDef && caster.CurJob.loadID == channelJobId;
        }

        // Ensures: true for a pawn carrying the MechControlled hediff. The only predicate the vanilla
        //          overseer/draft/command-range patches may use, so they never touch ordinary mechs.
        public static bool IsTemporarilyControlled(Pawn pawn)
        {
            return pawn.health.hediffSet.HasHediff(ControlledDef);
        }

        // Requires: assimilation finished on a still-spawned target.
        // Ensures: target joins the caster's faction for controlTicks and carries HediffComp_MechControlled.
        internal static void TakeControl(Pawn target, Pawn caster, int controlTicks)
        {
            Hediff control = HediffMaker.MakeHediff(ControlledDef, target);
            control.TryGetComp<HediffComp_MechControlled>().Begin(target.Faction, controlTicks);
            target.health.AddHediff(control);
            target.SetFaction(caster.Faction);
            SpellDiagnostics.Message("hack controlled " + target.LabelShort + " for " + controlTicks + " ticks");
        }
    }

    public class HediffCompProperties_MechAssimilation : HediffCompProperties
    {
        public HediffCompProperties_MechAssimilation()
        {
            compClass = typeof(HediffComp_MechAssimilation);
        }
    }

    /// <summary>The read-out on the target: stunned while the caster channels; removed on failure.</summary>
    public class HediffComp_MechAssimilation : HediffComp
    {
        private Pawn caster;
        private int channelJobId;
        private int ticksLeft;
        private int controlTicks;

        public Pawn Caster => caster;
        public int ChannelJobId => channelJobId;
        public bool Finished => ticksLeft <= 0;

        // Requires: ticks > 0; channelJobId is the loadID of the caster's hack channel Job.
        // Ensures: the countdown starts from ticks and remembers who channels it and under which Job.
        public void Begin(Pawn channeler, int channelJobId, int ticks, int controlTicks)
        {
            caster = channeler;
            this.channelJobId = channelJobId;
            this.controlTicks = controlTicks;
            ticksLeft = ticks;
            Pawn.stances.stunner.StunFor(2, caster, addBattleLog: false, showMote: false);
        }

        // Ensures: keeps the target stunned; removes the hediff (assimilation fails, faction unchanged) as
        //          soon as MechHackService.IsChanneling(caster, channelJobId) is false;
        //          calls MechHackService.TakeControl and removes itself when the countdown reaches 0.
        public override void CompPostTick(ref float severityAdjustment)
        {
            if (!MechHackService.IsChanneling(caster, channelJobId) || caster.Dead || caster.Downed
                || !caster.Spawned || caster.Map != Pawn.Map || caster.stances.stunner.Stunned || Pawn.Dead)
            {
                Pawn.health.RemoveHediff(parent);
                return;
            }
            if (--ticksLeft <= 0)
            {
                MechHackService.TakeControl(Pawn, caster, controlTicks);
                Pawn.health.RemoveHediff(parent);
                return;
            }
            // Renew only a two-tick stun; interruption cannot leave the full countdown stunned,
            // and removing this effect never erases another source's longer stun.
            Pawn.stances.stunner.StunFor(2, caster, addBattleLog: false, showMote: false);
        }

        public override string CompTipStringExtra => string.Format("WotF_Hack_Remaining".Translate(),
            ticksLeft.ToStringTicksToPeriod());

        // Ensures: caster reference, channelJobId and ticksLeft are saved.
        public override void CompExposeData()
        {
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref channelJobId, "channelJobId");
            Scribe_Values.Look(ref ticksLeft, "ticksLeft");
            Scribe_Values.Look(ref controlTicks, "controlTicks");
        }
    }

    /// <summary>
    /// The caster's side of the channel: stands in place while its targets assimilate. Each target
    /// finishes on its own countdown; the Job ends when none is left. Ending the Job any other way
    /// (order, stun, down, death) fails every unfinished assimilation; finished controls stay.
    /// </summary>
    public class JobDriver_ChannelMechHack : JobDriver
    {
        // Ensures: no reservation; targets stay attackable by others.
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        // Ensures: one toil that keeps the caster in place, faces the nearest assimilating target, and
        //          ends Succeeded once no spawned pawn on the map carries an assimilation tied to
        //          (pawn, job.loadID). Vanilla saves the Job, so the channel survives a reload.
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Dead || pawn.Downed || pawn.stances.stunner.Stunned);
            AddFinishAction(condition =>
            {
                if (condition == JobCondition.Succeeded || !pawn.Spawned) return;
                foreach (Pawn target in pawn.Map.mapPawns.AllPawnsSpawned.ToList())
                {
                    Hediff assimilation = target.health.hediffSet.GetFirstHediffOfDef(MechHackService.AssimilationDef);
                    var comp = assimilation?.TryGetComp<HediffComp_MechAssimilation>();
                    if (comp?.Caster == pawn && comp.ChannelJobId == job.loadID)
                        target.health.RemoveHediff(assimilation);
                }
            });
            Toil channel = ToilMaker.MakeToil();
            channel.initAction = () => pawn.pather.StopDead();
            channel.defaultCompleteMode = ToilCompleteMode.Never;
            channel.tickAction = () =>
            {
                Pawn nearest = null;
                float distance = float.MaxValue;
                foreach (Pawn target in pawn.Map.mapPawns.AllPawnsSpawned)
                {
                    var comp = target.health.hediffSet.GetFirstHediffOfDef(MechHackService.AssimilationDef)
                        ?.TryGetComp<HediffComp_MechAssimilation>();
                    if (comp?.Caster != pawn || comp.ChannelJobId != job.loadID) continue;
                    float next = (target.Position - pawn.Position).LengthHorizontalSquared;
                    if (next < distance) { nearest = target; distance = next; }
                }
                if (nearest == null) EndJobWith(JobCondition.Succeeded);
                else pawn.rotationTracker.FaceTarget(nearest);
            };
            yield return channel;
        }
    }

    public class HediffCompProperties_MechControlled : HediffCompProperties
    {
        public HediffCompProperties_MechControlled()
        {
            compClass = typeof(HediffComp_MechControlled);
        }
    }

    /// <summary>Temporary control. Does not use mechanitor bandwidth.</summary>
    public class HediffComp_MechControlled : HediffComp
    {
        private Faction originalFaction;
        private int ticksLeft;

        // Ensures: remembers the faction to return to and the control duration.
        public void Begin(Faction original, int ticks)
        {
            originalFaction = original;
            ticksLeft = ticks;
        }

        // Ensures: at 0 the hediff removes itself.
        public override void CompPostTick(ref float severityAdjustment)
        {
            if (--ticksLeft <= 0) Pawn.health.RemoveHediff(parent);
        }

        // Ensures: on removal (expiry or otherwise) the pawn returns to originalFaction, undrafted.
        public override void CompPostPostRemoved()
        {
            if (Pawn.drafter != null) Pawn.drafter.Drafted = false;
            if (Pawn.Faction != originalFaction) Pawn.SetFaction(originalFaction);
            SpellDiagnostics.Message("hack released " + Pawn.LabelShort);
        }

        public override string CompTipStringExtra => string.Format("WotF_Hack_Remaining".Translate(),
            ticksLeft.ToStringTicksToPeriod());

        // Ensures: original faction reference and ticksLeft are saved.
        public override void CompExposeData()
        {
            Scribe_References.Look(ref originalFaction, "originalFaction");
            Scribe_Values.Look(ref ticksLeft, "ticksLeft");
        }
    }
}
