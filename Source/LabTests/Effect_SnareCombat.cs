using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    [LabTest]
    public sealed class Effect_SnareCombat : LabTest
    {
        public override string Name => "effect.snare-combat";

        protected override void Run(TestContext t)
        {
            Check(t, false);
            Check(t, true);
        }

        private static void Check(TestContext t, bool ai)
        {
            string mode = ai ? "ai" : "drafted";
            Pawn caster = t.SpawnCaster(8, 5);
            Pawn target = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,
                Faction.OfPlayer, canGeneratePawnRelations: false, mustBeCapableOfViolence: true,
                forceNoGear: true));
            GenSpawn.Spawn(target, t.Arena.Cell(10, 5), t.Map, WipeMode.Vanish);
            t.Arena.Track(target);
            target.drafter.Drafted = true;
            t.Note(mode + "ShootingDisabled", target.skills.GetSkill(SkillDefOf.Shooting).TotallyDisabled);
            Pawn enemy = ai ? caster : t.SpawnTarget(14, 7);
            Lord lord = null;
            if (ai)
            {
                target.drafter.Drafted = false;
                target.SetFaction(Faction.OfAncientsHostile);
                lord = LordMaker.MakeNewLord(target.Faction,
                    new LordJob_AssaultColony(target.Faction, canKidnap: false, canTimeoutOrFlee: false, canSteal: false),
                    t.Map, new[] { target });
            }
            else
            {
                enemy.drafter.Drafted = false;
                enemy.SetFaction(Faction.OfAncientsHostile);
                enemy.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture, ((SnareGlyphDef)TestContext.Glyph("WotF_Effect_Snare")).rootDurationTicks * 3),
                    JobCondition.InterruptForced);
            }
            target.skills.GetSkill(SkillDefOf.Shooting).Level = 20;
            target.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_Autopistol")));

            IntVec3 from = target.Position;
            // Keep the entire movement and firing scene on the arena's cleared concrete.
            t.WalkTo(target, t.Arena.Cell(40, 5), 1);
            t.Expect(t.StepUntil(() => target.Position != from, 120), mode + " did not start walking");
            t.Measure(mode + "BeforeHit", target.Position + " job=" + target.CurJobDef?.defName);
            t.Expect(target.CurJobDef == JobDefOf.Goto && target.pather.Moving, mode + " fixture was not walking Goto");

            int movingJobId = target.CurJob.loadID;
            var book = GameComponent_Spellbook.Current;
            t.Expect(book.SaveSlot(caster, 0, TestContext.Recipe("moving-snare", SpellDefs.FormProjectile, "WotF_Effect_Snare"), out string reason), reason);
            book.SetActiveSlot(caster, 0);
            Command_Target cast = t.SpellAbility(caster).GetGizmos().OfType<Command_Target>().Single();
            t.Expect(!cast.Disabled, "snare gizmo disabled: " + cast.disabledReason);
            t.Expect(cast.targetingParams.validator(new TargetInfo(target)), "snare target rejected by gizmo");
            cast.action(target);
            HediffDef snare = DefDatabase<HediffDef>.GetNamed(SpellEffectDefs.HediffEnsnare);
            int duration = ((SnareGlyphDef)TestContext.Glyph("WotF_Effect_Snare")).rootDurationTicks;
            bool hit = t.StepUntil(() => target.health.hediffSet.HasHediff(snare), duration * 2);
            t.Expect(hit, mode + " snare did not hit moving target");
            if (hit)
            {
                IntVec3 stopped = target.Position;
                string jobAtHit = target.CurJobDef?.defName ?? "none";
                t.Measure(mode + "AtHit", stopped + " job=" + jobAtHit + " downed=" + target.Downed);
                t.Measure(mode + "JobIdentity", movingJobId + " -> " + target.CurJob?.loadID
                    + " destination=" + target.CurJob?.targetA + " giver=" + target.CurJob?.jobGiver);
                Verb weapon = target.equipment.PrimaryEq.PrimaryVerb;
                bool canShoot = weapon.CanHitTarget(enemy);
                int hitTick = t.Tick;
                t.Note(mode + "CanShootAtHit", canShoot);
                t.Expect(canShoot, mode + " cannot shoot the nearby enemy after snare hit");
                t.Expect(target.CurJobDef != JobDefOf.Goto, mode + " retained Goto after snare hit");
                t.Expect(!target.Downed, mode + " was downed by snare");
                float healthBefore = enemy.health.summaryHealth.SummaryHealthPercent;
                int partsBefore = enemy.health.hediffSet.GetMissingPartsCommonAncestors().Count;
                int changes = 0;
                int ticks = 0;
                int previousJob = target.CurJob?.loadID ?? -1;
                int firstCombatJob = previousJob;
                int expiry = target.CurJob?.expiryInterval ?? 0;
                int expiryObservationEnd = t.Tick + expiry * 2;
                var givers = new Dictionary<string, int>();
                string firstGiver = target.CurJob?.jobGiver?.GetType().Name ?? "none";
                givers[firstGiver] = 1;
                var jobs = new StringBuilder(jobAtHit);
                bool stayed = true;
                while (target.health.hediffSet.HasHediff(snare) && ticks < duration * 2)
                {
                    t.Step(1);
                    ticks++;
                    stayed &= target.Position == stopped;
                    int currentJob = target.CurJob?.loadID ?? -1;
                    if (currentJob != previousJob)
                    {
                        changes++;
                        string giver = target.CurJob?.jobGiver?.GetType().Name ?? "none";
                        givers[giver] = givers.TryGetValue(giver, out int count) ? count + 1 : 1;
                        jobs.Append(" -> ").Append(target.CurJobDef?.defName).Append('@').Append(ticks);
                        previousJob = currentJob;
                    }
                }
                bool retaliated = enemy.health.summaryHealth.SummaryHealthPercent < healthBefore
                    || enemy.health.hediffSet.GetMissingPartsCommonAncestors().Count > partsBefore;
                t.Note(mode + "ObservedTicks", ticks);
                t.Note(mode + "JobChanges", changes);
                t.Measure(mode + "Jobs", jobs.ToString());
                int shotAfterHitTicks = weapon.LastShotTick - hitTick;
                bool firedWhileSnared = shotAfterHitTicks > 0 && shotAfterHitTicks < ticks;
                t.Note(mode + "ShotAfterHitTicks", shotAfterHitTicks);
                t.Measure(mode + "ProjectileImpact", "not captured");
                t.Note(mode + "Retaliated", retaliated);
                t.Note(mode + "FiredWhileSnared", firedWhileSnared);
                t.Note(mode + "HitTick", hitTick);
                t.Measure(mode + "CurrentVerbTarget", weapon.CurrentTarget.ToString());
                t.Note(mode + "LastShotTick", target.equipment.PrimaryEq.PrimaryVerb.LastShotTick);
                t.Note(mode + "ObservationEndTick", t.Tick);
                t.Measure(mode + "Enemy", enemy + " at " + enemy.Position);
                t.Measure(mode + "ShooterHediffs", TestContext.HediffDump(target));
                t.Measure(mode + "VictimHealth", healthBefore + " -> " + enemy.health.summaryHealth.SummaryHealthPercent);
                t.Measure(mode + "Position", stopped + " -> " + target.Position);
                t.Expect(stayed, mode + " moved during snare");
                // AI may choose another hostile or miss; LastShotTick proves a real projectile launch.
                // The drafted branch still checks actual damage on its designated victim.
                t.Expect(ai ? firedWhileSnared : retaliated, mode + " did not automatically retaliate while snared"
                    + ": hit=" + hitTick + " lastShot=" + weapon.LastShotTick + " delta=" + shotAfterHitTicks + " window=" + ticks);
                // One transition to combat and one renewal is enough in this sub-expiry window.
                t.Expect(changes <= 2, mode + " repeatedly switched jobs: " + jobs);
                t.Measure(mode + "JobGiversDuringSnare", string.Join(", ", givers.Select(pair => pair.Key + "=" + pair.Value)));
                if (ai)
                {
                    t.Note("aiFirstCombatExpiry", expiry);
                    // Vanilla checks expiry on the pawn's hash interval, so observe two intervals.
                    var after = new StringBuilder();
                    while (t.Tick < expiryObservationEnd && !target.Dead)
                    {
                        t.Step(1);
                        int id = target.CurJob?.loadID ?? -1;
                        if (id == previousJob) continue;
                        after.Append(target.CurJobDef?.defName).Append(" from ").Append(target.CurJob?.jobGiver)
                            .Append(" at=").Append(t.Tick).Append(" snared=")
                            .Append(target.health.hediffSet.HasHediff(snare)).Append(';');
                        previousJob = id;
                    }
                    t.Measure("aiPostSnareTransitions", after.ToString());
                    t.Measure("aiAfterCombatExpiry", "job=" + target.CurJobDef?.defName
                        + " id=" + firstCombatJob + " -> " + target.CurJob?.loadID
                        + " giver=" + target.CurJob?.jobGiver + " snared=" + target.health.hediffSet.HasHediff(snare));
                }
            }
            if (!target.Destroyed) target.Destroy(DestroyMode.Vanish);
            if (lord != null) t.Map.lordManager.RemoveLord(lord);
            if (enemy != caster && !enemy.Destroyed) enemy.Destroy(DestroyMode.Vanish);
            if (!caster.Destroyed) caster.Destroy(DestroyMode.Vanish);
        }
    }
}
