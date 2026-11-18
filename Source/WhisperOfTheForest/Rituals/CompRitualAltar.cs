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
    /// The altar owns the active ritual: which Def is running, who hosts it and which chalk cell every
    /// participant stands on. It also owns its offerings: once a rite is prepared (the panel's
    /// [准备], or an auto-repeat rite) haulers carry the missing stacks into the altar's own container,
    /// the way vanilla fills a subcore scanner (WorkGiver_HaulToSubcoreScanner), and only what is inside
    /// counts. Nothing is paid when the rite starts - the network mana and the offerings are taken only
    /// when the chant completes, and an interrupted rite costs nothing (技术/04-仪式实现.md:116).
    /// Cancelling or switching the preparation drops the delivered offerings beside the altar.
    /// Hosting is a separate step: any rite whose Assess passes may start, and for a rite with offerings
    /// that means they are all inside, not that it is the prepared one.
    /// </summary>
    public class CompRitualAltar : ThingComp, IThingHolder
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
        private RitualDef collectDef;
        private SpellGlyphDef collectRune;
        private ThingOwner<Thing> offerings;

        // The player's order between TryStart and the host job's start; never saved (see Commit).
        private RitualRequest pendingOrder;
        private int pendingJobId = -1;

        public CompRitualAltar()
        {
            offerings = new ThingOwner<Thing>(this);
        }

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

        /// <summary>The rite whose offerings the altar is collecting; null when nothing is prepared.</summary>
        public RitualDef CollectDef => collectDef;

        public string CollectLabel => collectDef == null ? null
            : collectRune == null ? (string)collectDef.LabelCap : collectDef.LabelCap + ": " + collectRune.LabelCap;

        /// <summary>True while haulers should still bring something: a rite is prepared, none runs, a line is short.</summary>
        public bool NeedsOfferings => collectDef != null && !IsActive && parent.Spawned
            && HasMissing(Ledger(collectDef, collectRune));

        public bool IsPreparedFor(RitualDef def, SpellGlyphDef rune)
        {
            return def != null && collectDef == def && collectRune == rune;
        }

        /// <summary>Whether the container already holds every offering this rite takes; the work scan's cheap pre-check.</summary>
        public bool HasOfferingsFor(RitualDef def, SpellGlyphDef rune)
        {
            return !HasMissing(Ledger(def, rune));
        }

        /// <summary>How many of this stack the prepared rite still lacks; a hauler carries at most this.</summary>
        public int RequiredCountOf(Thing thing)
        {
            if (collectDef == null || IsActive)
            {
                return 0;
            }

            int required = 0;
            List<OfferingLine> lines = Ledger(collectDef, collectRune);
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Missing > 0 && lines[i].Cost.Matches(thing))
                {
                    required += lines[i].Missing;
                }
            }

            return required;
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return offerings;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, offerings);
        }

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

        /// <summary>
        /// Auto-repeat needs no [准备]: choosing the rite prepares it, and turning auto-repeat off stops the
        /// collection it started. A running rite is left alone; its completion settles the collection.
        /// </summary>
        public void SetAutoRitual(RitualDef def)
        {
            RitualDef previous = autoRitual;
            autoRitual = def != null && def.autoRepeatable ? def : null;
            if (IsActive || autoRitual == previous)
            {
                return;
            }

            if (autoRitual != null)
            {
                Prepare(autoRitual, null);
            }
            else if (IsPreparedFor(previous, null))
            {
                StopCollecting();
            }
        }

        /// <summary>Whether the panel's [准备] may collect for this rite: it has offerings and is not already collecting.</summary>
        public bool CanPrepare(RitualDef def, SpellGlyphDef rune)
        {
            return def != null && !IsActive && (!def.chooseRune || rune != null)
                && RitualCosts(def, rune).Count > 0 && !IsPreparedFor(def, rune);
        }

        /// <summary>
        /// Starts collecting this rite's offerings. Preparing a different rite than the one being collected
        /// is a switch: what was delivered for the old one drops beside the altar first.
        /// </summary>
        public void Prepare(RitualDef def, SpellGlyphDef rune)
        {
            if (IsPreparedFor(def, rune))
            {
                return;
            }

            if (collectDef != null)
            {
                StopCollecting();
            }

            collectDef = def;
            collectRune = rune;
            WotfLog.Info(WotfLog.ModuleRitual, "collecting offerings for " + def.defName
                + (rune == null ? "" : " / " + rune.defName) + " at " + parent.Position);
        }

        /// <summary>
        /// The panel's cancel. A running rite fed from the container ends together with its preparation;
        /// a rite without offerings only ends. Cancelling the auto-repeat rite's collection also turns
        /// auto-repeat off, or the next rare tick would prepare it again.
        /// </summary>
        public void CancelByPlayer()
        {
            bool fedFromContainer = !IsActive || IsPreparedFor(activeDef, activeRune);
            if (IsActive)
            {
                CancelRitual(null);
            }

            if (!fedFromContainer || collectDef == null)
            {
                return;
            }

            if (autoRitual == collectDef)
            {
                autoRitual = null;
            }

            StopCollecting();
        }

        private void StopCollecting()
        {
            RitualDef def = collectDef;
            collectDef = null;
            collectRune = null;
            int dropped = offerings.TotalStackCount;
            if (dropped > 0)
            {
                offerings.TryDropAll(parent.Position, parent.Map, ThingPlaceMode.Near);
            }

            WotfLog.Info(WotfLog.ModuleRitual, "stopped collecting " + def.defName + " at " + parent.Position
                + ", dropped " + dropped + " offerings");
        }

        /// <summary>
        /// Drops whatever the prepared rite does not count, all of it when nothing is prepared. A hauler
        /// that was already carrying a stack when the preparation was cancelled or switched still puts it
        /// in; this is where it comes back out.
        /// </summary>
        private void EjectSurplus()
        {
            if (offerings.Count == 0)
            {
                return;
            }

            var used = new Dictionary<Thing, int>();
            if (collectDef != null)
            {
                Ledger(collectDef, collectRune, used);
            }

            for (int i = offerings.Count - 1; i >= 0; i--)
            {
                Thing thing = offerings[i];
                int surplus = thing.stackCount - (used.TryGetValue(thing, out int kept) ? kept : 0);
                if (surplus > 0)
                {
                    offerings.TryDrop(thing, parent.Position, parent.Map, ThingPlaceMode.Near, surplus, out _);
                    WotfLog.Info(WotfLog.ModuleRitual, "ejected " + surplus + " " + thing.def.defName
                        + " the prepared rite does not use at " + parent.Position);
                }
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            // As CompThingContainer.PostDeSpawn: a demolished or destroyed altar leaves its offerings on the ground.
            if (mode != DestroyMode.WillReplace)
            {
                offerings.TryDropAll(parent.Position, map, ThingPlaceMode.Near);
                collectDef = null;
                collectRune = null;
            }
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
            stations ??= new List<RitualStation>();
            Scribe_Deep.Look(ref offerings, "ritualOfferings", this);
            // A save from before the container has no node, and Scribe_Deep then writes null (ScribeExtractor.cs:91-93).
            offerings ??= new ThingOwner<Thing>(this);
            Scribe_Defs.Look(ref collectDef, "ritualCollectDef");
            Scribe_Defs.Look(ref collectRune, "ritualCollectRune");

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

            // Save-load boundary: the performance only continues when the host, the rune and the target
            // are still there. Otherwise the rite ends here, with nothing spent, and the host's job fails
            // on its own because IsHost turns false. The offerings stay in the container.
            if (activeHost == null || activeHost.Dead || !activeHost.Spawned
                || (activeDef.chooseRune && activeRune == null)
                || (activeDef.chooseTeleportTarget && activeTeleportTarget == null)
                || (activeDef.chooseEquipment && (activeEquipment == null || activeBookSlot < 0))
                || (activeDef.chooseRuinType && ActiveRuinOption == null)
                || stations.Count != activeHelpers.Count + 1)
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

            if (autoRitual != null && collectDef == null)
            {
                Prepare(autoRitual, null);
            }

            EjectSurplus();
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
            if (!IsActive && collectDef != null)
            {
                text += "\n" + "WotF_Ritual_InspectCollecting".Translate(CollectLabel,
                    ProgressText(Ledger(collectDef, collectRune)));
            }
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

        /// <summary>
        /// The player's order (the panel's [开始仪式], Lab): validate with the chosen host, then give that
        /// pawn a forced host job. Nothing is written here; the rite starts in <see cref="Commit"/> when
        /// the job does, exactly like a rite a worker takes up from the work scan. True when the job was
        /// taken: running already, or queued behind a job the pawn cannot drop.
        /// </summary>
        public bool TryStart(RitualRequest request, bool silent = false, WorkGiver prioritizedBy = null)
        {
            RitualAssessment assessment = Assess(request);
            if (!assessment.CanStart)
            {
                Reject(silent, assessment.FirstProblem);
                return false;
            }

            Job hostJob = JobMaker.MakeJob(RitualJobDef, parent);
            pendingOrder = request;
            pendingJobId = hostJob.loadID;

            // TryTakeOrderedJob makes the pre-toil reservations (Pawn_JobTracker.cs:924): the altar
            // plus the equipment being inscribed. A false here means somebody else holds them.
            // A right-click goes through the vanilla prioritized-work order, like every other [优先 X].
            bool taken = prioritizedBy == null
                ? request.Host.jobs.TryTakeOrderedJob(hostJob, JobTag.Misc)
                : request.Host.jobs.TryTakeOrderedJobPrioritizedWork(hostJob, prioritizedBy, parent.Position);
            if (!taken)
            {
                pendingOrder = null;
                Reject(silent, "WotF_Ritual_AltarBusy".Translate());
                return false;
            }

            // An idle or interruptible pawn starts the job inside TryTakeOrderedJob, and a commit that
            // failed there has already ended it again.
            return IsHost(request.Host) || HasRitualJob(request.Host, RitualJobDef);
        }

        /// <summary>
        /// A rite hosted without the panel (work scan, right-click): this pawn alone, no helpers. The only
        /// choice it can reuse is the rune the preparation was made for; a rite that needs a gate, an item
        /// and page, or a ruin, or a rune nobody prepared, has none and returns null - the panel hosts it.
        /// </summary>
        public RitualRequest DirectRequest(RitualDef def, Pawn host)
        {
            if (def.chooseTeleportTarget || def.chooseEquipment || def.chooseRuinType
                || (def.chooseRune && collectDef != def))
            {
                return null;
            }

            return new RitualRequest { Def = def, Rune = def.chooseRune ? collectRune : null, Host = host };
        }

        /// <summary>
        /// The one start path, run by the host job when it actually starts (JobDriver_PerformRitual.
        /// Notify_Starting): a job from the work scan hosts the auto-repeat rite, a job from
        /// <see cref="TryStart(RitualRequest, bool)"/> (the panel's start, a right-click) hosts the order it was made for. The request is
        /// validated again here, because the pawn may have walked off, been queued or the altar changed
        /// since it was offered. False leaves the altar idle and the job ends with no toils.
        /// </summary>
        public bool Commit(Pawn host, Job job)
        {
            RitualRequest request;
            // The order goes first: a prioritized right-click job also carries workGiverDef.
            if (pendingOrder != null && pendingJobId == job.loadID && pendingOrder.Host == host)
            {
                request = pendingOrder;
            }
            else if (job.workGiverDef != null)
            {
                // WorkGiver_HostRitual offered this job in the same tick, so the auto rite is still set.
                request = DirectRequest(autoRitual, host);
            }
            else
            {
                // An order lives only between TryStart and the job's start and is not saved, so a queued
                // order that crossed a save/load has nothing left to start.
                WotfLog.Info(WotfLog.ModuleRitual, "host job of " + host.LabelShort + " at " + parent.Position
                    + " has no order to start");
                return false;
            }

            if (pendingJobId == job.loadID)
            {
                pendingOrder = null;
            }

            RitualAssessment assessment = Assess(request);
            if (!assessment.CanStart)
            {
                RejectCommit(job, request, assessment.FirstProblem);
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
            phase = RitualPhase.Gathering;
            activePower = assessment.Power;
            activeManaCost = assessment.ManaCost;
            activeRingSize = assessment.RingSize;
            activeChalkColor = assessment.ChalkColor;
            activeDurationTicks = assessment.DurationTicks;
            if (TryGetStation(activeHost, out IntVec3 hostCell))
            {
                job.targetB = hostCell;
            }

            // Helpers are only ever the ones the player picked in the panel, so they are ordered the same
            // way the host was; a worker taking the rite up as work hosts it alone.
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
                    for (int j = 0; j < helperJobs.Count; j++)
                    {
                        activeHelpers[j].jobs.EndCurrentOrQueuedJob(helperJobs[j], JobCondition.InterruptForced, false);
                    }

                    // The host job is still starting; with the state cleared its driver builds no toils.
                    ClearRitualState();
                    RejectCommit(job, request, "WotF_Ritual_HelperBusy".Translate(helper.LabelShort));
                    return false;
                }

                helperJobs.Add(helperJob);
            }

            WotfLog.Info(WotfLog.ModuleRitual,
                "started " + request.Def.defName + (request.Rune == null ? "" : " for " + request.Rune.defName)
                + " hosted by " + activeHost.LabelShort + " with " + activeHelpers.Count + " helpers at " + parent.Position
                + (job.workGiverDef == null ? " (ordered)" : " (work)"));
            return true;
        }

        /// <summary>A forced job tells the player why it did not start; a job from the work scan only logs.</summary>
        private void RejectCommit(Job job, RitualRequest request, string reason)
        {
            WotfLog.Info(WotfLog.ModuleRitual, "could not start " + request.Def.defName + " at " + parent.Position
                + " for " + request.Host.LabelShort + ": " + reason);
            Reject(!job.playerForced, reason);
        }

        /// <summary>
        /// Reserves the equipment being inscribed for the host's job so haulers leave it beside the altar.
        /// The offerings need none: they are inside the altar. Called by the job's pre-toils.
        /// </summary>
        public bool ReserveEquipment(Pawn claimant, Job job, bool errorOnFailed)
        {
            // Reservations are made before the job starts, so an ordered rite's item still sits in the order.
            Thing equipment = IsActive ? activeEquipment
                : pendingOrder != null && pendingJobId == job.loadID ? pendingOrder.Equipment : null;
            return equipment == null || claimant.Reserve(equipment, job, 1, -1, null, errorOnFailed);
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

            List<OfferingLine> ledger = Ledger(activeDef, activeRune);
            if (HasMissing(ledger))
            {
                FailRitual("WotF_Ritual_Complete_OfferingsGone".Translate(MissingText(ledger)));
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
            ConsumeOfferings(def, rune);
            // The collection this rite used is settled; an auto-repeat rite is prepared again on the next
            // rare tick and keeps whatever is left inside that it can count.
            if (IsPreparedFor(def, rune))
            {
                collectDef = null;
                collectRune = null;
            }

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

            // No check for the host already holding this altar's host job: Commit assesses while that very
            // job is starting, and a second order is refused by the altar's reservation instead.
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

            // Only what haulers have put into the altar counts, whichever rite it was prepared for.
            result.OfferingLines.Clear();
            SpellGlyphDef rune = result.Request.Rune;
            result.OfferingLines.AddRange(Ledger(def, rune));
            if (HasMissing(result.OfferingLines))
            {
                // The preparation only picks the wording: a rite nobody prepared lacks the step that fills the altar.
                result.Problems.Add(IsPreparedFor(def, rune)
                    ? "WotF_Ritual_NoMaterials".Translate(MissingText(result.OfferingLines))
                    : "WotF_Ritual_NotPrepared".Translate());
            }
        }

        /// <summary>
        /// Each cost line of the rite against the container, filled greedily in line order so a stack
        /// that matches two lines counts once. <paramref name="used"/> receives what each stack gives,
        /// which is exactly what completion consumes.
        /// </summary>
        private List<OfferingLine> Ledger(RitualDef def, SpellGlyphDef rune, Dictionary<Thing, int> used = null)
        {
            List<SpellGlyphCost> costs = RitualCosts(def, rune);
            var lines = new List<OfferingLine>(costs.Count);
            Dictionary<Thing, int> taken = used ?? new Dictionary<Thing, int>();
            for (int i = 0; i < costs.Count; i++)
            {
                SpellGlyphCost cost = costs[i];
                int delivered = 0;
                for (int t = 0; t < offerings.Count && delivered < cost.count; t++)
                {
                    Thing thing = offerings[t];
                    if (!cost.Matches(thing))
                    {
                        continue;
                    }

                    int already = taken.TryGetValue(thing, out int n) ? n : 0;
                    int take = Math.Min(cost.count - delivered, thing.stackCount - already);
                    if (take <= 0)
                    {
                        continue;
                    }

                    taken[thing] = already + take;
                    delivered += take;
                }

                lines.Add(new OfferingLine { Cost = cost, Delivered = delivered });
            }

            return lines;
        }

        private static bool HasMissing(List<OfferingLine> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Missing > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string MissingText(List<OfferingLine> lines)
        {
            var parts = new List<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Missing > 0)
                {
                    parts.Add(lines[i].Cost.Label + " x" + lines[i].Missing);
                }
            }

            return string.Join(", ", parts);
        }

        private static string ProgressText(List<OfferingLine> lines)
        {
            var parts = new List<string>(lines.Count);
            for (int i = 0; i < lines.Count; i++)
            {
                parts.Add(lines[i].Cost.Label + " " + lines[i].Delivered + "/" + lines[i].Cost.count);
            }

            return string.Join(", ", parts);
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

        private void ConsumeOfferings(RitualDef def, SpellGlyphDef rune)
        {
            var used = new Dictionary<Thing, int>();
            Ledger(def, rune, used);
            foreach (KeyValuePair<Thing, int> pair in used)
            {
                offerings.Take(pair.Key, pair.Value).Destroy(DestroyMode.Vanish);
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
