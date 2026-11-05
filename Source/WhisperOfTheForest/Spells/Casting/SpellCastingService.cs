using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Experimental.Missiles;

namespace WhisperOfTheForest.Experimental.Spells
{
    /// <summary>
    /// Entry-independent casting: windup, channel and recovery, driven on game ticks. The book page,
    /// the cast gizmo and the future staff all call <see cref="Begin"/> with a compiled plan, and this
    /// service owns everything that happens afterwards; no entry point resolves a cast itself.
    ///
    /// Movement, by design:
    ///   windup    - the caster may walk at full speed (this is what the cast-from-gizmo flow relies
    ///               on: the cast never touches the pawn's job);
    ///   channel   - the caster may walk, slowed to <see cref="SpellTuningDef.channelMoveSpeedFactor"/>;
    ///   recovery  - the caster is rooted for the recovery ticks;
    ///   both movement rules live in Patch_CastingMovement.
    ///
    /// Interruptions, which drop the cast without resolving anything and without starting the
    /// cooldown: the caster dies, is destroyed, leaves the map, is downed or stunned; a touched thing
    /// is destroyed or despawned; a touched target is out of reach or out of sight at release.
    /// Walking, taking damage and losing a projectile's target do not interrupt - a projectile
    /// re-homes on the thing it was aimed at while that thing is alive, and otherwise flies at the
    /// locked cell.
    /// </summary>
    public static class SpellCastingService
    {
        /// <summary>
        /// Starts a cast and returns whether it was accepted. A phase whose timing is zero ends the
        /// moment it starts, so a fully instant spell resolves before this call returns; an
        /// interrupted one logs why and resolves nothing.
        /// </summary>
        public static bool Begin(Pawn caster, SpellCastPlan plan, LocalTargetInfo target, out string reason)
        {
            reason = null;
            if (plan == null)
            {
                reason = SpellText.T("WotF_Spells.Reason.Unknown");
                return false;
            }

            if (caster == null || !caster.Spawned || caster.Map == null)
            {
                reason = SpellText.T("WotF_Spells.Reason.PawnNotSpawned");
                return false;
            }

            GameComponent_SpellCasts component = GameComponent_SpellCasts.Current;
            if (component == null)
            {
                reason = SpellText.T("WotF_Spells.Reason.NoGameComponent");
                return false;
            }

            if (component.FindFor(caster) != null)
            {
                reason = SpellText.T("WotF_Spells.Reason.AlreadyCasting");
                return false;
            }

            if (!TryLockTarget(caster, plan, target, out Thing targetThing, out IntVec3 cell, out reason))
            {
                return false;
            }

            int now = Find.TickManager.TicksGame;
            var cast = new SpellCast
            {
                caster = caster,
                map = caster.Map,
                targetThing = targetThing,
                cell = cell,
                plan = plan,
                phase = SpellCastPhase.Windup,
                startedTick = now,
                phaseEndTick = now + plan.timing.windupTicks
            };

            SpellDiagnostics.Message("cast '" + plan.recipeName + "' begins form=" + plan.form
                                     + " timing=" + plan.timing + " (windup/channel/recovery)"
                                     + " target=" + cell);

            component.Add(cast);
            if (Advance(cast, now))
            {
                component.Remove(cast);
            }

            return true;
        }

        /// <summary>Drops the pawn's cast in progress, if any. Nothing resolves and no cooldown starts.</summary>
        public static bool Cancel(Pawn caster)
        {
            SpellCast cast = FindCast(caster);
            if (cast == null)
            {
                return false;
            }

            GameComponent_SpellCasts.Current?.Remove(cast);
            SpellDiagnostics.Message("cast '" + NameOf(cast) + "' cancelled by request during " + cast.phase);
            return true;
        }

        /// <summary>The pawn's cast in progress, or null when it is not casting.</summary>
        public static SpellCast FindCast(Pawn pawn)
        {
            return pawn == null ? null : GameComponent_SpellCasts.Current?.FindFor(pawn);
        }

        public static bool IsCasting(Pawn pawn)
        {
            return FindCast(pawn) != null;
        }

        /// <summary>Movement multiplier for the pawn's current phase; 1 when it has no cast.</summary>
        public static float MoveFactorFor(Pawn pawn)
        {
            SpellCast cast = FindCast(pawn);
            if (cast == null || cast.phase != SpellCastPhase.Channel)
            {
                return 1f;
            }

            return SpellDefs.Tuning.channelMoveSpeedFactor;
        }

        /// <summary>True while the pawn's cast sits in recovery, the phase that roots it.</summary>
        public static bool IsRooted(Pawn pawn)
        {
            SpellCast cast = FindCast(pawn);
            return cast != null && cast.phase == SpellCastPhase.Recovery;
        }

        /// <summary>
        /// Advances one cast to its current phase and runs every transition that is already due.
        /// Returns true when the cast is over (finished or interrupted) and must be dropped.
        /// </summary>
        internal static bool Advance(SpellCast cast, int now)
        {
            while (true)
            {
                if (!IsStillValid(cast, out string invalid))
                {
                    SpellDiagnostics.Message("cast '" + NameOf(cast) + "' interrupted during "
                                             + cast.phase + ": " + invalid);
                    return true;
                }

                if (now < cast.phaseEndTick)
                {
                    return false;
                }

                switch (cast.phase)
                {
                    case SpellCastPhase.Windup:
                        cast.phase = SpellCastPhase.Channel;
                        cast.phaseEndTick = now + cast.plan.timing.channelTicks;
                        BeginChannel(cast);
                        break;

                    case SpellCastPhase.Channel:
                        if (!Release(cast, out string failure))
                        {
                            SpellDiagnostics.Message("cast '" + NameOf(cast) + "' interrupted at release: "
                                                     + failure);
                            return true;
                        }

                        cast.phase = SpellCastPhase.Recovery;
                        cast.phaseEndTick = now + cast.plan.timing.recoveryTicks;
                        break;

                    case SpellCastPhase.Recovery:
                        SpellDiagnostics.Debug("cast '" + NameOf(cast) + "' settled after "
                                                + (now - cast.startedTick) + " ticks");
                        return true;

                    default:
                        return true;
                }
            }
        }

        /// <summary>
        /// Resolves the target the entry point passed. Self runs on the caster; Touch needs a thing in
        /// reach; Projectile and Railgun aim at a cell (and keep the picked thing for re-homing).
        /// Everything that can be refused up front is refused here, with player-facing text.
        /// </summary>
        private static bool TryLockTarget(
            Pawn caster,
            SpellCastPlan plan,
            LocalTargetInfo target,
            out Thing targetThing,
            out IntVec3 cell,
            out string reason)
        {
            targetThing = null;
            reason = null;

            if (plan.form == SpellFormKind.Self || plan.form == SpellFormKind.Orbit)
            {
                targetThing = caster;
                cell = caster.Position;
                return true;
            }

            Map map = caster.Map;
            if (!target.IsValid)
            {
                cell = IntVec3.Invalid;
                reason = SpellText.T("WotF_Spells.Reason.NoTarget");
                return false;
            }

            if (target.Thing != null && target.Thing.Map != map)
            {
                cell = IntVec3.Invalid;
                reason = SpellText.T("WotF_Spells.Reason.TargetOtherMap");
                return false;
            }

            cell = target.Cell;
            if (!GenGrid.InBounds(cell, map))
            {
                reason = SpellText.T("WotF_Spells.Reason.TargetOutOfBounds");
                return false;
            }

            if (plan.form == SpellFormKind.Touch)
            {
                if (!SpellService.IsInTouchReach(caster.Position, cell))
                {
                    reason = SpellText.T("WotF_Spells.Reason.TouchNotAdjacent");
                    return false;
                }

                if (!SpellService.HasLineOfSight(caster, cell, map))
                {
                    reason = SpellText.T("WotF_Spells.Reason.NoLineOfSight");
                    return false;
                }
            }

            SpellRailgunSpec railgun = plan.Railgun;
            if (railgun != null && railgun.range > 0f && !caster.Position.InHorDistOf(cell, railgun.range))
            {
                reason = SpellText.TF("WotF_Spells.Reason.RailgunOutOfRange", railgun.range.ToString("0.#"));
                return false;
            }

            targetThing = target.Thing;
            return true;
        }

        /// <summary>
        /// Everything that ends a cast early. The map reference is checked too: a scribed cast whose
        /// caster was removed by a save/load has no live caster and is dropped here.
        /// </summary>
        private static bool IsStillValid(SpellCast cast, out string reason)
        {
            reason = null;
            Pawn caster = cast.caster;
            if (caster == null || cast.map == null || cast.plan == null)
            {
                reason = "the cast lost its caster, map or plan";
                return false;
            }

            if (caster.Destroyed || !caster.Spawned || caster.Map != cast.map)
            {
                reason = "the caster left the map";
                return false;
            }

            if (caster.Dead)
            {
                reason = "the caster died";
                return false;
            }

            if (caster.Downed)
            {
                reason = "the caster was downed";
                return false;
            }

            if (caster.stances != null && caster.stances.stunner != null && caster.stances.stunner.Stunned)
            {
                reason = "the caster was stunned";
                return false;
            }

            if (cast.plan.form == SpellFormKind.Touch && cast.targetThing != null
                && (cast.targetThing.Destroyed || !cast.targetThing.Spawned))
            {
                reason = "the touched thing is gone";
                return false;
            }

            return true;
        }

        /// <summary>Player-facing name for logs; a cast that lost its plan still logs as something.</summary>
        private static string NameOf(SpellCast cast)
        {
            return cast.plan != null ? cast.plan.recipeName : "?";
        }

        /// <summary>
        /// Start of the channel. Only a composite form needs it: its charge-up is the channel, so the
        /// visual request is handed over here with the channel length it must cover.
        /// </summary>
        private static void BeginChannel(SpellCast cast)
        {
            if (cast.plan.form != SpellFormKind.Railgun)
            {
                return;
            }

            RailgunCastService.PlayCharge(cast.map, cast.caster, cast.cell, cast.plan.Railgun,
                cast.plan.timing.channelTicks);
        }

        /// <summary>
        /// End of the channel: the spell really happens. Each form reuses its existing payload path;
        /// this method only decides which one and starts the cooldown.
        /// </summary>
        private static bool Release(SpellCast cast, out string failure)
        {
            failure = null;
            Pawn caster = cast.caster;
            Map map = cast.map;
            SpellCastPlan plan = cast.plan;

            switch (plan.form)
            {
                case SpellFormKind.Railgun:
                {
                    string report = RailgunCastService.Resolve(map, caster, cast.cell, plan.Railgun);
                    SpellDiagnostics.Message("cast '" + plan.recipeName + "' released form=railgun aim="
                                             + cast.cell + " " + report);
                    break;
                }

                case SpellFormKind.Projectile:
                {
                    ArcaneMissileSettings settings = BuildMissileSettings(plan, out failure);
                    if (settings == null)
                    {
                        return false;
                    }

                    // A live picked thing is re-homed on; a dead or despawned one leaves only the
                    // locked cell, so the dart still flies where the player aimed.
                    LocalTargetInfo aim = cast.targetThing != null && cast.targetThing.Spawned
                        ? new LocalTargetInfo(cast.targetThing)
                        : new LocalTargetInfo(cast.cell);

                    int launched = ArcaneMissileLauncher.Fire(caster, aim, settings);
                    if (launched <= 0)
                    {
                        failure = "the dart could not be launched";
                        return false;
                    }

                    SpellDiagnostics.Message("cast '" + plan.recipeName + "' released form=projectile segments="
                                             + plan.SegmentCount + " launched=" + launched + "/" + plan.missileCount
                                             + " speed=" + plan.missileSpeed.ToString("0.##"));
                    break;
                }

                case SpellFormKind.Orbit:
                {
                    SpellSegment first = plan.SegmentAt(0) ?? SpellExecutor.FirstSegmentOf(plan);
                    int spawned = SpellOrbitService.Spawn(map, caster, first);
                    if (spawned <= 0)
                    {
                        failure = "the orbs could not be placed";
                        return false;
                    }

                    SpellDiagnostics.Message("cast '" + plan.recipeName + "' released form=orbit orbs=" + spawned);
                    break;
                }

                default:
                {
                    if (!TryResolveContact(cast, out Thing primary, out IntVec3 cell, out failure))
                    {
                        return false;
                    }

                    string report = SpellExecutor.Execute(plan, caster, primary, cell, map);
                    SpellDiagnostics.Message("cast '" + plan.recipeName + "' released form=" + plan.form
                                             + " at " + cell + " " + report);
                    break;
                }
            }

            GameComponent_Spellbook.Current?.NotifyCast(caster);
            Messages.Message(SpellText.TF("WotF_Spells.CastDone", plan.recipeName),
                MessageTypeDefOf.NeutralEvent, false);
            return true;
        }

        /// <summary>
        /// Self resolves on the caster where it stands at release; Touch resolves on the thing that was
        /// picked, and only while it is still adjacent and visible.
        /// </summary>
        private static bool TryResolveContact(SpellCast cast, out Thing primary, out IntVec3 cell, out string failure)
        {
            primary = null;
            failure = null;
            Pawn caster = cast.caster;

            if (cast.plan.form == SpellFormKind.Self)
            {
                primary = caster;
                cell = caster.Position;
                return true;
            }

            Thing target = cast.targetThing;
            if (target == null || target.Destroyed || !target.Spawned || target.Map != cast.map)
            {
                cell = IntVec3.Invalid;
                failure = "the touched thing is gone";
                return false;
            }

            cell = target.Position;
            if (!SpellService.IsInTouchReach(caster.Position, cell))
            {
                failure = "the caster is no longer next to the target";
                return false;
            }

            if (!SpellService.HasLineOfSight(caster, cell, cast.map))
            {
                failure = "something is between the caster and the target";
                return false;
            }

            primary = target;
            return true;
        }

        /// <summary>
        /// Settings for one spell dart. Only the payload is new: the flight envelope, the impact
        /// dispatch and the saved copy are the existing missile machinery.
        /// </summary>
        private static ArcaneMissileSettings BuildMissileSettings(SpellCastPlan plan, out string failure)
        {
            failure = null;
            ArcaneMissileImpactDef impact =
                DefDatabase<ArcaneMissileImpactDef>.GetNamedSilentFail(SpellDefs.SpellImpactDefName);
            if (impact == null)
            {
                SpellDiagnostics.ErrorOnce("impact def " + SpellDefs.SpellImpactDefName
                                           + " is missing; spells cannot fly.");
                failure = "impact def " + SpellDefs.SpellImpactDefName + " is missing";
                return null;
            }

            SpellTuningDef tuning = SpellDefs.Tuning;
            SpellSegment first = plan.SegmentAt(0) ?? SpellExecutor.FirstSegmentOf(plan);
            float harm = first.LargestAmount(SpellEffectKind.Harm);
            float explosionDamage = first.LargestAmount(SpellEffectKind.Explosion);
            float heal = first.LargestAmount(SpellEffectKind.Heal);

            ArcaneMissileSettings settings = ArcaneMissileSettings.CreateDefault();
            settings.missileCount = first.missileCount;
            settings.speedCellsPerSecond = first.missileSpeed;
            settings.maxTurnDegreesPerSecond = tuning.missileTurnDegreesPerSecond;
            settings.maxRangeCells = Mathf.Max(tuning.castRange, first.missileSpeed * 4f);
            settings.damageDef = ArcaneMissileDefs.ArcaneForce;
            settings.armorPenetration = tuning.armorPenetration;

            // Readable numbers for the lab/tooltip. The payload itself is the frozen plan below.
            settings.damageAmount = Mathf.Max(harm, explosionDamage);
            settings.healAmount = heal;
            settings.explosionRadius = Mathf.Max(1.9f, first.LargestRadius(SpellEffectKind.Explosion));

            settings.impactDef = impact;
            settings.visualDef = null; // the launcher's documented default backend
            settings.expireOnTargetLost = false;
            settings.missileColorHex = "#7FE3C7";
            settings.trailColorHex = "#2FA98C";

            // Frozen at launch: the missile deep-copies this and scribes it with itself. A cast always
            // starts at the first segment; the trigger hand-over writes the following indices.
            settings.spellPlan = plan.Copy();
            settings.spellSegmentIndex = 0;
            return settings;
        }
    }
}
