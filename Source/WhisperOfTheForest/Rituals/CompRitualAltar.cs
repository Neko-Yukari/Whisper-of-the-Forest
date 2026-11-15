using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using WhisperOfTheForest.AncientRuins;
using WhisperOfTheForest.Equipment;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Race;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Teleport;

namespace WhisperOfTheForest.Rituals
{
    public class CompProperties_RitualAltar : CompProperties
    {
        public int tier;
        public StatDef powerStat;
        public float linkRadius = 12.9f;
        public List<string> ritualDefNames = new List<string>();

        public CompProperties_RitualAltar()
        {
            compClass = typeof(CompRitualAltar);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (tier < 1 || tier > 3) yield return parentDef.defName + " altar tier must be between 1 and 3.";
            if (powerStat == null) yield return parentDef.defName + " altar must declare powerStat.";
            var runeRituals = new List<RitualDef>();
            foreach (string name in ritualDefNames)
            {
                RitualDef ritual = DefDatabase<RitualDef>.GetNamedSilentFail(name);
                if (ritual == null)
                    yield return parentDef.defName + " has unknown ritual " + name;
                else if (ritual.chooseRune)
                    runeRituals.Add(ritual);
            }
            if (runeRituals.Count != 1)
                yield return parentDef.defName + " needs exactly one rune awakening ritual.";
        }
    }

    /// <summary>
    /// The altar owns the active ritual: which Def is running, who hosts it, which chalk cell every
    /// participant stands on and which offering stacks the host has reserved. Nothing is paid when
    /// the rite starts - the network mana and the offerings are taken only when the chant completes,
    /// and a rite that is interrupted or cancelled costs nothing (技术/04-仪式实现.md:116).
    ///
    /// The host's job makes the reservations through the vanilla reservation manager, so a second
    /// altar cannot lock the same ground or shelf stack, and a released job releases them again
    /// (Pawn_JobTracker.CleanupCurrentJob -> ReleaseAllReservations).
    /// </summary>
    public class CompRitualAltar : ThingComp
    {
        private Pawn activeHost;
        private RitualDef activeDef;
        private SpellGlyphDef activeRune;
        private Building activeTeleportTarget;
        private Thing activeEquipment;
        private int activeBookSlot = -1;
        private QuestScriptDef activeRuinQuest;
        private RitualDef autoRitual;
        private RitualPhase phase;
        private float activePower;
        private float activeManaCost;
        private int activeRingSize = 3;
        private ChalkColor activeChalkColor;
        private int activeDurationTicks;

        private List<Pawn> activeHelpers = new List<Pawn>();
        private List<RitualStation> stations = new List<RitualStation>();
        private List<RitualOffering> offerings = new List<RitualOffering>();

        public CompProperties_RitualAltar Props => (CompProperties_RitualAltar)props;

        public bool IsActive => activeDef != null;
        public RitualDef ActiveDef => activeDef;
        public Pawn ActiveHost => activeHost;
        public IReadOnlyList<Pawn> ActiveHelpers => activeHelpers;
        public RitualPhase Phase => phase;
        public RitualDef AutoRitual => autoRitual;
        public int ActiveParticipants => activeHelpers.Count;
        public float ActivePower => activePower;
        public int ActiveDurationTicks => activeDurationTicks;

        public float FacilityPower => parent.GetComp<CompAffectedByFacilities>()?.GetStatOffset(Props.powerStat) ?? 0f;

        public string ActiveLabel => activeDef == null ? null
            : (activeDef.chooseRune && activeRune != null
                ? activeDef.LabelCap + ": " + activeRune.LabelCap
                : activeDef.chooseEquipment && activeEquipment != null
                    ? activeDef.LabelCap + ": " + activeEquipment.LabelShort
                    : activeDef.chooseRuinType && ActiveRuinOption != null
                        ? activeDef.LabelCap + ": " + ActiveRuinOption.sitePart.LabelCap
                        : (string)activeDef.LabelCap);

        /// <summary>The ruin this altar's seeking rite is looking for; null for every other rite.</summary>
        private RuinSeekOption ActiveRuinOption => activeDef == null ? null : activeDef.RuinOptionFor(activeRuinQuest);

        /// <summary>True while this pawn is the host of the rite this altar is running.</summary>
        public bool IsHost(Pawn pawn)
        {
            return activeDef != null && activeHost == pawn;
        }

        /// <summary>True while this pawn is one of the rite's helpers.</summary>
        public bool IsHelping(Pawn pawn)
        {
            return activeDef != null && pawn != null && activeHelpers.Contains(pawn);
        }

        public bool TryGetStation(Pawn pawn, out IntVec3 cell)
        {
            for (int i = 0; i < stations.Count; i++)
            {
                if (stations[i].Pawn == pawn)
                {
                    cell = stations[i].Cell;
                    return true;
                }
            }

            cell = IntVec3.Invalid;
            return false;
        }

        public void SetAutoRitual(RitualDef def)
        {
            autoRitual = def != null && def.autoRepeatable ? def : null;
        }

        public IEnumerable<RitualDef> Rituals
        {
            get
            {
                for (int i = 0; i < Props.ritualDefNames.Count; i++)
                {
                    RitualDef def = DefDatabase<RitualDef>.GetNamedSilentFail(Props.ritualDefNames[i]);
                    if (def != null && def.tier <= Props.tier)
                    {
                        yield return def;
                    }
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref activeHost, "ritualHost", true);
            Scribe_Defs.Look(ref activeDef, "ritualDef");
            Scribe_Defs.Look(ref activeRune, "ritualRune");
            Scribe_References.Look(ref activeTeleportTarget, "ritualTeleportTarget", true);
            Scribe_References.Look(ref activeEquipment, "ritualEquipment", true);
            Scribe_Values.Look(ref activeBookSlot, "ritualBookSlot", -1);
            Scribe_Defs.Look(ref activeRuinQuest, "ritualRuinQuest");
            Scribe_Defs.Look(ref autoRitual, "ritualAuto");
            Scribe_Values.Look(ref phase, "ritualPhase", RitualPhase.Gathering);
            Scribe_Values.Look(ref activePower, "ritualPower", 0f);
            Scribe_Values.Look(ref activeManaCost, "ritualManaCost", 0f);
            Scribe_Values.Look(ref activeRingSize, "ritualRingSize", 3);
            Scribe_Values.Look(ref activeChalkColor, "ritualChalkColor", ChalkColor.White);
            Scribe_Values.Look(ref activeDurationTicks, "ritualDurationTicks", 0);
            Scribe_Collections.Look(ref activeHelpers, "ritualHelpers", LookMode.Reference);
            Scribe_Collections.Look(ref stations, "ritualStations", LookMode.Deep);
            Scribe_Collections.Look(ref offerings, "ritualOfferings", LookMode.Deep);

            if (Scribe.mode != LoadSaveMode.PostLoadInit)
            {
                return;
            }

            if (autoRitual != null && !autoRitual.autoRepeatable)
            {
                autoRitual = null;
            }

            if (activeDef == null)
            {
                ClearRitualState();
                return;
            }

            // Save-load boundary: the performance only continues when the host, the rune, the target
            // and every reserved stack are still there. Otherwise the rite ends here, with nothing
            // spent, and the host's job fails on its own because IsHost turns false.
            if (activeHost == null || activeHost.Dead || !activeHost.Spawned
                || (activeDef.chooseRune && activeRune == null)
                || (activeDef.chooseTeleportTarget && activeTeleportTarget == null)
                || (activeDef.chooseEquipment && (activeEquipment == null || activeBookSlot < 0))
                || (activeDef.chooseRuinType && ActiveRuinOption == null)
                || stations.Count != activeHelpers.Count + 1
                || !OfferingsPresent(out _))
            {
                WotfLog.Info(WotfLog.ModuleRitual, "dropped a saved rite at load: " + activeDef.defName);
                ClearRitualState();
            }
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (activeDef != null)
            {
                VerifyRitual();
                return;
            }

            if (autoRitual != null)
            {
                TryStart(new RitualRequest { Def = autoRitual, Host = BestCaster() }, true);
            }
        }

        /// <summary>
        /// The rare tick safety net: a host that lost the job (downed, drafted, interrupted, another
        /// altar took them) ends the rite, and the roster is pruned to the pawns still performing it.
        /// </summary>
        private void VerifyRitual()
        {
            if (activeHost == null || activeHost.Dead || !activeHost.Spawned
                || !HasRitualJob(activeHost, RitualJobDef))
            {
                CancelRitual(null);
                return;
            }

            PruneLostHelpers();
            if (activeHelpers.Count < activeDef.minParticipants)
            {
                CancelRitual(null);
            }
        }

        private bool HasRitualJob(Pawn pawn, JobDef def)
        {
            Job current = pawn.jobs?.curJob;
            if (current != null && current.def == def && current.targetA.Thing == parent)
            {
                return true;
            }

            if (pawn.jobs?.jobQueue != null)
            {
                foreach (QueuedJob queued in pawn.jobs.jobQueue)
                {
                    if (queued.job != null && queued.job.def == def && queued.job.targetA.Thing == parent)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void PruneLostHelpers()
        {
            bool changed = false;
            for (int i = activeHelpers.Count - 1; i >= 0; i--)
            {
                Pawn helper = activeHelpers[i];
                if (helper == null || helper.Dead || !helper.Spawned || helper.Downed || helper.Drafted
                    || helper.InMentalState || !HasRitualJob(helper, AssistJobDef))
                {
                    WotfLog.Info(WotfLog.ModuleRitual,
                        "helper left " + (helper == null ? "<null>" : helper.LabelShort) + " at " + parent.Position);
                    activeHelpers.RemoveAt(i);
                    for (int s = stations.Count - 1; s >= 0; s--)
                    {
                        if (stations[s].Pawn == helper)
                        {
                            stations.RemoveAt(s);
                        }
                    }

                    changed = true;
                }
            }

            // Resonance is "everyone actually in the circle" (技术/04-仪式实现.md:105-116), so the
            // number the altar displays and the number Apply receives have to follow the seated roster.
            if (changed)
            {
                activePower = FacilityPower + PowerOf(activeHost, activeHelpers);
            }
        }

        /// <summary>
        /// Called by the host's gather toil every tick. The chant begins only when every participant
        /// is standing on their own chalk cell, which is what vanilla does: the gather toil completes
        /// on AllPawnsDone (PsychicRitualToil_Goto.cs:167) and early arrivals are parked on
        /// DutyDefOf.WaitForRitualParticipants (:201), so nobody chants while walking.
        /// </summary>
        public bool TryBeginChanting()
        {
            if (activeDef == null)
            {
                return false;
            }

            if (phase == RitualPhase.Chanting)
            {
                return true;
            }

            PruneLostHelpers();
            if (activeHelpers.Count < activeDef.minParticipants)
            {
                CancelRitual(null);
                return false;
            }

            if (!AllSeated())
            {
                return false;
            }

            phase = RitualPhase.Chanting;
            WotfLog.Info(WotfLog.ModuleRitual, "chant begins at " + parent.Position + " with "
                + (activeHelpers.Count + 1) + " participants");
            return true;
        }

        private bool AllSeated()
        {
            for (int i = 0; i < stations.Count; i++)
            {
                RitualStation station = stations[i];
                if (station.Pawn == null || !station.Pawn.Spawned || station.Pawn.Downed
                    || station.Pawn.Position != station.Cell)
                {
                    return false;
                }
            }

            return stations.Count > 0;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!parent.Spawned)
            {
                yield break;
            }

            yield return CodexGizmo();
            yield return HostGizmo();
            yield return AutoRitualGizmo();
        }

        public override string CompInspectStringExtra()
        {
            string text = IsActive
                ? (string)"WotF_Ritual_InspectActive".Translate(ActiveLabel)
                : (string)"WotF_Ritual_InspectIdle".Translate();
            if (IsActive)
            {
                text += "\n" + "WotF_Ritual_Participants".Translate(activeHelpers.Count);
                if (phase == RitualPhase.Gathering)
                {
                    text += "\n" + "WotF_Ritual_InspectGathering".Translate();
                }

                if (activeDef.chooseRune && activeRune != null)
                {
                    List<IntVec3> missing = RitualRing.MissingCells(parent.Map, parent.Position,
                        activeRingSize, activeChalkColor);
                    if (missing.Count > 0)
                    {
                        text += "\n" + "WotF_Ritual_RingIncomplete".Translate(missing.Count);
                    }
                }
            }

            text += "\n" + string.Format("WotF_Ritual_Power".Translate(), (IsActive ? activePower : FacilityPower).ToString("0.#"));
            return autoRitual == null
                ? text
                : text + "\n" + "WotF_Ritual_AutoRepeat_Current".Translate(autoRitual.LabelCap);
        }

        private Gizmo CodexGizmo()
        {
            return new Command_Action
            {
                defaultLabel = "WotF_Ritual_Gizmo_Codex".Translate(),
                defaultDesc = "WotF_Ritual_Gizmo_CodexDesc".Translate(),
                icon = RitualIcons.Codex,
                action = delegate { Find.WindowStack.Add(new Window_RitualCodex(this)); }
            };
        }

        private Gizmo HostGizmo()
        {
            return new Command_Action
            {
                defaultLabel = IsActive
                    ? "WotF_Ritual_InProgress".Translate(ActiveLabel)
                    : "WotF_Ritual_Gizmo_Host".Translate(),
                defaultDesc = "WotF_Ritual_Gizmo_HostDesc".Translate(),
                icon = RitualIcons.Host,
                action = delegate { Find.WindowStack.Add(new Window_RitualHost(this)); }
            };
        }

        private Gizmo AutoRitualGizmo()
        {
            var options = new List<FloatMenuOption>();
            foreach (RitualDef def in Rituals)
            {
                if (!def.autoRepeatable)
                {
                    continue;
                }

                RitualDef captured = def;
                options.Add(new FloatMenuOption(captured.LabelCap, delegate { SetAutoRitual(captured); }));
            }

            if (autoRitual != null)
            {
                options.Add(new FloatMenuOption(
                    "WotF_Ritual_AutoRepeat_Off".Translate(),
                    delegate { SetAutoRitual(null); }));
            }

            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption("WotF_Ritual_AutoRepeat_None".Translate(), null));
            }

            return new Command_Action
            {
                defaultLabel = autoRitual == null
                    ? "WotF_Ritual_Gizmo_Auto".Translate()
                    : "WotF_Ritual_AutoRepeat_Current".Translate(autoRitual.LabelCap),
                defaultDesc = "WotF_Ritual_AutoRepeat_Desc".Translate(),
                icon = RitualIcons.Auto,
                action = delegate { Find.WindowStack.Add(new FloatMenu(options)); }
            };
        }

        private static void Reject(bool silent, string text)
        {
            if (!silent)
            {
                Messages.Message(text, MessageTypeDefOf.RejectInput, false);
            }
        }

        public bool TryStart(RitualDef def)
        {
            return TryStart(def, null, BestCaster());
        }

        public bool TryStart(RitualDef def, Pawn host)
        {
            return TryStart(def, null, host);
        }

        public bool TryStart(RitualDef def, SpellGlyphDef rune)
        {
            return TryStart(def, rune, BestCaster());
        }

        public bool TryStart(RitualDef def, Building teleportTarget)
        {
            return TryStart(def, null, BestCaster(), teleportTarget);
        }

        public bool TryStart(RitualDef def, SpellGlyphDef rune, Pawn host, Building teleportTarget = null,
            bool silent = false)
        {
            return TryStart(new RitualRequest
            {
                Def = def,
                Rune = rune,
                Host = host,
                TeleportTarget = teleportTarget
            }, silent);
        }

        public bool TryStart(RitualDef def, SpellGlyphDef rune, Pawn host, List<Pawn> helpers,
            Building teleportTarget = null, bool silent = false)
        {
            var request = new RitualRequest
            {
                Def = def,
                Rune = rune,
                Host = host,
                TeleportTarget = teleportTarget
            };
            if (helpers != null)
            {
                request.Helpers.AddRange(helpers);
            }

            return TryStart(request, silent);
        }

        /// <summary>The single start path: validate once, then hand the jobs out.</summary>
        public bool TryStart(RitualRequest request, bool silent = false)
        {
            RitualAssessment assessment = Assess(request);
            if (!assessment.CanStart)
            {
                Reject(silent, assessment.FirstProblem);
                return false;
            }

            // State first, so the drivers already find a host and their stations on their first tick.
            activeDef = request.Def;
            activeRune = request.Rune;
            activeTeleportTarget = request.TeleportTarget;
            activeEquipment = request.Equipment;
            activeBookSlot = request.BookSlot;
            activeRuinQuest = request.RuinOption?.quest;
            activeHost = request.Host;
            activeHelpers = new List<Pawn>(request.Helpers);
            stations = new List<RitualStation>(assessment.Stations);
            offerings = new List<RitualOffering>(assessment.Offerings);
            phase = RitualPhase.Gathering;
            activePower = assessment.Power;
            activeManaCost = assessment.ManaCost;
            activeRingSize = assessment.RingSize;
            activeChalkColor = assessment.ChalkColor;
            activeDurationTicks = assessment.DurationTicks;

            Job hostJob = JobMaker.MakeJob(RitualJobDef, parent);
            if (TryGetStation(activeHost, out IntVec3 hostCell))
            {
                hostJob.targetB = hostCell;
            }

            // TryTakeOrderedJob makes the pre-toil reservations (Pawn_JobTracker.cs:924): the altar
            // plus every offering stack. A false here means somebody else holds them, so nothing is
            // started and nothing is spent.
            if (!activeHost.jobs.TryTakeOrderedJob(hostJob, JobTag.Misc))
            {
                ClearRitualState();
                Reject(silent, "WotF_Ritual_NoMaterials".Translate(""));
                return false;
            }

            var helperJobs = new List<Job>(activeHelpers.Count);
            for (int i = 0; i < activeHelpers.Count; i++)
            {
                Pawn helper = activeHelpers[i];
                Job helperJob = JobMaker.MakeJob(AssistJobDef, parent);
                if (TryGetStation(helper, out IntVec3 cell))
                {
                    helperJob.targetB = cell;
                }

                if (!helper.jobs.TryTakeOrderedJob(helperJob, JobTag.Misc))
                {
                    Pawn busyHost = activeHost;
                    for (int j = 0; j < helperJobs.Count; j++)
                    {
                        activeHelpers[j].jobs.EndCurrentOrQueuedJob(helperJobs[j], JobCondition.InterruptForced, false);
                    }

                    // Clear before ending the host job: its finish action calls NotifyHostJobEnded, which
                    // would otherwise announce a cancelled rite on top of the real reason below.
                    ClearRitualState();
                    busyHost.jobs.EndCurrentOrQueuedJob(hostJob, JobCondition.InterruptForced, false);
                    Reject(silent, "WotF_Ritual_HelperBusy".Translate(helper.LabelShort));
                    return false;
                }

                helperJobs.Add(helperJob);
            }

            SetAutoRitual(autoRitual);
            WotfLog.Info(WotfLog.ModuleRitual,
                "started " + request.Def.defName + (request.Rune == null ? "" : " for " + request.Rune.defName)
                + " hosted by " + activeHost.LabelShort + " with " + activeHelpers.Count + " helpers at " + parent.Position);
            return true;
        }

        /// <summary>
        /// Reserves every offering stack, and the equipment being inscribed, for the host's job so haulers
        /// leave them on the ring. Called by the job's pre-toils.
        /// </summary>
        public bool ReserveOfferings(Pawn claimant, Job job, bool errorOnFailed)
        {
            for (int i = 0; i < offerings.Count; i++)
            {
                RitualOffering portion = offerings[i];
                if (portion?.Thing == null)
                {
                    return false;
                }

                if (!claimant.Reserve(portion.Thing, job, 1, portion.Count, null, errorOnFailed))
                {
                    return false;
                }
            }

            return activeEquipment == null || claimant.Reserve(activeEquipment, job, 1, -1, null, errorOnFailed);
        }

        /// <summary>The host's last toil: pay, then apply. A missing stack or a short network fails loudly.</summary>
        public void CompleteRitual(Pawn host)
        {
            if (activeDef == null || activeHost != host)
            {
                return;
            }

            if (!parent.Spawned)
            {
                CancelRitual(host);
                return;
            }

            if (!OfferingsPresent(out string missing))
            {
                FailRitual("WotF_Ritual_Complete_OfferingsGone".Translate(missing));
                return;
            }

            // The item may have been carried off and the page rewritten while the ring chanted; both
            // are checked before anything is charged, like the offerings.
            if (activeDef.chooseEquipment)
            {
                SpellRecipe page = GameComponent_Spellbook.Current.GetSlot(host, activeBookSlot);
                if (activeEquipment == null || !IsEnchantableNearby(activeEquipment, activeDef)
                    || page == null || !CompEnchantedEquipment.AcceptsRecipe(page, out _))
                {
                    FailRitual("WotF_Ritual_Inscribe_Lost".Translate());
                    return;
                }
            }

            // The gate may have been closed, cut off from its network or destroyed while the ring was
            // chanting. Checked here, with the offerings and the mana, so a lost gate costs nothing.
            if (activeDef.chooseTeleportTarget
                && (activeTeleportTarget == null || !activeTeleportTarget.Spawned
                    || activeTeleportTarget.TryGetComp<CompTeleportGate>()?.Running != true))
            {
                FailRitual("WotF_Ritual_Complete_GateLost".Translate());
                return;
            }

            // Two altars can chant the same rite at once, and the first one to finish may have placed
            // its ruin while this one was still chanting. The single-ruin rule is re-checked here,
            // together with the offerings and the mana, before anything is taken.
            if (activeDef.chooseRuinType && RuinSeeking.HasOutstandingRuin())
            {
                FailRitual("WotF_Ritual_RuinSeek_AlreadyOutstanding".Translate());
                return;
            }

            ManaNetwork network = Network();
            if (network == null || network.Stored + 0.0001f < activeManaCost)
            {
                FailRitual("WotF_Ritual_Complete_NoMana".Translate(activeManaCost.ToString("0.#"),
                    (network == null ? 0f : network.Stored).ToString("0.#")));
                return;
            }

            RitualDef def = activeDef;
            SpellGlyphDef rune = activeRune;
            Building teleportTarget = activeTeleportTarget;
            Thing equipment = activeEquipment;
            int bookSlot = activeBookSlot;
            RuinSeekOption ruinOption = ActiveRuinOption;
            int participants = activeHelpers.Count;
            float power = FacilityPower + PowerOf(activeHost, activeHelpers);

            // The rite carries the colonists standing near the altar to the gate. Their landing cells are
            // settled here, with the offerings and the mana, so a gate hemmed in by walls or pawns costs
            // nothing: the effect only carries out a plan that already exists.
            Dictionary<Pawn, IntVec3> teleportLanding = null;
            if (def.chooseTeleportTarget)
            {
                List<Pawn> passengers = TeleportGateTransfer.RitePassengers(parent.Map, parent.Position,
                    def.RadiusFor(power));
                if (passengers.Count == 0)
                {
                    FailRitual("WotF_Ritual_Teleport_NoPassengers".Translate());
                    return;
                }

                if (!TeleportGateTransfer.TryPlanRiteLanding(teleportTarget.GetComp<CompTeleportGate>(),
                        passengers, out teleportLanding, out int unplaced))
                {
                    FailRitual("WotF_Ritual_Teleport_NoRoom".Translate(unplaced, passengers.Count));
                    return;
                }
            }

            network.TryDraw(activeManaCost);
            ConsumeOfferings();
            ClearRitualState();
            def.EffectWorker.Apply(new RitualContext(parent.Map, (Building)parent, host, def, rune,
                teleportTarget, participants, power, equipment, bookSlot, ruinOption, teleportLanding));
            WotfLog.Info(WotfLog.ModuleRitual,
                "completed " + def.defName + (rune == null ? "" : " for " + rune.defName)
                + " by " + host.LabelShort + " participants=" + participants + " power=" + power);
        }

        /// <summary>The host's job ended while the rite was still running: nothing is charged.</summary>
        public void NotifyHostJobEnded(Pawn host)
        {
            if (activeDef == null || activeHost != host)
            {
                return;
            }

            CancelRitual(host);
        }

        public void CancelRitual(Pawn caster)
        {
            if (activeDef == null || (caster != null && activeHost != caster))
            {
                return;
            }

            RitualDef def = activeDef;
            ClearRitualState();
            WotfLog.Info(WotfLog.ModuleRitual, "cancelled " + def.defName + " at " + parent.Position);
            Messages.Message("WotF_Ritual_Cancelled".Translate(def.LabelCap), parent,
                MessageTypeDefOf.NegativeEvent, false);
        }

        private void FailRitual(string reason)
        {
            RitualDef def = activeDef;
            ClearRitualState();
            WotfLog.Info(WotfLog.ModuleRitual, "failed " + def.defName + ": " + reason);
            Messages.Message(reason, parent, MessageTypeDefOf.NegativeEvent, false);
        }

        private void ClearRitualState()
        {
            activeHost = null;
            activeDef = null;
            activeRune = null;
            activeTeleportTarget = null;
            activeEquipment = null;
            activeBookSlot = -1;
            activeRuinQuest = null;
            phase = RitualPhase.Gathering;
            activePower = 0f;
            activeManaCost = 0f;
            activeRingSize = 3;
            activeChalkColor = ChalkColor.White;
            activeDurationTicks = 0;
            activeHelpers = new List<Pawn>();
            stations = new List<RitualStation>();
            offerings = new List<RitualOffering>();
        }

        private ManaNetwork Network()
        {
            return MapComponent_ManaNetwork.For(parent.Map)?.NetworkNear(parent.Position, Props.linkRadius);
        }

        private static float PowerOf(Pawn host, IList<Pawn> helpers)
        {
            float total = 0f;
            Gene_ForestBloodline hostGene = host == null ? null : Gene_ForestBloodline.Get(host);
            if (hostGene != null)
            {
                total += hostGene.Level;
            }

            for (int i = 0; i < helpers.Count; i++)
            {
                Gene_ForestBloodline gene = helpers[i] == null ? null : Gene_ForestBloodline.Get(helpers[i]);
                if (gene != null)
                {
                    total += gene.Level;
                }
            }

            return total;
        }

        /// <summary>
        /// One validation pass. The window renders these numbers and the start press rejects with the
        /// first problem, so the panel can never offer a start that the altar then refuses.
        /// </summary>
        public RitualAssessment Assess(RitualRequest request)
        {
            var result = new RitualAssessment { Request = request };
            if (request?.Def == null)
            {
                result.Problems.Add("WotF_Ritual_NoCaster".Translate());
                return result;
            }

            RitualDef def = request.Def;
            result.RingSize = def.ringSize;
            result.ChalkColor = def.chalkColor;
            result.ManaCost = def.manaCost;
            result.DurationTicks = def.durationTicks;

            if (def.tier > Props.tier)
            {
                result.Problems.Add(string.Format("WotF_Ritual_TierRequired".Translate(), def.tier));
            }

            if (def.chooseRune)
            {
                if (request.Rune == null)
                {
                    result.Problems.Add("WotF_Ritual_ChooseRune".Translate());
                }
                else
                {
                    if (request.Rune.kind == SpellGlyphKind.Super)
                    {
                        result.Problems.Add("WotF_Ritual_SuperRuneNeedsRubbing".Translate());
                    }

                    if (request.Rune.tier > Props.tier)
                    {
                        result.Problems.Add(string.Format("WotF_Ritual_TierRequired".Translate(), request.Rune.tier));
                    }

                    if (GameComponent_Spellbook.IsUnlockedInData(request.Rune))
                    {
                        result.Problems.Add("WotF_Ritual_RuneAlreadyUnlocked".Translate(request.Rune.LabelCap));
                    }

                    RuneAwakeningTier tier = def.RuneTierFor(request.Rune.tier);
                    if (tier == null)
                    {
                        result.Problems.Add("WotF_Ritual_TierRequired".Translate(request.Rune.tier));
                    }
                    else
                    {
                        result.RingSize = tier.ringSize;
                        result.ChalkColor = tier.chalkColor;
                        result.ManaCost = tier.manaCost;
                        result.DurationTicks = tier.durationTicks;
                    }
                }
            }
            else if (request.Rune != null)
            {
                result.Problems.Add("WotF_Ritual_ChooseRune".Translate());
            }

            if (def.chooseTeleportTarget)
            {
                CompTeleportGate gate = request.TeleportTarget == null
                    ? null
                    : request.TeleportTarget.TryGetComp<CompTeleportGate>();
                if (gate == null || !gate.parent.Spawned || request.TeleportTarget.Map != parent.Map)
                {
                    result.Problems.Add("WotF_Ritual_Teleport_NoGate".Translate());
                }
                else if (!gate.Running)
                {
                    // A closed, unlinked or unpowered gate is not a destination (设定/05).
                    result.Problems.Add(gate.StateText());
                }
            }
            else if (request.TeleportTarget != null)
            {
                result.Problems.Add("WotF_Ritual_Teleport_NoGate".Translate());
            }

            if (IsActive)
            {
                result.Problems.Add("WotF_Ritual_AlreadyRunning".Translate(ActiveLabel));
            }

            if (!parent.Spawned)
            {
                result.Problems.Add("WotF_Ritual_CannotAct".Translate(parent.LabelShort));
                return result;
            }

            AssessRoster(request, result);
            AssessTeleportParty(request, result);
            AssessEquipment(request, result);
            AssessRuinSeek(request, result);
            AssessFormation(request, result);
            AssessCosts(def, result);
            return result;
        }

        /// <summary>
        /// Seeking only: one unclaimed ruin at a time, and the quest has to be able to find a site
        /// before the ring may start, because the mana is only drawn when the chant completes.
        /// </summary>
        private void AssessRuinSeek(RitualRequest request, RitualAssessment result)
        {
            RitualDef def = request.Def;
            if (!def.chooseRuinType)
            {
                if (request.RuinOption != null)
                {
                    result.Problems.Add("WotF_Ritual_RuinSeek_Choose".Translate());
                }

                return;
            }

            if (request.RuinOption == null)
            {
                result.Problems.Add("WotF_Ritual_RuinSeek_Choose".Translate());
            }
            else if (RuinSeeking.HasOutstandingRuin())
            {
                result.Problems.Add("WotF_Ritual_RuinSeek_AlreadyOutstanding".Translate());
            }
            else if (!RuinSeeking.CanSeek(request.RuinOption, parent.Map))
            {
                result.Problems.Add("WotF_Ritual_RuinSeek_NoSite".Translate());
            }
        }

        /// <summary>
        /// Inscription only: the item must lie inside the rite's radius and carry the enchantment comp,
        /// and the host's chosen page must be a form that equipment can hold.
        /// </summary>
        private void AssessEquipment(RitualRequest request, RitualAssessment result)
        {
            RitualDef def = request.Def;
            if (!def.chooseEquipment)
            {
                return;
            }

            if (request.Equipment == null || !IsEnchantableNearby(request.Equipment, def))
            {
                result.Problems.Add("WotF_Ritual_ChooseEquipment".Translate());
            }

            SpellRecipe recipe = request.Host == null || SpellService.OpenSlotCount(request.Host) == 0
                ? null
                : GameComponent_Spellbook.Current.GetSlot(request.Host, request.BookSlot);
            if (recipe == null)
            {
                result.Problems.Add((request.Host != null && SpellService.OpenSlotCount(request.Host) == 0
                    ? "WotF_Ritual_HostNoBook"
                    : "WotF_Ritual_ChooseBookSlot").Translate());
            }
            else if (!CompEnchantedEquipment.AcceptsRecipe(recipe, out string reason))
            {
                result.Problems.Add(reason);
            }
        }

        /// <summary>Weapons and armour within the rite's radius that can take an inscription.</summary>
        public List<Thing> EnchantableNearby(RitualDef def)
        {
            var found = new List<Thing>();
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, def.effectRadius, true))
            {
                if (IsEnchantableNearby(thing, def))
                {
                    found.Add(thing);
                }
            }

            return found;
        }

        private bool IsEnchantableNearby(Thing thing, RitualDef def)
        {
            return thing.Spawned && thing.Map == parent.Map
                && thing.TryGetComp<CompEnchantedEquipment>() != null
                && thing.Position.InHorDistOf(parent.Position, def.effectRadius);
        }

        private void AssessRoster(RitualRequest request, RitualAssessment result)
        {
            if (request.Host == null)
            {
                // The host is picked by hand, never defaulted: with someone eligible on the map the
                // missing piece is the choice, and only an empty roster means there is nobody to host.
                result.Problems.Add((HasEligibleHostCandidate() ? "WotF_Ritual_ChooseHost" : "WotF_Ritual_NoCaster").Translate());
            }
            else if (request.Host.Drafted)
            {
                result.Problems.Add("WotF_Ritual_Drafted".Translate());
            }
            else if (!IsEligibleRitualCaster(request.Host, out string hostReason))
            {
                result.Problems.Add(hostReason);
            }
            else if (HasRitualJob(request.Host, RitualJobDef) || HasRitualJob(request.Host, AssistJobDef))
            {
                result.Problems.Add("WotF_Ritual_Casting".Translate(request.Host.LabelShort));
            }

            var seen = new HashSet<Pawn>();
            if (request.Host != null)
            {
                seen.Add(request.Host);
            }

            for (int i = 0; i < request.Helpers.Count; i++)
            {
                Pawn helper = request.Helpers[i];
                if (helper == null)
                {
                    result.Problems.Add("WotF_Ritual_NoCaster".Translate());
                    continue;
                }

                if (!seen.Add(helper))
                {
                    result.Problems.Add("WotF_Ritual_HelperDuplicate".Translate(helper.LabelShort));
                    continue;
                }

                if (helper.Drafted)
                {
                    result.Problems.Add("WotF_Ritual_HelperDrafted".Translate(helper.LabelShort));
                }
                else if (!IsEligibleRitualCaster(helper, out string helperReason))
                {
                    result.Problems.Add(helperReason);
                }
                else if (HasRitualJob(helper, RitualJobDef) || HasRitualJob(helper, AssistJobDef))
                {
                    result.Problems.Add("WotF_Ritual_Casting".Translate(helper.LabelShort));
                }
            }

            result.BloodlinePower = PowerOf(request.Host, request.Helpers);
            result.FacilityPower = FacilityPower;
            result.Power = result.BloodlinePower + result.FacilityPower;
        }

        /// <summary>
        /// A teleport rite with nobody to carry cannot start. The party is whoever stands near the altar
        /// plus the host and helpers, who are still walking to the ring at assessment and will be on it
        /// when the chant ends; the completion check counts the same way once they are there.
        /// </summary>
        private void AssessTeleportParty(RitualRequest request, RitualAssessment result)
        {
            RitualDef def = request.Def;
            if (!def.chooseTeleportTarget)
            {
                return;
            }

            var standingBy = new List<Pawn> { request.Host };
            standingBy.AddRange(request.Helpers);
            if (TeleportGateTransfer.RitePassengers(parent.Map, parent.Position, def.RadiusFor(result.Power),
                    standingBy).Count == 0)
            {
                result.Problems.Add("WotF_Ritual_Teleport_NoPassengers".Translate());
            }
        }

        private void AssessFormation(RitualRequest request, RitualAssessment result)
        {
            RitualDef def = request.Def;
            result.MissingCells.Clear();
            result.MissingCells.AddRange(RitualRing.MissingCells(parent.Map, parent.Position,
                result.RingSize, result.ChalkColor));
            result.RingComplete = result.MissingCells.Count == 0;
            if (!result.RingComplete)
            {
                result.Problems.Add("WotF_Ritual_RingIncomplete".Translate(result.MissingCells.Count));
            }

            if (request.Helpers.Count < def.minParticipants)
            {
                result.Problems.Add("WotF_Ritual_NeedsParticipants".Translate(def.minParticipants, request.Helpers.Count));
            }

            result.Stations.Clear();
            result.Stations.AddRange(RitualFormation.AssignStations(parent.Map, parent.Position,
                result.RingSize, request.Host, request.Helpers, out string stationProblem));
            if (stationProblem != null)
            {
                result.Problems.Add(stationProblem);
            }
        }

        private void AssessCosts(RitualDef def, RitualAssessment result)
        {
            if (def.requiresDeepResources
                && !RitualEffect_LeylineMining.HasDeepResources(parent.Map, parent.Position, def.RadiusFor(result.Power)))
            {
                result.Problems.Add("WotF_Ritual_LeylineMining_NoResource".Translate());
            }

            ManaNetwork network = Network();
            result.ManaStored = network == null ? 0f : network.Stored;
            result.ManaEnough = network != null && result.ManaStored + 0.0001f >= result.ManaCost;
            if (!result.ManaEnough)
            {
                result.Problems.Add("WotF_Ritual_NoMana".Translate());
            }

            result.Offerings.Clear();
            result.MissingOfferings.Clear();
            PlanOfferings(def, result);
            if (result.MissingOfferings.Count > 0)
            {
                result.Problems.Add("WotF_Ritual_NoMaterials".Translate(string.Join(", ", result.MissingOfferings)));
            }
        }

        /// <summary>
        /// Splits the Def's cost lines into concrete stacks inside the effect radius, then asks the
        /// reservation manager how much of each is still free. Both the shortfall and the "somebody
        /// else reserved it" cases are reported here, so the start press never hits the
        /// TryMakePreToilReservations warning in Pawn_JobTracker.cs:937.
        /// </summary>
        private void PlanOfferings(RitualDef def, RitualAssessment result)
        {
            List<SpellGlyphCost> costs = RitualCosts(def, result.Request.Rune);
            if (costs.Count == 0)
            {
                return;
            }

            float radius = def.RadiusFor(result.Power);
            var things = new List<Thing>(GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, radius, true));
            var claimed = new Dictionary<Thing, int>();
            for (int i = 0; i < costs.Count; i++)
            {
                SpellGlyphCost cost = costs[i];
                int remaining = cost.count;
                for (int t = 0; t < things.Count && remaining > 0; t++)
                {
                    Thing thing = things[t];
                    if (!thing.Spawned || thing.Destroyed || !cost.Matches(thing))
                    {
                        continue;
                    }

                    int already = claimed.TryGetValue(thing, out int used) ? used : 0;
                    int freeHere = thing.stackCount - already;
                    if (freeHere <= 0)
                    {
                        continue;
                    }

                    int take = Math.Min(remaining, freeHere);
                    claimed[thing] = already + take;
                    result.Offerings.Add(new RitualOffering { Thing = thing, Count = take });
                    remaining -= take;
                }

                if (remaining > 0)
                {
                    result.MissingOfferings.Add(cost.Label + " x" + remaining);
                }
            }

            foreach (KeyValuePair<Thing, int> pair in claimed)
            {
                // 如果 request.Host 为空，则无法预留。此处若评估时无 host，应视作评估失败而非崩溃
                Pawn claimant = result.Request.Host;
                if (claimant == null)
                {
                    result.MissingOfferings.Add(pair.Key.def.label + " x" + pair.Value + " (no host for reservation)");
                    continue;
                }
                int unreserved = parent.Map.reservationManager.CanReserveStack(claimant, pair.Key, 1);
                if (unreserved < pair.Value)
                {
                    result.MissingOfferings.Add(pair.Key.def.label + " x" + pair.Value
                        + " (" + "WotF_Ritual_ReservedElsewhere".Translate() + ")");
                }
            }
        }

        private static List<SpellGlyphCost> RitualCosts(RitualDef def, SpellGlyphDef rune)
        {
            var costs = new List<SpellGlyphCost>();
            if (def.inputCosts != null)
            {
                for (int i = 0; i < def.inputCosts.Count; i++)
                {
                    ThingDefCountClass cost = def.inputCosts[i];
                    costs.Add(new SpellGlyphCost { thingDef = cost.thingDef, count = cost.count });
                }
            }

            if (rune?.unlockCost != null)
            {
                costs.AddRange(rune.unlockCost);
            }

            return costs;
        }

        private bool OfferingsPresent(out string missing)
        {
            var shortfalls = new List<string>();
            for (int i = 0; i < offerings.Count; i++)
            {
                RitualOffering portion = offerings[i];
                Thing thing = portion?.Thing;
                if (thing == null || thing.Destroyed || !thing.Spawned || thing.stackCount < portion.Count)
                {
                    shortfalls.Add((thing?.def?.label ?? "?") + " x" + (portion?.Count ?? 0));
                }
            }

            missing = string.Join(", ", shortfalls);
            return shortfalls.Count == 0;
        }

        private void ConsumeOfferings()
        {
            for (int i = 0; i < offerings.Count; i++)
            {
                RitualOffering portion = offerings[i];
                Thing thing = portion?.Thing;
                if (thing == null || thing.Destroyed)
                {
                    continue;
                }

                if (portion.Count >= thing.stackCount)
                {
                    thing.Destroy(DestroyMode.Vanish);
                }
                else
                {
                    thing.SplitOff(portion.Count).Destroy(DestroyMode.Vanish);
                }
            }
        }

        /// <summary>
        /// Ritual hosting follows the ritual rules, not the casting rules: 设定/04:24 and 设定/05:52
        /// require the forest bloodline from the host and nothing else, so a bloodline pawn without a
        /// spellbook can still host. The other conditions are being able to act, being player-controlled
        /// and not already casting. SpellService.IsEligibleCaster stays untouched: casting itself still needs the book.
        /// </summary>
        internal static bool IsEligibleRitualCaster(Pawn pawn, out string reason)
        {
            reason = null;
            if (pawn == null || pawn.Destroyed)
            {
                reason = "WotF_Ritual_NoCaster".Translate();
                return false;
            }

            if (pawn.Dead || !pawn.Spawned || pawn.Downed || pawn.InMentalState)
            {
                reason = "WotF_Ritual_CannotAct".Translate(pawn.LabelShort);
                return false;
            }

            if (!pawn.IsPlayerControlled)
            {
                reason = SpellText.T("WotF_Spells.Reason.NotPlayerControlled");
                return false;
            }

            if (Gene_ForestBloodline.Get(pawn) == null)
            {
                reason = "WotF_Ritual_NoBloodline".Translate(pawn.LabelShort);
                return false;
            }

            if (SpellCastingService.IsCasting(pawn))
            {
                reason = "WotF_Ritual_Casting".Translate(pawn.LabelShort);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Whether the host list of the ritual panel offers at least one pawn the player can pick -
        /// the same test the panel uses to enable a host row.
        /// </summary>
        private bool HasEligibleHostCandidate()
        {
            foreach (Pawn pawn in parent.Map.mapPawns.FreeColonistsSpawned)
            {
                if (IsEligibleRitualCaster(pawn, out _))
                {
                    return true;
                }
            }

            return false;
        }

        private Pawn BestCaster()
        {
            Pawn best = null;
            float bestDistance = float.MaxValue;
            foreach (Pawn pawn in parent.Map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn.Dead || pawn.Downed || pawn.Drafted || pawn.InMentalState)
                {
                    continue;
                }

                if (!IsEligibleRitualCaster(pawn, out _))
                {
                    continue;
                }

                if (HasRitualJob(pawn, RitualJobDef) || HasRitualJob(pawn, AssistJobDef))
                {
                    continue;
                }

                float distance = (pawn.Position - parent.Position).LengthHorizontalSquared;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = pawn;
                }
            }

            return best;
        }

        private static JobDef RitualJobDef => DefDatabase<JobDef>.GetNamed("WotF_PerformRitual");
        private static JobDef AssistJobDef => DefDatabase<JobDef>.GetNamed("WotF_AssistRitual");
    }
}
