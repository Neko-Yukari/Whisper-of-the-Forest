using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Teleport;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Lab commands for the T3 gate round: stand a colony gate up with its network (<c>gate
    /// action=build</c>), load it, read every gate's state and the fee the current load would cost
    /// (<c>gate</c>), send it to a ruin (<c>gate action=send</c>) and bring it back through a ruin's
    /// broken gate (<c>gate action=return</c>). Every mutation goes through the shipped comp and the
    /// shipped transfer path: the lab only fills the container and calls the same method the gate
    /// menu calls, so what runs here is what a player's click runs.
    /// </summary>
    internal static partial class LabCommands
    {
        private static string Gate(string id, Dictionary<string, string> args)
        {
            if (Current.Game == null)
            {
                throw new LabCommandException("no game loaded");
            }

            switch (Get(args, "action", "list").ToLowerInvariant())
            {
                case "list":
                    return GateList(id);
                case "build":
                    return GateBuild(id, args);
                case "load":
                    return GateLoad(id, args);
                case "send":
                    return GateSend(id, args);
                case "return":
                    return GateReturn(id, args);
                case "flick":
                    return GateFlick(id, args);
                default:
                    throw new LabCommandException("gate action must be list, build, load, send, return or flick");
            }
        }

        /// <summary>Every gate on every map, every reachable ruin, and which colony gates are open.</summary>
        private static string GateList(string id)
        {
            var json = new StringBuilder(1024);
            json.Append("\"action\":\"list\"");
            json.Append(",\"gates\":[");
            bool first = true;
            foreach (Map map in Find.Maps)
            {
                foreach (CompTeleportGate gate in TeleportGateTransfer.GatesOn(map))
                {
                    if (!first)
                    {
                        json.Append(',');
                    }

                    first = false;
                    AppendGate(json, gate);
                }
            }

            json.Append(']');
            json.Append(",\"ruins\":[");
            first = true;
            foreach (Site site in TeleportGateTransfer.RuinSites())
            {
                if (!first)
                {
                    json.Append(',');
                }

                first = false;
                CompTeleportGate anchor = site.HasMap ? TeleportGateTransfer.FindRuinGate(site.Map) : null;
                json.Append('{');
                json.Append("\"tile\":").Append(Json.Num(site.Tile.tileId));
                json.Append(",\"label\":").Append(Json.Str(site.Label));
                json.Append(",\"part\":").Append(Json.Str(site.MainSitePartDef.defName));
                json.Append(",\"hasMap\":").Append(Json.Bool(site.HasMap));
                json.Append(",\"gateId\":").Append(anchor == null ? "null" : Json.Num(anchor.parent.thingIDNumber));
                json.Append('}');
            }

            json.Append(']');
            json.Append(",\"homeGates\":[");
            first = true;
            foreach (CompTeleportGate home in TeleportGateTransfer.WorkingHomeGates())
            {
                if (!first)
                {
                    json.Append(',');
                }

                first = false;
                json.Append(Json.Num(home.parent.thingIDNumber));
            }

            json.Append(']');
            return Ok(id, json.ToString());
        }

        /// <summary>
        /// Stands a gate near the selected colonist with a charged battery beside it, so the gate is
        /// open on the next tick. Flicking it on and off afterwards is `gate action=flick`.
        /// </summary>
        private static string GateBuild(string id, Dictionary<string, string> args)
        {
            if (Get(args, "confirm", "0") != "1")
            {
                throw new LabCommandException("gate build mutates the map; pass confirm=1");
            }

            Map map = RequireMap();
            Pawn selected = RequireSelectedPawn();
            ThingDef def = ThingDef.Named("WotF_TeleportGate");
            if (!CellFinder.TryFindRandomCellNear(selected.Position, map, 8,
                    cell => GenSpawn.CanSpawnAt(def, cell, map), out IntVec3 cell, 200))
            {
                throw new LabCommandException("no free " + def.size + " cell near " + selected.Position);
            }

            var gate = (Building)ThingMaker.MakeThing(def, StuffFor(def, args));
            gate.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(gate, cell, map, Rot4.North, WipeMode.Vanish);

            IntVec3 nodeCell = cell + new IntVec3(def.size.x + 1, 0, 1);
            IntVec3 batteryCell = cell + new IntVec3(def.size.x + 1, 0, 3);
            if (!nodeCell.InBounds(map) || !batteryCell.InBounds(map))
            {
                throw new LabCommandException("the gate landed too close to the map edge for its node and battery");
            }

            Thing node = ThingMaker.MakeThing(ThingDef.Named("WotF_ManaNode"), ThingDefOf.BlocksGranite);
            node.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(node, nodeCell, map, Rot4.North, WipeMode.Vanish);

            var battery = (Building)ThingMaker.MakeThing(ThingDef.Named("WotF_ManaBattery"), ThingDefOf.BlocksGranite);
            battery.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(battery, batteryCell, map, Rot4.North, WipeMode.Vanish);
            CompManaBattery stored = battery.GetComp<CompManaBattery>();
            stored?.SetStored(stored.Capacity);

            MapComponent_ManaNetwork.For(map).MarkDirty();
            map.reachability.ClearCache();
            WotfLog.Info(WotfLog.ModuleTeleport, "lab built a gate at " + cell + " with a node and a charged battery");

            var json = new StringBuilder(512);
            json.Append("\"action\":\"build\"");
            json.Append(",\"node\":").Append(Json.Num(node.thingIDNumber));
            json.Append(",\"battery\":").Append(Json.Num(battery.thingIDNumber));
            json.Append(",\"batteryStored\":").Append(Json.Num(stored?.Stored ?? 0f));
            json.Append(",\"tickNote\":").Append(Json.Str("the gate pays its upkeep on the next tick; read state with `gate`"));
            json.Append(",\"gate\":");
            AppendGate(json, gate.GetComp<CompTeleportGate>());
            return Ok(id, json.ToString());
        }

        /// <summary>
        /// Loads the gate the way the loading job does: <c>pawn=&lt;thingId&gt;</c> despawns the pawn
        /// into the container, <c>items=Def:count,Def:count</c> splits stacks off nearby ground.
        /// Ground items are taken within <c>radius=</c> cells of the gate, by default the gate's own
        /// <c>arrivalRadius</c>: the broken gate has no mana link, so <c>linkRadius</c> is 0 there.
        /// </summary>
        private static string GateLoad(string id, Dictionary<string, string> args)
        {
            Map map = RequireMap();
            CompTeleportGate gate = RequireGateOnMap(map, RequireInt(args, "id"));
            CompTransporter transporter = gate.Transporter;
            if (transporter == null)
            {
                throw new LabCommandException(gate.parent.def.defName + " has no CompTransporter");
            }

            ThingOwner owner = transporter.GetDirectlyHeldThings();
            int pawns = 0;
            if (args.ContainsKey("pawn"))
            {
                Pawn pawn = FindPawn(map, RequireInt(args, "pawn"));
                if (pawn == null || !pawn.Spawned)
                {
                    throw new LabCommandException("no spawned pawn with id " + args["pawn"]);
                }

                pawn.DeSpawnOrDeselect();
                if (!owner.TryAdd(pawn))
                {
                    throw new LabCommandException("the gate refused " + pawn.LabelShort);
                }

                pawns++;
            }

            float pickupRadius = args.ContainsKey("radius") ? RequireFloat(args, "radius") : gate.Props.arrivalRadius;
            int items = 0;
            string list = Get(args, "items", null);
            if (list != null)
            {
                foreach (string entry in list.Split(','))
                {
                    string[] parts = entry.Split(':');
                    ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(parts[0].Trim());
                    if (def == null || def.category != ThingCategory.Item)
                    {
                        throw new LabCommandException("items must name loaded item defs, got '" + parts[0] + "'");
                    }

                    int want = 1;
                    if (parts.Length > 1 && !int.TryParse(parts[1].Trim(), out want))
                    {
                        throw new LabCommandException("items must look like Def:count, got '" + entry + "'");
                    }

                    foreach (Thing thing in map.listerThings.ThingsOfDef(def).ToList())
                    {
                        if (want <= 0)
                        {
                            break;
                        }

                        if (!thing.Spawned || !thing.Position.InHorDistOf(gate.parent.Position, pickupRadius))
                        {
                            continue;
                        }

                        Thing piece = thing.SplitOff(want < thing.stackCount ? want : thing.stackCount);
                        if (!owner.TryAdd(piece))
                        {
                            piece.Destroy();
                            continue;
                        }

                        want -= piece.stackCount;
                        items += piece.stackCount;
                    }
                }
            }

            var json = new StringBuilder(512);
            json.Append("\"action\":\"load\"");
            json.Append(",\"pawnsLoaded\":").Append(Json.Num(pawns));
            json.Append(",\"itemsLoaded\":").Append(Json.Num(items));
            json.Append(",\"gate\":");
            AppendGate(json, gate);
            return Ok(id, json.ToString());
        }

        /// <summary>
        /// The long connection: the same call the gate's own menu makes, so map generation, the fee,
        /// the contents move and the camera all run through the shipped path. The event finishes after
        /// this command returns; read the result with `gate` or `ruin`.
        /// </summary>
        private static string GateSend(string id, Dictionary<string, string> args)
        {
            if (Get(args, "confirm", "0") != "1")
            {
                throw new LabCommandException("gate send moves pawns and cargo; pass confirm=1");
            }

            Map map = RequireMap();
            CompTeleportGate gate = RequireGateOnMap(map, RequireInt(args, "id"));
            Site site;
            if (args.ContainsKey("tile"))
            {
                int tile = RequireInt(args, "tile");
                site = TeleportGateTransfer.RuinSites().FirstOrDefault(s => s.Tile.tileId == tile);
            }
            else
            {
                site = TeleportGateTransfer.RuinSites().FirstOrDefault();
            }

            if (site == null)
            {
                throw new LabCommandException("no gate-reachable ruin; run the seeking rite or pass tile=");
            }

            CompTransporter transporter = gate.Transporter;
            float cost = transporter == null ? 0f : TeleportGateTransfer.RemoteCost(gate, transporter);
            TeleportGateTransfer.Send(gate, site);

            var json = new StringBuilder(512);
            json.Append("\"action\":\"send\"");
            json.Append(",\"queued\":").Append(Json.Bool(true));
            json.Append(",\"tile\":").Append(Json.Num(site.Tile.tileId));
            json.Append(",\"site\":").Append(Json.Str(site.Label));
            json.Append(",\"hasMap\":").Append(Json.Bool(site.HasMap));
            json.Append(",\"cost\":").Append(Json.Num(cost));
            json.Append(",\"pawns\":").Append(Json.Num(transporter == null ? 0 : TeleportGateTransfer.PawnCount(transporter)));
            json.Append(",\"cargoMass\":").Append(Json.Num(transporter == null ? 0f : TeleportGateTransfer.CargoMass(transporter)));
            json.Append(",\"gate\":");
            AppendGate(json, gate);
            return Ok(id, json.ToString());
        }

        /// <summary>
        /// The one crossing home from a ruin's broken gate. Synchronous: the contents land by the
        /// colony gate and the broken gate is destroyed before this returns.
        /// </summary>
        private static string GateReturn(string id, Dictionary<string, string> args)
        {
            if (Get(args, "confirm", "0") != "1")
            {
                throw new LabCommandException("gate return moves pawns and cargo; pass confirm=1");
            }

            CompTeleportGate ruinGate = RequireGate(RequireInt(args, "id"));
            CompTeleportGate home = args.ContainsKey("home")
                ? RequireGate(RequireInt(args, "home"))
                : TeleportGateTransfer.WorkingHomeGates().FirstOrDefault();

            CompTransporter transporter = ruinGate.Transporter;
            int before = transporter == null ? 0 : transporter.GetDirectlyHeldThings().Count;
            int pawnsBefore = transporter == null ? 0 : TeleportGateTransfer.PawnCount(transporter);
            float storedBefore = home?.Network?.Stored ?? 0f;
            int gateId = ruinGate.parent.thingIDNumber;

            // The product refuses for its own reasons - no open home gate, nothing loaded, no free landing
            // cell, the home network cannot pay - and says so in the words the player would read. A refusal
            // is a failed command here, not an ok:true that moved nothing.
            TeleportGateTransfer.Return(ruinGate, home, out string refusal, out float cost);
            if (refusal != null)
            {
                throw new LabCommandException(refusal);
            }

            var json = new StringBuilder(512);
            json.Append("\"action\":\"return\"");
            json.Append(",\"ruinGate\":").Append(Json.Num(gateId));
            json.Append(",\"contentsBefore\":").Append(Json.Num(before));
            json.Append(",\"pawnsBefore\":").Append(Json.Num(pawnsBefore));
            json.Append(",\"cost\":").Append(Json.Num(cost));
            json.Append(",\"homeStoredBefore\":").Append(Json.Num(storedBefore));
            json.Append(",\"homeStoredAfter\":").Append(Json.Num(home.Network?.Stored ?? 0f));
            json.Append(",\"ruinGateDestroyed\":").Append(Json.Bool(ruinGate.parent.Destroyed));
            json.Append(",\"home\":");
            AppendGate(json, home);
            return Ok(id, json.ToString());
        }

        private static string GateFlick(string id, Dictionary<string, string> args)
        {
            CompTeleportGate gate = RequireGate(RequireInt(args, "id"));
            CompFlickable flickable = gate.parent.GetComp<CompFlickable>();
            if (flickable == null)
            {
                throw new LabCommandException(gate.parent.def.defName + " has no CompFlickable");
            }

            flickable.SwitchIsOn = Get(args, "on", "0") == "1";
            var json = new StringBuilder(384);
            json.Append("\"action\":\"flick\"");
            json.Append(",\"gate\":");
            AppendGate(json, gate);
            return Ok(id, json.ToString());
        }

        private static void AppendGate(StringBuilder json, CompTeleportGate gate)
        {
            Map map = gate?.parent.MapHeld;
            CompTransporter transporter = gate?.Transporter;
            json.Append('{');
            json.Append("\"id\":").Append(Json.Num(gate.parent.thingIDNumber));
            json.Append(",\"def\":").Append(Json.Str(gate.parent.def.defName));
            json.Append(",\"pos\":").Append(Json.Cell(gate.parent.Position));
            json.Append(",\"map\":").Append(map == null ? "null" : Json.Num(map.uniqueID));
            json.Append(",\"home\":").Append(Json.Bool(map != null && map.IsPlayerHome));
            json.Append(",\"ruined\":").Append(Json.Bool(gate.Ruined));
            json.Append(",\"switchedOn\":").Append(Json.Bool(gate.SwitchedOn));
            json.Append(",\"running\":").Append(Json.Bool(gate.Running));
            json.Append(",\"state\":").Append(Json.Str(gate.StateText()));
            json.Append(",\"linkRadius\":").Append(Json.Num(gate.Props.linkRadius));
            json.Append(",\"arrivalRadius\":").Append(Json.Num(gate.Props.arrivalRadius));
            json.Append(",\"upkeepPerSecond\":").Append(Json.Num(gate.Props.upkeepPerSecond));
            json.Append(",\"networkStored\":").Append(Json.Num(gate.Network?.Stored ?? -1f));
            json.Append(",\"pawns\":").Append(Json.Num(transporter == null ? 0 : TeleportGateTransfer.PawnCount(transporter)));
            json.Append(",\"cargoMass\":").Append(Json.Num(transporter == null ? 0f : TeleportGateTransfer.CargoMass(transporter)));
            json.Append(",\"remoteCost\":").Append(transporter == null || gate.Ruined
                ? "null"
                : Json.Num(TeleportGateTransfer.RemoteCost(gate, transporter)));
            json.Append('}');
        }

        /// <summary>Stuff for a MadeFromStuff def: <c>stuff=</c> wins, otherwise one stone block.</summary>
        private static ThingDef StuffFor(ThingDef def, Dictionary<string, string> args)
        {
            if (!def.MadeFromStuff)
            {
                return null;
            }

            ThingDef stuff = Get(args, "stuff", null) == null
                ? ThingDefOf.BlocksGranite
                : DefDatabase<ThingDef>.GetNamedSilentFail(args["stuff"]);
            if (stuff == null || !stuff.IsStuff)
            {
                throw new LabCommandException("stuff must name a loaded stuff def, got " + Get(args, "stuff", null));
            }

            return stuff;
        }

        private static CompTeleportGate RequireGateOnMap(Map map, int thingId)
        {
            Thing thing = FindThing(map, thingId);
            CompTeleportGate gate = thing?.TryGetComp<CompTeleportGate>();
            if (gate == null)
            {
                throw new LabCommandException("no teleport gate with id " + thingId + " on this map");
            }

            return gate;
        }

        private static CompTeleportGate RequireGate(int thingId)
        {
            foreach (Map map in Find.Maps)
            {
                CompTeleportGate gate = FindThing(map, thingId)?.TryGetComp<CompTeleportGate>();
                if (gate != null)
                {
                    return gate;
                }
            }

            throw new LabCommandException("no teleport gate with id " + thingId + " on any map");
        }
    }
}
