using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// Runs a compiled chain at one point of the map. This is the only executor: the Self/Touch/Orbit
    /// forms call it directly, a spell dart's payload calls it where the dart lands, and a Linger,
    /// Rune or Delay runs its frozen payload through exactly this code later.
    ///
    /// Every direct hit goes through <see cref="SpellDamage"/>, so nothing duplicates a damage
    /// implementation. Heal and Grow no longer mature/repair directly: they are the spell table's
    /// 凝血 (tend + faster healing) and 催生 (fertility boost through Plant.GrowthRate).
    /// </summary>
    public static class SpellExecutor
    {
        /// <summary>
        /// Applies every step of the plan and returns a compact report of what really happened
        /// (affected target counts per effect) for the log and for the acceptance run.
        /// </summary>
        public static string Execute(SpellCastPlan plan, Pawn caster, Thing primary, IntVec3 cell, Map map)
        {
            return ExecuteStepList(plan.steps, caster, primary, cell, map);
        }

        /// <summary>
        /// Applies a list of already frozen steps. A Linger beat, a Rune trigger, a delayed payload and
        /// an orbit touch all arrive here, so a delayed Harm behaves exactly like an immediate one.
        /// </summary>
        public static string ExecuteStepList(
            List<SpellPlanStep> steps,
            Pawn caster,
            Thing primary,
            IntVec3 cell,
            Map map)
        {
            if (steps == null || map == null)
            {
                return "no-plan";
            }

            var report = new StringBuilder();
            for (int i = 0; i < steps.Count; i++)
            {
                SpellPlanStep step = steps[i];
                if (step == null)
                {
                    continue;
                }

                if (report.Length > 0)
                {
                    report.Append(' ');
                }

                report.Append(step.effect.ToString().ToLowerInvariant());
                report.Append('=');
                report.Append(Apply(step, caster, primary, cell, map));
            }

            return report.Length == 0 ? "no-effects" : report.ToString();
        }

        /// <summary>Independent copies, so a spawned field/orb never shares a step with the book.</summary>
        public static List<SpellPlanStep> CopySteps(List<SpellPlanStep> steps)
        {
            var copy = new List<SpellPlanStep>(steps?.Count ?? 0);
            if (steps != null)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    copy.Add(steps[i]?.Copy());
                }
            }

            return copy;
        }

        private static int Apply(SpellPlanStep step, Pawn caster, Thing primary, IntVec3 cell, Map map)
        {
            switch (step.effect)
            {
                case SpellEffectKind.Harm:
                    return ApplyHarm(step, caster, primary, cell, map);
                case SpellEffectKind.Heal:
                    return ApplyClot(step, primary, cell, map);
                case SpellEffectKind.Explosion:
                    return ApplyExplosion(step, caster, primary, cell, map);
                case SpellEffectKind.Grow:
                    return ApplyGrow(step, cell, map);
                case SpellEffectKind.Ignite:
                    return ApplyIgnite(step, caster, primary, cell, map);
                case SpellEffectKind.Lightning:
                    return ApplyLightning(step, caster, primary, cell, map);
                case SpellEffectKind.Freeze:
                    return ApplyTimedState(step, primary, cell, map, SpellEffectDefs.HediffFreeze);
                case SpellEffectKind.Snare:
                    return ApplyTimedState(step, primary, cell, map, SpellEffectDefs.HediffEnsnare);
                case SpellEffectKind.Hack:
                    return MechHackService.Apply(step, caster, primary, cell, map);
                case SpellEffectKind.Hex:
                    return ApplyTimedState(step, primary, cell, map, SpellEffectDefs.HediffHex);
                case SpellEffectKind.ColdSnap:
                    return ApplyColdSnap(step, caster, primary, cell, map);
                case SpellEffectKind.Knockback:
                    return ApplyMove(step, primary, cell, map, false);
                case SpellEffectKind.Pull:
                    return ApplyMove(step, primary, cell, map, true);
                case SpellEffectKind.Magelight:
                    return SpellEffectRuntime.SpawnTemporaryLight(map, cell, step) ? 1 : 0;
                case SpellEffectKind.Blink:
                    return ApplyBlink(step, caster, primary, cell, map);
                case SpellEffectKind.Linger:
                    return SpawnField(step, caster, cell, map, SpellFieldMode.Linger);
                case SpellEffectKind.Rune:
                    return SpawnField(step, caster, cell, map, SpellFieldMode.Rune);
                case SpellEffectKind.Delay:
                    return ScheduleDelay(step, caster, cell, map);
                default:
                    return 0;
            }
        }

        private static int ApplyHarm(SpellPlanStep step, Pawn caster, Thing primary, IntVec3 cell, Map map)
        {
            int affected = 0;

            foreach (Thing target in CollectTargets(step, primary, cell, map, SpellTargets.CanTakeDamage))
            {
                if (step.dot && step.durationTicks > 0 && target is Pawn pawn)
                {
                    ApplySustainedHarm(pawn, step, caster);
                    affected++;
                    continue;
                }

                SpellDamage.Deal(target, step.amount, step.penetration, caster);
                affected++;
            }

            return affected;
        }

        /// <summary>Harm + Extend: the same total damage, paid out over the frozen duration.</summary>
        private static void ApplySustainedHarm(Pawn pawn, SpellPlanStep step, Pawn caster)
        {
            int interval = step.pulseIntervalTicks > 0 ? step.pulseIntervalTicks : step.durationTicks;
            int pulses = Math.Max(1, step.durationTicks / Math.Max(1, interval));
            float perPulse = Math.Max(0.01f, step.amount / pulses);

            Hediff dot = SpellEffectRuntime.MakeTimedHediff(pawn, SpellEffectDefs.HediffArcaneDot, step.durationTicks);
            if (dot == null)
            {
                return;
            }

            if (dot is Hediff_SpellDot sustained)
            {
                sustained.damageDef = SpellDamage.ArcaneForce;
                sustained.damagePerPulse = perPulse;
                sustained.armorPenetration = step.penetration;
                sustained.pulseIntervalTicks = interval;
                sustained.instigator = caster;
            }

            pawn.health.AddHediff(dot);
            SpellDiagnostics.Debug("sustained harm on " + pawn.LabelShort + " " + perPulse.ToString("0.##")
                                   + " every " + interval + " ticks for " + step.durationTicks + " ticks");
        }

        /// <summary>
        /// 凝血: tend every wound on the spot with no medicine, then leave a healing boost behind. It
        /// never restores lost health directly, so a fresh wound still has to heal over time.
        /// </summary>
        private static int ApplyClot(SpellPlanStep step, Thing primary, IntVec3 cell, Map map)
        {
            float quality = Mathf.Clamp01(step.factor * step.magnitudeFactor);
            int affected = 0;

            foreach (Thing target in CollectTargets(step, primary, cell, map, SpellTargets.CanBeHealed))
            {
                if (!(target is Pawn pawn) || pawn.health?.hediffSet == null)
                {
                    continue;
                }

                List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
                for (int i = 0; i < hediffs.Count; i++)
                {
                    if (hediffs[i] is Hediff_Injury injury && !HediffUtility.IsPermanent(injury))
                    {
                        injury.Tended(quality, 1f);
                    }
                }

                Hediff clotting = SpellEffectRuntime.MakeTimedHediff(pawn, SpellEffectDefs.HediffClotting, step.durationTicks);
                if (clotting != null)
                {
                    pawn.health.AddHediff(clotting);
                }

                affected++;
            }

            return affected;
        }

        private static int ApplyExplosion(SpellPlanStep step, Pawn caster, Thing primary, IntVec3 cell, Map map)
        {
            if (!GenGrid.InBounds(cell, map))
            {
                return 0;
            }

            // Sensitive keeps the blast from damaging buildings: the vanilla explosion skips any thing
            // it is told to ignore, so the whole structure list goes in instead of a second explosion
            // implementation.
            List<Thing> ignored = step.sensitive ? BuildingsAround(cell, map, step.radius) : null;
            DamageDef damageDef = SpellDamage.ArcaneForce;
            if (damageDef == null)
            {
                SpellDiagnostics.ErrorOnce("explosion has no damage def; the blast was skipped.");
                return 0;
            }

            GenExplosion.DoExplosion(
                cell,
                map,
                step.radius,
                damageDef,
                caster,
                damAmount: Mathf.Max(1, Mathf.RoundToInt(step.amount)),
                armorPenetration: step.penetration,
                doVisualEffects: false,
                damageFalloff: true,
                ignoredThings: ignored);

            return 1;
        }

        private static List<Thing> BuildingsAround(IntVec3 cell, Map map, float radius)
        {
            var buildings = new List<Thing>();
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(cell, map, radius, true))
            {
                if (thing is Building)
                {
                    buildings.Add(thing);
                }
            }

            return buildings;
        }

        /// <summary>
        /// 催生: the hit cells get a growth-rate multiplier for a while. Nothing is matured and no
        /// growth field is written; the Harmony postfix on Plant.GrowthRate reads this registry.
        /// </summary>
        private static int ApplyGrow(SpellPlanStep step, IntVec3 cell, Map map)
        {
            if (!GenGrid.InBounds(cell, map) || step.durationTicks <= 0)
            {
                return 0;
            }

            float factor = step.factor > 0f ? step.factor * Mathf.Max(1f, step.magnitudeFactor) : 0f;
            if (factor <= 1f)
            {
                return 0;
            }

            if (!step.aoe)
            {
                MapComponent_ForestFertility.Register(map, cell, step.durationTicks, factor);
                return 1;
            }

            int affected = 0;
            foreach (IntVec3 target in GenRadial.RadialCellsAround(cell, step.radius, true))
            {
                if (GenGrid.InBounds(target, map))
                {
                    MapComponent_ForestFertility.Register(map, target, step.durationTicks, factor);
                    affected++;
                }
            }

            return affected;
        }

        /// <summary>
        /// 引燃: the plain version starts real fire (it can spread, as the spell table warns). The
        /// Sensitive version puts a non-spreading burn on the target instead of ever spawning fire.
        /// </summary>
        private static int ApplyIgnite(SpellPlanStep step, Pawn caster, Thing primary, IntVec3 cell, Map map)
        {
            int affected = 0;

            foreach (Thing target in CollectTargets(step, primary, cell, map, CanBurn))
            {
                if (step.sensitive)
                {
                    if (target is Pawn pawn)
                    {
                        ApplyDotState(pawn, SpellEffectDefs.HediffScorch, step, caster,
                            DamageDefOf.Burn, step.burnAmount, step.durationTicks);
                        affected++;
                    }
                    else
                    {
                        target.TakeDamage(new DamageInfo(DamageDefOf.Burn, step.amount,
                            armorPenetration: step.penetration, angle: -1f, instigator: caster,
                            intendedTarget: target, instigatorGuilty: true, spawnFilth: false));
                        affected++;
                    }

                    continue;
                }

                if (target is Pawn)
                {
                    target.TryAttachFire(step.fireSize, caster);
                    affected++;
                }
                else if (FireUtility.TryStartFireIn(target.Position, map, step.fireSize, caster))
                {
                    affected++;
                }
            }

            if (primary == null && !step.sensitive && GenGrid.InBounds(cell, map))
            {
                if (FireUtility.TryStartFireIn(cell, map, step.fireSize, caster))
                {
                    affected++;
                }
            }

            return affected;
        }

        /// <summary>
        /// 落雷: EMP first, then burn damage scaled by the 受电 stacks already on the target, then one
        /// more stack. The stacks are a Def-driven hediff with a severity cap, so the bookkeeping is
        /// the same for every target and survives a save.
        /// </summary>
        private static int ApplyLightning(SpellPlanStep step, Pawn caster, Thing primary, IntVec3 cell, Map map)
        {
            int affected = 0;
            foreach (Thing target in CollectTargets(step, primary, cell, map, SpellTargets.CanTakeDamage))
            {
                float stacks = 0f;
                if (target is Pawn pawn)
                {
                    Hediff charge = SpellEffectRuntime.FindHediff(pawn, SpellEffectDefs.HediffLightningCharge);
                    stacks = charge?.Severity ?? 0f;
                }

                if (step.empAmount > 0f)
                {
                    target.TakeDamage(new DamageInfo(DamageDefOf.EMP, step.empAmount,
                        armorPenetration: 0f, angle: -1f, instigator: caster,
                        intendedTarget: target, instigatorGuilty: true, spawnFilth: false));
                }

                float damage = step.amount * (1f + stacks * step.perStackFactor);
                target.TakeDamage(new DamageInfo(DamageDefOf.Burn, damage,
                    armorPenetration: step.penetration, angle: -1f,
                    instigator: caster, intendedTarget: target, instigatorGuilty: true, spawnFilth: false));

                if (target is Pawn charged)
                {
                    // Adding another instance merges into the existing stack, so each strike raises
                    // severity by the Def's initial value until the Def's cap.
                    SpellEffectRuntime.AddTimedHediff(charged, SpellEffectDefs.HediffLightningCharge, step.durationTicks);
                }

                affected++;
            }

            return affected;
        }

        /// <summary>霜冻/缚足/咒印: one timed Def-driven state on each target.</summary>
        private static int ApplyTimedState(SpellPlanStep step, Thing primary, IntVec3 cell, Map map, string hediffDefName)
        {
            int affected = 0;
            foreach (Thing target in CollectTargets(step, primary, cell, map, SpellTargets.CanBeHealed))
            {
                if (target is Pawn pawn)
                {
                    SpellEffectRuntime.AddTimedHediff(pawn, hediffDefName, step.durationTicks);
                    affected++;
                }
            }

            return affected;
        }

        /// <summary>冰裂: damage, multiplied when the target already carries the Freeze state.</summary>
        private static int ApplyColdSnap(SpellPlanStep step, Pawn caster, Thing primary, IntVec3 cell, Map map)
        {
            int affected = 0;

            foreach (Thing target in CollectTargets(step, primary, cell, map, SpellTargets.CanTakeDamage))
            {
                bool slowed = target is Pawn pawn
                              && SpellEffectRuntime.FindHediff(pawn, SpellEffectDefs.HediffFreeze) != null;
                float amount = slowed ? step.amount * Mathf.Max(1f, step.vulnerableFactor) : step.amount;
                SpellDamage.Deal(target, amount, step.penetration, caster);
                affected++;
            }

            return affected;
        }

        private static int ApplyMove(SpellPlanStep step, Thing primary, IntVec3 cell, Map map, bool toward)
        {
            int affected = 0;
            foreach (Thing target in CollectTargets(step, primary, cell, map, SpellTargets.CanBeHealed))
            {
                if (target is Pawn pawn && SpellEffectRuntime.TryMovePawn(pawn, cell, step.amount, toward, map))
                {
                    affected++;
                }
            }

            return affected;
        }

        /// <summary>
        /// 闪现: from a Self cast the caster flickers forward; delivered by a dart, the caster lands at
        /// the impact point. Both use the same standable-cell check.
        /// </summary>
        private static int ApplyBlink(SpellPlanStep step, Pawn caster, Thing primary, IntVec3 cell, Map map)
        {
            if (caster == null || !caster.Spawned || caster.Map != map)
            {
                return 0;
            }

            IntVec3 destination;
            if (primary == caster)
            {
                destination = SpellEffectRuntime.ForwardCell(caster, Mathf.RoundToInt(step.amount));
            }
            else
            {
                destination = cell;
            }

            return SpellEffectRuntime.TryTeleport(caster, destination, map) ? 1 : 0;
        }

        private static int SpawnField(SpellPlanStep step, Pawn caster, IntVec3 cell, Map map, SpellFieldMode mode)
        {
            if (!GenGrid.InBounds(cell, map) || step.payload == null || step.payload.steps.Count == 0)
            {
                return 0;
            }

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(SpellEffectDefs.ThingSpellField);
            if (def == null)
            {
                SpellDiagnostics.ErrorOnce("thing def " + SpellEffectDefs.ThingSpellField + " is missing; the field was not placed.");
                return 0;
            }

            var field = (Thing_SpellField)ThingMaker.MakeThing(def);
            field.Configure(mode, caster, step);
            GenSpawn.Spawn(field, cell, map);
            return 1;
        }

        private static int ScheduleDelay(SpellPlanStep step, Pawn caster, IntVec3 cell, Map map)
        {
            if (step.payload == null || step.payload.steps.Count == 0 || step.durationTicks <= 0)
            {
                return 0;
            }

            MapComponent_SpellDelays component = map.GetComponent<MapComponent_SpellDelays>();
            if (component == null)
            {
                return 0;
            }

            component.Schedule(caster, cell, Find.TickManager.TicksGame + step.durationTicks, step.payload.steps);
            return 1;
        }

        private static void ApplyDotState(
            Pawn pawn,
            string hediffDefName,
            SpellPlanStep step,
            Pawn caster,
            DamageDef damageDef,
            float amount,
            int durationTicks)
        {
            Hediff dot = SpellEffectRuntime.MakeTimedHediff(pawn, hediffDefName, durationTicks);
            if (dot == null)
            {
                return;
            }

            if (dot is Hediff_SpellDot sustained)
            {
                sustained.damageDef = damageDef;
                sustained.damagePerPulse = Mathf.Max(0.01f, amount);
                sustained.armorPenetration = step.penetration;
                sustained.pulseIntervalTicks = step.pulseIntervalTicks > 0 ? step.pulseIntervalTicks : 60;
                sustained.instigator = caster;
            }

            pawn.health.AddHediff(dot);
        }

        /// <summary>Pawns, buildings, plants and items: anything a fire could plausibly start on.</summary>
        private static bool CanBurn(Thing thing)
        {
            if (thing == null || thing.Destroyed || !thing.Spawned)
            {
                return false;
            }

            return thing is Pawn || thing.def.useHitPoints;
        }

        /// <summary>
        /// Targets of one step: the single hit without AOE, every distinct thing inside the working
        /// radius with it. The list is materialised before anything resolves, because an effect can
        /// remove or spawn things while it runs.
        /// </summary>
        private static List<Thing> CollectTargets(
            SpellPlanStep step,
            Thing primary,
            IntVec3 cell,
            Map map,
            Predicate<Thing> accept)
        {
            var found = new List<Thing>();
            if (!step.aoe)
            {
                Thing target = primary;
                if (target == null)
                {
                    // A delayed payload has no picked thing any more: it resolves where it was aimed,
                    // so the target is whatever now occupies that cell.
                    target = FirstAt(cell, map, accept);
                }

                if (target != null && accept(target))
                {
                    found.Add(target);
                }

                return found;
            }

            if (!GenGrid.InBounds(cell, map))
            {
                return found;
            }

            var seen = new HashSet<Thing>();
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(cell, map, step.radius, true))
            {
                if (thing != null && accept(thing) && seen.Add(thing))
                {
                    found.Add(thing);
                }
            }

            return found;
        }

        /// <summary>First acceptable spawned thing standing on that cell; null when the cell is bare.</summary>
        private static Thing FirstAt(IntVec3 cell, Map map, Predicate<Thing> accept)
        {
            if (!GenGrid.InBounds(cell, map))
            {
                return null;
            }

            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                if (accept(things[i]))
                {
                    return things[i];
                }
            }

            return null;
        }
    }
}
