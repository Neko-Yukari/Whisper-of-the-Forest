using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Mana;

namespace WhisperOfTheForest.Teleport
{
    /// <summary>
    /// The connections a gate offers and the fees they charge (设定/05, 技术/04).
    ///
    /// Every move carries the contents as they are: passengers and cargo wait inside the gate's own
    /// <see cref="CompTransporter"/>, loaded by the vanilla loading job, by Dialog_LoadTransporters or
    /// by the transporter's own gizmos, and they arrive through <see cref="SkipUtility.SkipTo"/> —
    /// vanilla's lossless teleport primitive (SkipUtility.cs:10-22), which despawns, spawns, plays both
    /// skip effecters and re-drafts a pawn that was drafted. Nothing is dropped on the ground and no
    /// thing is re-created, so a stack of forty mithril arrives as the same stack of forty.
    ///
    /// A connection is all or nothing. Before any fee is drawn or anything leaves the container, every
    /// pawn and every item is given its own landing cell (<see cref="TryAssign"/>) among the cells a
    /// walker can reach from the gate (<see cref="LandingCells"/>), so nobody lands in a walled-off pocket
    /// or on an island; if one of them finds none the whole connection is refused with a message, and
    /// neither mana nor contents move. Fees come from the Def, never from code, and are drawn from the
    /// gate's own network in one all-or-nothing draw once the plan exists. The colony gate is never
    /// consumed. Once the plan exists the move has no refusal left: a thing the engine then fails to
    /// place throws (<see cref="Move"/>), and the ruin's broken gate destroys itself only after the whole
    /// move has returned.
    ///
    /// A first crossing is the one case that may not refuse: it is the crossing that generates the
    /// ruin's map, and a map whose site loses its only visitor is deleted by the next tick
    /// (Site.ShouldRemoveMapNow, Site.cs:385-412, then MapParent.CheckRemoveMapNow, MapParent.cs:315-326)
    /// together with the ruin. So a first crossing widens its search from the gate's radius to the whole
    /// map, says so in the log and to the player, and only gives up when the whole map has no reachable
    /// free cell — which is a defect, and is logged as one.
    /// </summary>
    public static class TeleportGateTransfer
    {
        private const string ConnectingKey = "WotF_TeleportGate_Connecting";

        public static int PawnCount(CompTransporter transporter)
        {
            int count = 0;
            foreach (Thing thing in transporter.GetDirectlyHeldThings())
            {
                if (thing is Pawn)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Cargo only: pawns are charged per head, so their own mass is not counted twice.</summary>
        public static float CargoMass(CompTransporter transporter)
        {
            return CollectionsMassCalculator.MassUsage(transporter.GetDirectlyHeldThings(),
                IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload, includePawnsMass: false);
        }

        public static bool IsEmpty(CompTransporter transporter)
        {
            return transporter == null || transporter.GetDirectlyHeldThings().Count == 0;
        }

        public static float RemoteCost(CompTeleportGate gate, CompTransporter transporter)
        {
            CompProperties_TeleportGate props = gate.Props;
            return props.remoteStartCost
                   + PawnCount(transporter) * props.remoteCostPerPawn
                   + CargoMass(transporter) * props.remoteCostPerKg;
        }

        /// <summary>
        /// The crossing home is charged exactly like the crossing out — the same three figures, read from
        /// the home gate's own comp (设定/05) — and paid by the home gate's network, the only network on
        /// this side of the connection. The ruin gate carries no fees: it is off the network.
        /// The menu and the button both ask this one function.
        /// </summary>
        public static bool CanPayReturn(CompTeleportGate home, CompTransporter transporter, out float cost)
        {
            cost = home == null || transporter == null ? 0f : RemoteCost(home, transporter);
            ManaNetwork network = home?.Network;
            return network != null && network.Stored + 0.0001f >= cost;
        }

        /// <summary>Every gate standing on this map, colony or ruin.</summary>
        public static IEnumerable<CompTeleportGate> GatesOn(Map map)
        {
            foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Transporter))
            {
                CompTeleportGate gate = thing.TryGetComp<CompTeleportGate>();
                if (gate != null)
                {
                    yield return gate;
                }
            }
        }

        /// <summary>The ruin's own gate: the broken one its map generated.</summary>
        public static CompTeleportGate FindRuinGate(Map map)
        {
            foreach (CompTeleportGate gate in GatesOn(map))
            {
                if (gate.Ruined)
                {
                    return gate;
                }
            }

            return null;
        }

        /// <summary>
        /// Whether any hostile active threat remains on the map, counting dormant pawns as hostile —
        /// the exact same clear check vanilla uses for sites (Site.cs:324-327).
        /// </summary>
        public static bool HasActiveHostiles(Map map)
        {
            return map != null && GenHostility.AnyHostileActiveThreatToPlayer(map, countDormantPawnsAsHostile: true);
        }

        /// <summary>
        /// A ruin's broken gate stays locked until every active or dormant hostile on its map is
        /// cleared; non-ruined gates are unaffected.
        /// </summary>
        public static bool CanReturnFromRuin(bool ruined, bool hasActiveHostiles) => !ruined || !hasActiveHostiles;

        /// <summary>Sites whose parts carry <see cref="RuinSiteExtension"/>.</summary>
        public static IEnumerable<Site> RuinSites()
        {
            foreach (WorldObject worldObject in Find.WorldObjects.AllWorldObjects)
            {
                Site site = worldObject as Site;
                if (site != null && RuinSiteExtension.Reachable(site))
                {
                    yield return site;
                }
            }
        }

        /// <summary>The colony's open gates: the only valid far end of a connection.</summary>
        public static IEnumerable<CompTeleportGate> WorkingHomeGates()
        {
            foreach (Map map in Find.Maps)
            {
                if (!map.IsPlayerHome)
                {
                    continue;
                }

                foreach (CompTeleportGate gate in GatesOn(map))
                {
                    if (gate.CanAcceptRemote)
                    {
                        yield return gate;
                    }
                }
            }
        }

        public static List<FloatMenuOption> Options(CompTeleportGate gate)
        {
            var options = new List<FloatMenuOption>();
            if (!gate.parent.Spawned)
            {
                return options;
            }

            if (gate.Ruined && !CanReturnFromRuin(true, HasActiveHostiles(gate.parent.Map)))
            {
                options.Add(Disabled((string)"WotF_TeleportGate_RuinHostiles".Translate()));
                return options;
            }

            CompTransporter transporter = gate.Transporter;
            if (transporter == null)
            {
                options.Add(Disabled((string)"WotF_TeleportGate_NoTransporter".Translate()));
                return options;
            }

            if (IsEmpty(transporter))
            {
                options.Add(Disabled((string)"WotF_TeleportGate_Empty".Translate()));
                return options;
            }

            if (gate.Ruined)
            {
                // The crossing home costs what the crossing out costs, paid by the home gate's network.
                // A home gate that cannot pay is offered greyed out with the walk-back hint; Return()
                // makes the same judgement again when it actually runs.
                foreach (CompTeleportGate home in WorkingHomeGates())
                {
                    CompTeleportGate destination = home;
                    bool canPay = CanPayReturn(destination, transporter, out float fee);
                    string label = (string)"WotF_TeleportGate_ReturnTo".Translate(HomeLabel(destination),
                        fee.ToString("0.#"));
                    if (!canPay)
                    {
                        label += " " + (string)"WotF_TeleportGate_ReturnNeeds".Translate(fee.ToString("0.#"),
                            HomeLabel(destination),
                            (destination.Network?.Stored ?? 0f).ToString("0.#"));
                    }

                    options.Add(new FloatMenuOption(label,
                        canPay ? (Action)delegate { Return(gate, destination); } : null));
                }

                if (options.Count == 0)
                {
                    options.Add(Disabled((string)"WotF_TeleportGate_NoHomeGate".Translate()));
                }

                return options;
            }

            if (!gate.Running)
            {
                options.Add(Disabled(gate.StateText()));
                return options;
            }

            // 基地内部传送: gate to gate on the same map, no fee (设定/05).
            foreach (CompTeleportGate other in GatesOn(gate.parent.Map))
            {
                if (other == gate || !other.CanAcceptRemote)
                {
                    continue;
                }

                CompTeleportGate destination = other;
                options.Add(new FloatMenuOption(
                    (string)"WotF_TeleportGate_SendLocal".Translate(HomeLabel(destination)),
                    delegate { SendLocal(gate, destination); }));
            }

            float cost = RemoteCost(gate, transporter);
            ManaNetwork network = gate.Network;
            float stored = network == null ? 0f : network.Stored;
            bool affordable = network != null && stored + 0.0001f >= cost;
            bool canLead = AnyNonDownedColonist(transporter);
            foreach (Site site in RuinSites())
            {
                Site destination = site;

                // A first crossing generates the map, and a generated map with nobody on it is deleted
                // with its site on the next tick, so vanilla's own visit rule applies to it as well
                // (TransportersArrivalAction_VisitSite.cs:76-91).
                bool needsParty = !destination.HasMap && !canLead;
                string label = (string)"WotF_TeleportGate_SendTo".Translate(destination.Label, cost.ToString("0.#"));
                if (!affordable)
                {
                    label += " — " + (string)"WotF_TeleportGate_ManaShort".Translate(cost.ToString("0.#"), stored.ToString("0.#"));
                }
                else if (needsParty)
                {
                    label += " — " + (string)"WotF_TeleportGate_NeedsParty".Translate();
                }

                options.Add(new FloatMenuOption(label,
                    affordable && !needsParty ? (Action)delegate { Send(gate, destination); } : null));
            }

            if (options.Count == 0)
            {
                options.Add(Disabled((string)"WotF_TeleportGate_NoRuin".Translate()));
            }

            return options;
        }

        /// <summary>Colony gate to a ruin: the long trip. Map generation runs as a long event.</summary>
        public static void Send(CompTeleportGate gate, Site site)
        {
            if (site == null)
            {
                Message((string)"WotF_TeleportGate_NoRuin".Translate());
                return;
            }

            LongEventHandler.QueueLongEvent(delegate { SendNow(gate, site); }, ConnectingKey, false, null);
        }

        private static void SendNow(CompTeleportGate gate, Site site)
        {
            if (!gate.parent.Spawned || site == null || !site.Spawned)
            {
                Message((string)"WotF_TeleportGate_Gone".Translate());
                return;
            }

            // The event runs after the click, not during it: the gate may have been flicked off or lost
            // its mana in between, and a gate that is not running is not a gate that may send.
            if (!gate.Running)
            {
                Message((string)"WotF_TeleportGate_CannotConnect".Translate(gate.parent.LabelCap, gate.StateText()));
                return;
            }

            CompTransporter transporter = gate.Transporter;
            if (IsEmpty(transporter))
            {
                Message((string)"WotF_TeleportGate_Empty".Translate());
                return;
            }

            bool firstVisit = !site.HasMap;

            // A map with nobody on it is deleted by the next tick and takes the site with it, so a
            // crossing that generates the map has to be able to land at least one colonist.
            if (firstVisit && !AnyNonDownedColonist(transporter))
            {
                Message((string)"WotF_TeleportGate_NeedsParty".Translate());
                return;
            }

            // Affordability is settled before the map exists, so a party that cannot pay does not leave
            // a freshly generated ruin behind. The authoritative draw comes after the landing plan.
            float cost = RemoteCost(gate, transporter);
            ManaNetwork network = gate.Network;
            if (network == null || network.Stored + 0.0001f < cost)
            {
                Message((string)"WotF_TeleportGate_ManaShort".Translate(cost.ToString("0.#"),
                    (network == null ? 0f : network.Stored).ToString("0.#")));
                return;
            }

            Map destination = firstVisit ? GenerateRuinMap(site) : site.Map;
            if (destination == null)
            {
                Message((string)"WotF_TeleportGate_Failed".Translate(site.Label));
                return;
            }

            CompTeleportGate anchor = FindRuinGate(destination);
            if (anchor == null)
            {
                // The ruin's own gen step reports the defect; nothing here can stand in for the gate.
                WotfLog.Error(WotfLog.ModuleTeleport, "the ruin at tile " + site.Tile.tileId
                    + " has no broken gate; the connection was refused");
                Message((string)"WotF_TeleportGate_NoAnchor".Translate(site.Label));
                return;
            }

            if (!TryPlanArrival(transporter, anchor, firstVisit, out Dictionary<Thing, IntVec3> plan,
                    out int unplaced, out bool widened))
            {
                WotfLog.Error(WotfLog.ModuleTeleport, "the ruin at tile " + site.Tile.tileId
                    + " has no reachable free cell for " + unplaced + " of the " + (plan.Count + unplaced)
                    + " things in gate " + gate.parent.thingIDNumber + "; the connection was refused");
                Message((string)"WotF_TeleportGate_NoLanding".Translate(site.Label, unplaced));
                return;
            }
            if (!network.TryDraw(cost))
            {
                WotfLog.Error(WotfLog.ModuleTeleport, "gate " + gate.parent.thingIDNumber + " passed the"
                    + " affordability check but could not pay " + cost.ToString("0.#") + " mana");
                Message((string)"WotF_TeleportGate_ManaShort".Translate(cost.ToString("0.#"),
                    network.Stored.ToString("0.#")));
                return;
            }

            if (widened)
            {
                int reach = plan.Count == 0
                    ? 0
                    : Mathf.CeilToInt(plan.Values.Max(cell => cell.DistanceTo(anchor.parent.Position)));
                WotfLog.Info(WotfLog.ModuleTeleport, "nothing fit beside the broken gate at tile "
                    + site.Tile.tileId + ", so the party landed on the nearest free ground; the farthest"
                    + " landing is " + reach + " cells from the gate");
                Messages.Message((string)"WotF_TeleportGate_Widened".Translate(anchor.parent.LabelCap, reach),
                    anchor.parent, MessageTypeDefOf.CautionInput, false);
            }

            Map source = gate.parent.Map;
            List<Thing> party = plan.Keys.ToList();
            int pawnCount = PawnCountOf(party);
            float cargoMass = CargoMassOf(party);
            Move(transporter, plan, destination);

            // The party just went there, so the view follows them unless the player was looking elsewhere.
            if (Find.CurrentMap == source)
            {
                Current.Game.CurrentMap = destination;
                CameraJumper.TryHideWorld();
            }

            if (firstVisit)
            {
                SendArrivalLetter(site, destination, anchor.parent);
            }

            CameraJumper.TryJump(anchor.parent);
            Messages.Message(
                (string)"WotF_TeleportGate_Sent".Translate(pawnCount,
                    cargoMass.ToString("0.#"), cost.ToString("0.#"), site.Label),
                anchor.parent, MessageTypeDefOf.PositiveEvent, false);
            WotfLog.Info(WotfLog.ModuleTeleport, "gate " + gate.parent.thingIDNumber + " sent " + party.Count
                + " things (" + pawnCount + " pawns, " + cargoMass.ToString("0.#")
                + " kg) to the ruin at tile " + site.Tile.tileId + " for " + cost.ToString("0.#") + " mana");
        }

        /// <summary>Gate to gate on one map: no start fee, no per-head or per-kilo fee (设定/05).</summary>
        public static void SendLocal(CompTeleportGate from, CompTeleportGate to)
        {
            CompTransporter transporter = from.Transporter;
            if (!from.parent.Spawned || to == null || !to.parent.Spawned || to.parent.Map != from.parent.Map)
            {
                Message((string)"WotF_TeleportGate_Gone".Translate());
                return;
            }

            // The menu was built a moment ago; either end may have closed since.
            if (!from.Running)
            {
                Message((string)"WotF_TeleportGate_CannotConnect".Translate(from.parent.LabelCap, from.StateText()));
                return;
            }

            if (!to.CanAcceptRemote)
            {
                Message((string)"WotF_TeleportGate_CannotConnect".Translate(to.parent.LabelCap, to.StateText()));
                return;
            }

            if (IsEmpty(transporter))
            {
                Message((string)"WotF_TeleportGate_Empty".Translate());
                return;
            }

            // The destination map is already standing, so this one may still refuse.
            if (!TryPlanArrival(transporter, to, false, out Dictionary<Thing, IntVec3> plan, out int unplaced,
                    out _))
            {
                RefuseNoRoom(to, unplaced, plan.Count + unplaced);
                return;
            }

            Move(transporter, plan, to.parent.Map);
            CameraJumper.TryJump(to.parent);
            Messages.Message((string)"WotF_TeleportGate_SentLocalDone".Translate(plan.Count, to.parent.LabelCap),
                to.parent, MessageTypeDefOf.PositiveEvent, false);
        }

        /// <summary>The ruin gate's one connection home, charged like the crossing out, then it destroys itself.</summary>
        public static void Return(CompTeleportGate ruinGate, CompTeleportGate home)
        {
            Return(ruinGate, home, out _, out _);
        }

        /// <summary>
        /// As above, and says why the crossing was refused in the words the player sees — null when it went
        /// through — together with the fee that was charged. The menu greys its option out for the same
        /// reasons, so one function decides for both.
        /// </summary>
        public static void Return(CompTeleportGate ruinGate, CompTeleportGate home, out string refusal,
            out float cost)
        {
            refusal = null;
            cost = 0f;
            CompTransporter transporter = ruinGate.Transporter;

            if (!ruinGate.parent.Spawned)
            {
                refusal = (string)"WotF_TeleportGate_NoHomeGate".Translate();
                Message(refusal);
                return;
            }

            if (!CanReturnFromRuin(ruinGate.Ruined, HasActiveHostiles(ruinGate.parent.Map)))
            {
                refusal = (string)"WotF_TeleportGate_RuinHostiles".Translate();
                Message(refusal);
                return;
            }

            // CanAcceptRemote is the run-time check: spawned, switched on and paid this tick.
            if (home == null || !home.CanAcceptRemote)
            {
                refusal = (string)"WotF_TeleportGate_NoHomeGate".Translate();
                Message(refusal);
                return;
            }

            if (IsEmpty(transporter))
            {
                refusal = (string)"WotF_TeleportGate_Empty".Translate();
                Message(refusal);
                return;
            }

            // Order as asked: the home gate is running (above), then the landing is planned, then the fee is
            // drawn from the home gate's network, then the contents move, and only then does the gate break.
            if (!TryPlanArrival(transporter, home, false, out Dictionary<Thing, IntVec3> plan, out int unplaced,
                    out _))
            {
                refusal = RefuseNoRoom(home, unplaced, plan.Count + unplaced);
                return;
            }

            ManaNetwork network = home.Network;
            cost = RemoteCost(home, transporter);
            if (network == null || !network.TryDraw(cost))
            {
                WotfLog.Info(WotfLog.ModuleTeleport, "the crossing home to gate " + home.parent.thingIDNumber
                    + " could not pay " + cost.ToString("0.#") + " mana; nothing was moved");
                refusal = (string)"WotF_TeleportGate_ReturnCost".Translate(cost.ToString("0.#"),
                    HomeLabel(home), (network == null ? 0f : network.Stored).ToString("0.#"));
                Message(refusal);
                return;
            }

            Map ruinMap = ruinGate.parent.Map;
            Move(transporter, plan, home.parent.Map);

            // Move returns only when everything crossed (it throws otherwise), so the container is empty
            // and nothing is lost with the gate.
            ruinGate.parent.Destroy(DestroyMode.KillFinalize);

            if (Find.CurrentMap == ruinMap)
            {
                Current.Game.CurrentMap = home.parent.Map;
                CameraJumper.TryHideWorld();
            }

            CameraJumper.TryJump(home.parent);
            Messages.Message(
                (string)"WotF_TeleportGate_Returned".Translate(plan.Count, HomeLabel(home), cost.ToString("0.#")),
                home.parent, MessageTypeDefOf.PositiveEvent, false);
            WotfLog.Info(WotfLog.ModuleTeleport, "the ruin gate at tile " + ruinMap.Tile.tileId
                + " sent " + plan.Count + " things home to gate " + home.parent.thingIDNumber + " for "
                + cost.ToString("0.#") + " mana and broke");
        }

        /// <summary>
        /// The colonists a teleport rite carries: player-controlled, within <paramref name="radius"/> of
        /// the altar. Shared by the altar's pre-payment check and by the effect, so both decide on the
        /// same party. <paramref name="standingByForRing"/> names pawns that are not on the ring yet but
        /// will be when the chant ends (the host and the helpers): the assessment passes them, so a host
        /// who is still walking over is not mistaken for an empty party.
        /// </summary>
        public static List<Pawn> RitePassengers(Map map, IntVec3 altar, float radius,
            IEnumerable<Pawn> standingByForRing = null)
        {
            var passengers = new List<Pawn>();
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.IsColonistPlayerControlled && pawn.Position.InHorDistOf(altar, radius))
                {
                    passengers.Add(pawn);
                }
            }

            if (standingByForRing != null)
            {
                foreach (Pawn pawn in standingByForRing)
                {
                    if (pawn != null && pawn.IsColonistPlayerControlled && !passengers.Contains(pawn))
                    {
                        passengers.Add(pawn);
                    }
                }
            }

            return passengers;
        }

        /// <summary>
        /// Landing cells for the local teleport rite, inside the gate's <c>riteArrivalRadius</c> of the
        /// footprint — the same zone the rite publishes as its arrival area. Nothing is decided here that
        /// the caller cannot check before it charges. It answers false when there is nobody to carry, or
        /// when someone finds no cell, and the rite must then fail without spending anything.
        /// </summary>
        public static bool TryPlanRiteLanding(CompTeleportGate gate, List<Pawn> passengers,
            out Dictionary<Pawn, IntVec3> plan, out int unplaced)
        {
            plan = new Dictionary<Pawn, IntVec3>();
            unplaced = passengers.Count;
            if (passengers.Count == 0)
            {
                return false;
            }

            Map map = gate.parent.Map;
            List<IntVec3> cells = LandingCells(gate, RiteZone(gate, map));
            List<Thing> things = passengers.Cast<Thing>().ToList();
            if (!TryAssign(cells, things, map, out Dictionary<Thing, IntVec3> assigned, out unplaced))
            {
                return false;
            }

            foreach (KeyValuePair<Thing, IntVec3> step in assigned)
            {
                plan[(Pawn)step.Key] = step.Value;
            }

            return true;
        }

        /// <summary>
        /// Every cell within <c>riteArrivalRadius</c> of a cell the gate's footprint touches, which is the
        /// reach the rite promises and the zone its arrival check measures.
        /// </summary>
        private static IEnumerable<IntVec3> RiteZone(CompTeleportGate gate, Map map)
        {
            int radius = gate.Props.riteArrivalRadius;
            var seen = new HashSet<IntVec3>();
            foreach (IntVec3 root in GenAdj.CellsAdjacent8Way(gate.parent))
            {
                if (!root.InBounds(map) || !root.Standable(map))
                {
                    continue;
                }

                foreach (IntVec3 cell in GenRadial.RadialCellsAround(root, radius, true))
                {
                    if (seen.Add(cell))
                    {
                        yield return cell;
                    }
                }
            }
        }

        /// <summary>
        /// Gives every thing in the container its own landing cell. A pawn needs a standable cell with no
        /// pawn on it, anything else a standable cell with no item on it, and no two things share a cell —
        /// otherwise vanilla's spawn would shove or destroy what was already lying there
        /// (GenSpawn.cs:152-166). Pawns choose first because theirs is the stricter rule.
        ///
        /// The search starts at the destination gate's <c>arrivalRadius</c>. A first crossing into a ruin
        /// may widen it to the whole map, because a map generated with nobody on it is deleted by the next
        /// tick (Site.cs:385-412). Anything but a first crossing keeps the radius and may refuse. Either way
        /// a candidate has to be reachable on foot from the gate (<see cref="LandingCells"/>).
        /// </summary>
        private static bool TryPlanArrival(CompTransporter transporter, CompTeleportGate anchor, bool widen,
            out Dictionary<Thing, IntVec3> plan, out int unplaced, out bool widened)
        {
            Map map = anchor.parent.Map;
            List<Thing> things = PawnFirst(transporter);
            widened = false;
            List<IntVec3> cells = LandingCells(anchor,
                GenRadial.RadialCellsAround(anchor.parent.Position, anchor.Props.arrivalRadius, true)
                    .InRandomOrder());
            if (TryAssign(cells, things, map, out plan, out unplaced))
            {
                return true;
            }

            if (!widen)
            {
                return false;
            }

            // Nearest to the gate first, so the widened landing stays as close to the gate as the map allows.
            widened = true;
            IntVec3 center = anchor.parent.Position;
            cells = LandingCells(anchor, map.AllCells);
            cells.Sort((a, b) => a.DistanceToSquared(center).CompareTo(b.DistanceToSquared(center)));
            return TryAssign(cells, things, map, out plan, out unplaced);
        }

        /// <summary>
        /// The cells among <paramref name="candidates"/> where something may land beside the gate: in
        /// bounds, standable, outside the gate's own footprint, and reachable on foot from the gate. This
        /// is the one place that decides it, from one root, for every plan (the three connections and the
        /// rite): a ruin's temple is walled in, and a cell in a sealed pocket or on an island within the
        /// radius is no place to put a person. A gate with no standable cell around it has nowhere to land
        /// anything, so every plan against it fails.
        /// </summary>
        private static List<IntVec3> LandingCells(CompTeleportGate gate, IEnumerable<IntVec3> candidates)
        {
            Map map = gate.parent.Map;
            var cells = new List<IntVec3>();
            if (!TryFindReachRoot(gate, out IntVec3 root))
            {
                return cells;
            }

            CellRect footprint = gate.parent.OccupiedRect();
            TraverseParms walker = TraverseParms.For(TraverseMode.PassDoors);
            foreach (IntVec3 cell in candidates)
            {
                if (!cell.InBounds(map) || footprint.Contains(cell) || !cell.Standable(map))
                {
                    continue;
                }

                if (cell != root && !map.reachability.CanReach(root, cell, PathEndMode.OnCell, walker))
                {
                    continue;
                }

                cells.Add(cell);
            }

            return cells;
        }

        /// <summary>The first standable cell around the gate's footprint: where reachability is measured from.</summary>
        private static bool TryFindReachRoot(CompTeleportGate gate, out IntVec3 root)
        {
            Map map = gate.parent.Map;
            foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(gate.parent))
            {
                if (cell.InBounds(map) && cell.Standable(map))
                {
                    root = cell;
                    return true;
                }
            }

            root = IntVec3.Invalid;
            return false;
        }

        private static List<Thing> PawnFirst(CompTransporter transporter)
        {
            return transporter.GetDirectlyHeldThings().OrderBy(thing => thing is Pawn ? 0 : 1).ToList();
        }

        /// <summary>
        /// One cell per thing. A failed plan reports how many things found no cell; nothing has moved yet,
        /// so the caller can still refuse without paying.
        /// </summary>
        private static bool TryAssign(List<IntVec3> cells, List<Thing> things, Map map,
            out Dictionary<Thing, IntVec3> plan, out int unplaced)
        {
            plan = new Dictionary<Thing, IntVec3>(things.Count);
            var taken = new HashSet<IntVec3>();
            unplaced = 0;
            foreach (Thing thing in things)
            {
                bool pawn = thing is Pawn;
                IntVec3 chosen = IntVec3.Invalid;
                foreach (IntVec3 cell in cells)
                {
                    if (taken.Contains(cell))
                    {
                        continue;
                    }

                    if (pawn ? cell.GetFirstPawn(map) != null : cell.GetItemCount(map) != 0)
                    {
                        continue;
                    }

                    chosen = cell;
                    break;
                }

                if (!chosen.IsValid)
                {
                    unplaced++;
                    continue;
                }

                taken.Add(chosen);
                plan[thing] = chosen;
            }

            return unplaced == 0;
        }

        /// <summary>The refusal the player sees when no landing cell is free; returned so callers can report it.</summary>
        private static string RefuseNoRoom(CompTeleportGate anchor, int unplaced, int total)
        {
            WotfLog.Info(WotfLog.ModuleTeleport, "connection to " + anchor.parent.LabelCap + " refused: "
                + unplaced + " of " + total + " things have no free cell within "
                + anchor.Props.arrivalRadius.ToString("0.#"));
            string text = (string)"WotF_TeleportGate_NoRoom".Translate(anchor.parent.LabelCap, unplaced, total,
                anchor.Props.arrivalRadius.ToString("0.#"));
            Message(text);
            return text;
        }

        /// <summary>
        /// Hands the planned things to <see cref="SkipUtility.SkipTo"/>, one at a time. The plan gave every
        /// thing a free, standable, reachable cell, so a thing that still comes back null is an engine
        /// failure and not an outcome to work around: it throws, naming the thing and the cell, and vanilla
        /// logs it. Nothing here catches it, puts the thing back or carries on with the rest.
        /// </summary>
        private static void Move(CompTransporter transporter, Dictionary<Thing, IntVec3> plan, Map destination)
        {
            ThingOwner owner = transporter.GetDirectlyHeldThings();
            foreach (KeyValuePair<Thing, IntVec3> step in plan)
            {
                owner.Remove(step.Key);
                if (SkipUtility.SkipTo(step.Key, step.Value, destination) == null)
                {
                    throw new InvalidOperationException("gate transfer: " + step.Key.ToStringSafe()
                        + " could not be placed at " + step.Value + " on " + destination);
                }
            }
        }

        private static int PawnCountOf(List<Thing> things)
        {
            int count = 0;
            foreach (Thing thing in things)
            {
                if (thing is Pawn)
                {
                    count++;
                }
            }

            return count;
        }

        private static float CargoMassOf(List<Thing> things)
        {
            return CollectionsMassCalculator.MassUsage(things,
                IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload, includePawnsMass: false);
        }

        private static bool AnyNonDownedColonist(CompTransporter transporter)
        {
            foreach (Thing thing in transporter.GetDirectlyHeldThings())
            {
                if (thing is Pawn pawn && pawn.IsColonist && !pawn.Downed)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Generates the ruin's map the way a transporter arrival does (TransportersArrivalAction_
        /// VisitSite.cs:54-60): generate, then tell the tick manager and the player's relations about the
        /// new map. The arrival letter waits until the party is actually standing there.
        /// </summary>
        private static Map GenerateRuinMap(Site site)
        {
            Map map = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, site.PreferredMapSize, null);
            if (map == null)
            {
                WotfLog.Error(WotfLog.ModuleTeleport, "the ruin at tile " + site.Tile.tileId
                    + " generated no map; the connection was refused");
                return null;
            }

            Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
            PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter_Send(map.mapPawns.AllPawns,
                "LetterRelatedPawnsInMapWherePlayerLanded".Translate(Faction.OfPlayer.def.pawnsPlural),
                LetterDefOf.NeutralEvent, informEvenIfSeenBefore: true);
            return map;
        }

        /// <summary>
        /// The letter a caravan's first entry sends (CaravanArrivalAction_VisitSite.cs:68-70): the
        /// arrival sentence, then each site part's own arrival text and letter def, the same walk as the
        /// vanilla AppendThreatInfo (:118-156). The "entering counts as an attack" block (:72-75) is not
        /// carried over: the ruin does not set considerEnteringAsAttack.
        /// </summary>
        private static void SendArrivalLetter(Site site, Map map, Thing fallbackTarget)
        {
            var text = new StringBuilder();
            text.Append(((string)"LetterCaravanEnteredMap".Translate(
                (string)"WotF_TeleportGate_PartyLabel".Translate(), site)).CapitalizeFirst());

            LetterDef letterDef = null;
            var targets = new List<GlobalTargetInfo>();
            var used = new HashSet<SitePartDef>();
            foreach (SitePart part in site.parts)
            {
                string partText = part.def.Worker.GetArrivedLetterPart(map, out LetterDef preferred, out LookTargets partTargets);
                if (partText == null)
                {
                    continue;
                }

                if (used.Add(part.def))
                {
                    if (text.Length > 0)
                    {
                        text.AppendLine();
                        text.AppendLine();
                    }

                    text.Append(partText);
                }

                if (letterDef == null)
                {
                    letterDef = preferred;
                }

                if (partTargets.IsValid())
                {
                    targets.AddRange(partTargets.targets);
                }
            }

            LookTargets look = targets.Count > 0 ? new LookTargets(targets) : new LookTargets(fallbackTarget);
            Find.LetterStack.ReceiveLetter(LetterMaker.MakeLetter("LetterLabelCaravanEnteredMap".Translate(site),
                text.ToString(), letterDef ?? LetterDefOf.NeutralEvent, look));
        }

        private static string HomeLabel(CompTeleportGate gate)
        {
            Map map = gate.parent.MapHeld;
            MapParent parent = map?.Parent;
            return (parent != null ? parent.LabelCap + " · " : "") + gate.parent.LabelCap;
        }

        private static FloatMenuOption Disabled(string label)
        {
            return new FloatMenuOption(label, null);
        }

        private static void Message(string text)
        {
            Messages.Message(text, MessageTypeDefOf.RejectInput, false);
        }
    }
}
