using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using WhisperOfTheForest.AncientRuins;
using WhisperOfTheForest.Rituals;
using WhisperOfTheForest.Teleport;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Lab commands for the ruin round: observe the world-map quests and sites the seeking rite left
    /// behind (<c>ruin</c>), walk into a site map the way a caravan does (<c>ruin action=enter</c>),
    /// census what the map generated (<c>ruin action=report</c>) and roll a loot table in the air
    /// without spawning anything (<c>loot</c>). Everything here is a dev harness around the product
    /// path: the quests, the site part and the casket are the shipped objects, not stand-ins.
    /// </summary>
    internal static partial class LabCommands
    {
        /// <summary>Ground loot the ruin report counts, grouped the way the loot tables roll it.</summary>
        private static readonly string[][] RuinReportGroups =
        {
            new[] { "WotF_Mithril" },
            new[] { "WotF_Adamantine" },
            new[] { "ComponentSpacer" },
            new[] { "WotF_EngineeringResonanceCore" }
        };

        private const string RubbingPrefix = "WotF_SuperRuneRubbing";

        private const string ResonanceCoreDefName = "WotF_EngineeringResonanceCore";

        /// <summary>
        /// "list" (default, read-only): the seeking quests and their sites, including the world-map
        /// deadline each one still has. "enter" needs confirm=1: it forms a caravan from the selected
        /// colonist and runs the vanilla arrival action, which generates the map and sends the arrived
        /// letter; the map comes out of a long event, so "enter" does not switch to it. "switch" makes the
        /// site's map the current one once it exists. "report" censuses the site's map whichever map the
        /// game is showing.
        /// </summary>
        private static string Ruin(string id, Dictionary<string, string> args)
        {
            if (Current.Game == null)
            {
                throw new LabCommandException("no game loaded");
            }

            string action = Get(args, "action", "list").ToLowerInvariant();
            switch (action)
            {
                case "list":
                    return RuinList(id);
                case "enter":
                    return RuinEnter(id, args);
                case "report":
                    return RuinReport(id, args);
                case "switch":
                    return RuinSwitch(id, args);
                default:
                    throw new LabCommandException("ruin action must be list, enter, report or switch");
            }
        }

        /// <summary>
        /// Every ruin option any ritual Def offers. The lab never names the two ruins itself, so the
        /// commands keep working when a third ruin is added to the data.
        /// </summary>
        private static List<RuinSeekOption> AllRuinOptions()
        {
            var options = new List<RuinSeekOption>();
            foreach (RitualDef def in DefDatabase<RitualDef>.AllDefsListForReading)
            {
                if (def.ruinOptions == null)
                {
                    continue;
                }

                foreach (RuinSeekOption option in def.ruinOptions)
                {
                    if (option != null && option.sitePart != null && !options.Contains(option))
                    {
                        options.Add(option);
                    }
                }
            }

            return options;
        }

        private static bool IsRuinSite(Site site)
        {
            if (site == null)
            {
                return false;
            }

            List<RuinSeekOption> options = AllRuinOptions();
            for (int i = 0; i < site.parts.Count; i++)
            {
                if (options.Any(option => option.sitePart == site.parts[i].def))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The quests the seeking rite generated, whatever state they are in.</summary>
        private static List<Quest> RuinQuests()
        {
            var quests = new List<Quest>();
            foreach (Quest quest in Find.QuestManager.QuestsListForReading)
            {
                if (quest.root != null && quest.root.root is QuestNode_Root_RuinSeek)
                {
                    quests.Add(quest);
                }
            }

            return quests;
        }

        private static Site SiteOfQuest(Quest quest, out QuestPart_SpawnWorldObject spawnPart)
        {
            spawnPart = null;
            if (quest == null || !quest.TryGetFirstPartOfType(out spawnPart))
            {
                return null;
            }

            return spawnPart.worldObject as Site;
        }

        private static string RuinList(string id)
        {
            StringBuilder json = new StringBuilder(1024);
            json.Append("\"action\":\"list\"");
            json.Append(",\"hasOutstanding\":").Append(Json.Bool(RuinSeeking.HasOutstandingRuin()));
            json.Append(",\"tick\":").Append(Json.Num(Find.TickManager.TicksGame));
            json.Append(",\"quests\":[");

            List<Quest> quests = RuinQuests();
            for (int i = 0; i < quests.Count; i++)
            {
                Quest quest = quests[i];
                if (i > 0)
                {
                    json.Append(',');
                }

                Site site = SiteOfQuest(quest, out QuestPart_SpawnWorldObject spawnPart);
                json.Append('{');
                json.Append("\"id\":").Append(Json.Num(quest.id));
                json.Append(",\"def\":").Append(Json.Str(quest.root.defName));
                json.Append(",\"name\":").Append(Json.Str(quest.name));
                json.Append(",\"state\":").Append(Json.Str(quest.State.ToString()));
                json.Append(",\"spawnPartSpawned\":")
                    .Append(Json.Bool(spawnPart != null && spawnPart.worldObject != null && spawnPart.worldObject.Spawned));
                json.Append(",\"timeoutTicksLeft\":");
                if (quest.TryGetFirstPartOfType(out QuestPart_WorldObjectTimeout timeout))
                {
                    json.Append(Json.Num(timeout.TicksLeft));
                }
                else
                {
                    json.Append("null");
                }

                json.Append(",\"site\":");
                if (site == null)
                {
                    json.Append("null");
                }
                else
                {
                    AppendSite(json, site);
                }

                json.Append('}');
            }

            json.Append(']');

            // Sites that carry a ruin part but no quest are orphans worth seeing; they would keep the
            // "already outstanding" gate honest while being unreachable from the quest list.
            json.Append(",\"sites\":[");
            bool firstSite = true;
            foreach (Site site in Find.WorldObjects.Sites)
            {
                if (!IsRuinSite(site))
                {
                    continue;
                }

                if (!firstSite)
                {
                    json.Append(',');
                }

                firstSite = false;
                AppendSite(json, site);
            }

            json.Append(']');

            // The ruin options the seeking rite can point at, so a refused CanSeek can be explained.
            json.Append(",\"options\":[");
            List<RuinSeekOption> options = AllRuinOptions();
            for (int i = 0; i < options.Count; i++)
            {
                if (i > 0)
                {
                    json.Append(',');
                }

                RuinSeekOption option = options[i];
                json.Append('{');
                json.Append("\"quest\":").Append(Json.Str(option.quest?.defName));
                json.Append(",\"sitePart\":").Append(Json.Str(option.sitePart.defName));
                json.Append(",\"threatPointsFactor\":").Append(Json.Num(option.threatPointsFactor));
                json.Append('}');
            }

            json.Append(']');
            return Ok(id, json.ToString());
        }

        private static void AppendSite(StringBuilder json, Site site)
        {
            json.Append('{');
            json.Append("\"label\":").Append(Json.Str(site.Label));
            json.Append(",\"tileId\":").Append(Json.Num(site.Tile.tileId));
            json.Append(",\"layer\":").Append(Json.Str(site.Tile.LayerDef.defName));
            json.Append(",\"spawned\":").Append(Json.Bool(site.Spawned));
            json.Append(",\"hasMap\":").Append(Json.Bool(site.HasMap));
            json.Append(",\"mapId\":").Append(site.HasMap ? Json.Num(site.Map.uniqueID) : "null");
            json.Append(",\"faction\":").Append(Json.Str(site.Faction?.def.defName));
            json.Append(",\"enterCooldownTicks\":").Append(Json.Num(site.EnterCooldownTicksLeft()));
            json.Append(",\"questTags\":[")
                .Append(string.Join(",", (site.questTags ?? new List<string>()).Select(Json.Str)))
                .Append(']');
            json.Append(",\"parts\":[")
                .Append(string.Join(",", site.parts.Select(part => Json.Str(part.def.defName))))
                .Append(']');
            json.Append('}');
        }

        /// <summary>
        /// Picks the ruin site a command works on: the single outstanding one, or the one named by
        /// <c>quest=</c>. More than one candidate is an error instead of a guess.
        /// </summary>
        private static Site RequireRuinSite(Dictionary<string, string> args)
        {
            string wantedQuest = Get(args, "quest", null);
            var candidates = new List<Site>();
            foreach (Quest quest in RuinQuests())
            {
                if (wantedQuest != null && !quest.root.defName.Equals(wantedQuest, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Site site = SiteOfQuest(quest, out QuestPart_SpawnWorldObject _);
                if (site != null && site.Spawned && !candidates.Contains(site))
                {
                    candidates.Add(site);
                }
            }

            if (candidates.Count == 0)
            {
                throw new LabCommandException(wantedQuest == null
                    ? "no sought ruin on the world map; run a seeking ritual first"
                    : "no sought ruin for quest '" + wantedQuest + "'");
            }

            if (candidates.Count > 1)
            {
                throw new LabCommandException("several sought ruins exist (" + string.Join(", ", candidates.Select(s => s.Label))
                    + "); pass quest=<QuestScriptDef> to pick one");
            }

            return candidates[0];
        }

        private static string RuinEnter(string id, Dictionary<string, string> args)
        {
            if (Get(args, "confirm", "0") != "1")
            {
                throw new LabCommandException("ruin enter forms a caravan and generates a site map; pass confirm=1");
            }

            if (LongEventHandler.AnyEventNowOrWaiting)
            {
                throw new LabCommandException("a long event is already running; retry when the game is back");
            }

            Map home = RequireMap();
            Site site = RequireRuinSite(args);
            if (site.EnterCooldownBlocksEntering())
            {
                throw new LabCommandException("site enter cooldown is still running ("
                    + site.EnterCooldownTicksLeft().ToStringTicksToPeriod() + " left)");
            }

            Pawn pawn = RequireSelectedPawn();
            if (!pawn.IsColonistPlayerControlled)
            {
                throw new LabCommandException("select a player colonist to walk into the ruin");
            }

            if (pawn.Map != home)
            {
                throw new LabCommandException("the selected colonist is not on the current map");
            }

            // Same shape as CaravanExitMapUtility.ExitMapAndCreateCaravan, but the caravan is born on
            // the site tile: the lab must not spend map days travelling, and the vanilla arrival
            // action only cares that a caravan exists at the destination.
            Caravan caravan = CaravanMaker.MakeCaravan(Gen.YieldSingle(pawn), Faction.OfPlayer, site.Tile,
                addToWorldPawnsIfNotAlready: false);
            pawn.ExitMap(allowedToJoinOrCreateCaravan: false, Rot4.Invalid);
            if (!caravan.ContainsPawn(pawn))
            {
                throw new LabCommandException("the selected colonist did not join the lab caravan");
            }

            home.Parent?.Notify_CaravanFormed(caravan);

            // The vanilla arrival path: it generates the map, notifies the tick manager, builds the
            // threat letter from the site part's arrivedLetter and then spawns the caravan on it.
            // The arrival runs map generation as a long event, so the map is not there yet when this
            // returns; `ruin action=switch` moves the view once `ruin` shows hasMap=true.
            new CaravanArrivalAction_VisitSite(site).Arrived(caravan);

            var json = new StringBuilder(256);
            json.Append("\"action\":\"enter\"");
            json.Append(",\"queued\":").Append(Json.Bool(!site.HasMap));
            json.Append(",\"pawn\":").Append(Json.Num(pawn.thingIDNumber));
            json.Append(",\"pawnMap\":").Append(pawn.Spawned && pawn.Map != null ? Json.Num(pawn.Map.uniqueID) : "null");
            json.Append(",\"caravan\":").Append(caravan.Destroyed ? "null" : Json.Num(caravan.ID));
            json.Append(",\"site\":");
            AppendSite(json, site);
            return Ok(id, json.ToString());
        }

        /// <summary>The map of the sought ruin, whichever map the game is showing; an error until it exists.</summary>
        private static Map RequireRuinMap(Dictionary<string, string> args, out Site site)
        {
            site = RequireRuinSite(args);
            if (!site.HasMap)
            {
                throw new LabCommandException("the ruin at tile " + site.Tile.tileId
                    + " has no map yet; run `ruin action=enter confirm=1` and wait for the generation to finish");
            }

            return site.Map;
        }

        /// <summary>Makes the ruin's map the current one and leaves the world view, for screenshots.</summary>
        private static string RuinSwitch(string id, Dictionary<string, string> args)
        {
            Map map = RequireRuinMap(args, out Site site);
            Current.Game.CurrentMap = map;
            CameraJumper.TryHideWorld();

            // Look at the party if it is there, otherwise at the middle of the map.
            Pawn colonist = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            CameraJumper.TryJump(colonist != null ? colonist.Position : map.Center, map);

            var json = new StringBuilder(256);
            json.Append("\"action\":\"switch\"");
            json.Append(",\"map\":").Append(Json.Num(map.uniqueID));
            json.Append(",\"currentMap\":").Append(Json.Num(Current.Game.CurrentMap.uniqueID));
            json.Append(",\"colonists\":").Append(Json.Num(map.mapPawns.FreeColonistsSpawnedCount));
            json.Append(",\"site\":");
            AppendSite(json, site);
            return Ok(id, json.ToString());
        }

        private static string RuinReport(string id, Dictionary<string, string> args)
        {
            Map map = RequireRuinMap(args, out Site site);

            var json = new StringBuilder(1024);
            json.Append("\"action\":\"report\"");
            json.Append(",\"tick\":").Append(Json.Num(Find.TickManager.TicksGame));
            json.Append(",\"map\":").Append(Json.Num(map.uniqueID));
            json.Append(",\"site\":");
            AppendSite(json, site);

            var caskets = map.listerThings.AllThings.OfType<Building_RelicCasket>().ToList();
            json.Append(",\"casketCount\":").Append(Json.Num(caskets.Count));
            json.Append(",\"caskets\":[");
            for (int i = 0; i < caskets.Count; i++)
            {
                if (i > 0)
                {
                    json.Append(',');
                }

                Building_RelicCasket casket = caskets[i];
                json.Append('{');
                json.Append("\"id\":").Append(Json.Num(casket.thingIDNumber));
                json.Append(",\"pos\":").Append(Json.Cell(casket.Position));
                json.Append(",\"openTicks\":").Append(Json.Num(casket.OpenTicks));
                json.Append(",\"canOpen\":").Append(Json.Bool(casket.CanOpen));
                json.Append(",\"contents\":[");
                var contents = casket.SearchableContents;
                for (int j = 0; j < contents.Count; j++)
                {
                    if (j > 0)
                    {
                        json.Append(',');
                    }

                    Thing thing = contents[j];
                    json.Append('{');
                    json.Append("\"def\":").Append(Json.Str(thing.def.defName));
                    json.Append(",\"count\":").Append(Json.Num(thing.stackCount));
                    json.Append('}');
                }

                json.Append(']');
                json.Append('}');
            }

            json.Append(']');

            json.Append(",\"ground\":[");
            bool first = true;
            foreach (string[] group in RuinReportGroups)
            {
                first = AppendGroundGroup(json, map, group[0], group, first);
            }

            // The rubbings are two defs behind one prefix; read the prefix instead of listing them twice.
            string[] rubbings = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(t => t.defName.StartsWith(RubbingPrefix, StringComparison.Ordinal))
                .Select(t => t.defName)
                .ToArray();
            AppendGroundGroup(json, map, "rubbings", rubbings, first);
            json.Append(']');

            int hostile = 0;
            int mechanoids = 0;
            int mechanoidsDormant = 0;
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer))
                {
                    hostile++;
                }

                if (!pawn.RaceProps.IsMechanoid)
                {
                    continue;
                }

                mechanoids++;
                CompCanBeDormant dormant = pawn.TryGetComp<CompCanBeDormant>();
                if (dormant != null && !dormant.Awake)
                {
                    mechanoidsDormant++;
                }
            }

            json.Append(",\"pawns\":{");
            json.Append("\"total\":").Append(Json.Num(map.mapPawns.AllPawnsSpawnedCount));
            json.Append(",\"colonists\":").Append(Json.Num(map.mapPawns.FreeColonistsSpawnedCount));
            json.Append(",\"hostileToPlayer\":").Append(Json.Num(hostile));
            json.Append(",\"mechanoids\":").Append(Json.Num(mechanoids));
            json.Append(",\"mechanoidsDormant\":").Append(Json.Num(mechanoidsDormant));
            json.Append(",\"mechanoidsAwake\":").Append(Json.Num(mechanoids - mechanoidsDormant));
            json.Append('}');

            AppendExitCountdown(json, site);
            AppendBrokenGate(json, map);
            return Ok(id, json.ToString());
        }

        /// <summary>
        /// Where the ruin's broken gate stands and what surrounds it, so the spacing rule is readable in
        /// game: how far the nearest guard is and how many standable cells the party can actually walk to
        /// inside the gate's arrivalRadius. The gap to the temple rectangle is not recomputable here —
        /// the generator's working data is cleared when the map finishes (Verse/MapGenerator.cs:216-218 and
        /// ClearWorkingData :358-373) — it is logged at generation under "broken gate stands at".
        /// </summary>
        private static void AppendBrokenGate(StringBuilder json, Map map)
        {
            CompTeleportGate gate = TeleportGateTransfer.FindRuinGate(map);
            json.Append(",\"brokenGate\":");
            if (gate == null)
            {
                json.Append("null");
                return;
            }

            CellRect footprint = gate.parent.OccupiedRect();
            json.Append('{');
            json.Append("\"pos\":").Append(Json.Cell(gate.parent.Position));
            json.Append(",\"footprint\":").Append(Json.Str(footprint.ToString()));
            json.Append(",\"arrivalRadius\":").Append(Json.Num(gate.Props.arrivalRadius));

            float nearest = -1f;
            int guards = 0;
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (!pawn.RaceProps.IsMechanoid)
                {
                    continue;
                }

                guards++;
                float distance = pawn.Position.DistanceTo(gate.parent.Position);
                if (nearest < 0f || distance < nearest)
                {
                    nearest = distance;
                }
            }

            json.Append(",\"mechanoids\":").Append(Json.Num(guards));
            json.Append(",\"nearestGuard\":").Append(nearest < 0f ? "null" : Json.Num(nearest));
            json.Append(",\"landingCells\":").Append(Json.Num(CountLandingCells(map, gate, footprint)));
            json.Append('}');
        }

        /// <summary>Standable cells the party can walk to inside the gate's arrival radius, one walk from its ring.</summary>
        private static int CountLandingCells(Map map, CompTeleportGate gate, CellRect footprint)
        {
            IntVec3 center = footprint.CenterCell;
            float radius = gate.Props.arrivalRadius;
            IntVec3 root = IntVec3.Invalid;
            foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(gate.parent))
            {
                if (cell.InBounds(map) && cell.Standable(map))
                {
                    root = cell;
                    break;
                }
            }

            if (!root.IsValid)
            {
                return 0;
            }

            int count = 0;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (!cell.InBounds(map) || footprint.Contains(cell) || !cell.Standable(map))
                {
                    continue;
                }

                if (map.reachability.CanReach(root, cell, PathEndMode.OnCell, TraverseMode.PassDoors))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool AppendGroundGroup(StringBuilder json, Map map, string key, string[] defNames, bool first)
        {
            int count = 0;
            int stacks = 0;
            foreach (string defName in defNames)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def == null)
                {
                    continue;
                }

                foreach (Thing thing in map.listerThings.ThingsOfDef(def))
                {
                    count += thing.stackCount;
                    stacks++;
                }
            }

            if (!first)
            {
                json.Append(',');
            }

            json.Append('{');
            json.Append("\"key\":").Append(Json.Str(key));
            json.Append(",\"defs\":[").Append(string.Join(",", defNames.Select(Json.Str))).Append(']');
            json.Append(",\"count\":").Append(Json.Num(count));
            json.Append(",\"stacks\":").Append(Json.Num(stacks));
            json.Append('}');
            return false;
        }

        /// <summary>
        /// The site's forced-exit clock, read straight from the game component that owns it. The
        /// deadline runs whether or not the quest is still alive, so the lab reports it the same way
        /// the inspect pane does.
        /// </summary>
        private static void AppendExitCountdown(StringBuilder json, Site site)
        {
            json.Append(",\"exitDeadline\":");
            RuinDeadline deadline = GameComponent_RuinDeadlines.Current?.For(site);
            if (deadline == null)
            {
                json.Append("null");
                return;
            }

            json.Append('{');
            json.Append("\"durationTicks\":").Append(Json.Num(deadline.durationTicks));
            json.Append(",\"started\":").Append(Json.Bool(deadline.Started));
            json.Append(",\"startTick\":").Append(Json.Num(deadline.StartTick));
            json.Append(",\"ticksLeft\":").Append(Json.Num(deadline.TicksLeft));
            json.Append(",\"daysLeft\":").Append(Json.Num(deadline.TicksLeft / (float)GenDate.TicksPerDay));
            json.Append(",\"exited\":").Append(Json.Bool(deadline.Exited));
            json.Append('}');
        }

        /// <summary>
        /// Rolls a ThingSetMakerDef <c>n</c> times and counts what came out. Nothing is spawned, so
        /// the request never leaves a pile on a map; the things are destroyed right after counting.
        /// </summary>
        private static string Loot(string id, Dictionary<string, string> args)
        {
            if (Current.Game == null)
            {
                throw new LabCommandException("no game loaded");
            }

            string defName = Get(args, "def", null);
            if (defName == null)
            {
                throw new LabCommandException("loot needs def=<ThingSetMakerDef>");
            }

            ThingSetMakerDef def = DefDatabase<ThingSetMakerDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                throw new LabCommandException("unknown ThingSetMakerDef '" + defName + "'");
            }

            int runs = args.ContainsKey("n") ? RequireInt(args, "n") : 10;
            if (runs < 1 || runs > 5000)
            {
                throw new LabCommandException("n must be 1..5000");
            }

            var hits = new Dictionary<string, int>();
            var totals = new Dictionary<string, int>();
            int emptyRuns = 0;
            int runsWithRubbing = 0;
            int runsWithResonanceCore = 0;
            for (int run = 0; run < runs; run++)
            {
                List<Thing> things = def.root.Generate(default(ThingSetMakerParams));
                if (things.Count == 0)
                {
                    emptyRuns++;
                }

                bool runHadRubbing = false;
                bool runHadResonanceCore = false;
                foreach (Thing thing in things)
                {
                    string name = thing.def.defName;
                    hits[name] = hits.TryGetValue(name, out int hit) ? hit + 1 : 1;
                    totals[name] = totals.TryGetValue(name, out int total) ? total + thing.stackCount : thing.stackCount;
                    runHadRubbing |= name.StartsWith(RubbingPrefix, StringComparison.Ordinal);
                    runHadResonanceCore |= name == ResonanceCoreDefName;
                    thing.Destroy(DestroyMode.Vanish);
                }

                if (runHadRubbing)
                {
                    runsWithRubbing++;
                }

                if (runHadResonanceCore)
                {
                    runsWithResonanceCore++;
                }
            }

            var json = new StringBuilder(512);
            json.Append("\"def\":").Append(Json.Str(def.defName));
            json.Append(",\"label\":").Append(Json.Str(def.LabelCap.ToString()));
            json.Append(",\"runs\":").Append(Json.Num(runs));
            json.Append(",\"emptyRuns\":").Append(Json.Num(emptyRuns));
            json.Append(",\"runsWithRubbing\":").Append(Json.Num(runsWithRubbing));
            json.Append(",\"rubbingRate\":").Append(Json.Num(runsWithRubbing / (float)runs));
            json.Append(",\"runsWithResonanceCore\":").Append(Json.Num(runsWithResonanceCore));
            json.Append(",\"items\":[");
            bool first = true;
            foreach (KeyValuePair<string, int> entry in hits.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (!first)
                {
                    json.Append(',');
                }

                first = false;
                json.Append('{');
                json.Append("\"def\":").Append(Json.Str(entry.Key));
                json.Append(",\"hits\":").Append(Json.Num(entry.Value));
                json.Append(",\"hitRate\":").Append(Json.Num(entry.Value / (float)runs));
                json.Append(",\"total\":").Append(Json.Num(totals[entry.Key]));
                json.Append(",\"averagePerRun\":").Append(Json.Num(totals[entry.Key] / (float)runs));
                json.Append('}');
            }

            json.Append(']');
            return Ok(id, json.ToString());
        }

        /// <summary>
        /// Resolves a <c>ruin=</c> key against a ritual's options. The key may be the quest defName,
        /// the site part defName, or the last segment of either (<c>common</c>, <c>core</c>).
        /// </summary>
        private static RuinSeekOption FindRuinOption(RitualDef def, string key)
        {
            if (def == null || def.ruinOptions == null)
            {
                return null;
            }

            foreach (RuinSeekOption option in def.ruinOptions)
            {
                if (option == null)
                {
                    continue;
                }

                if (MatchesRuinKey(option.quest?.defName, key) || MatchesRuinKey(option.sitePart?.defName, key))
                {
                    return option;
                }
            }

            return null;
        }

        private static bool MatchesRuinKey(string candidate, string key)
        {
            if (candidate == null || key == null)
            {
                return false;
            }

            if (candidate.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            int underscore = candidate.LastIndexOf('_');
            return underscore >= 0 && underscore < candidate.Length - 1
                && candidate.Substring(underscore + 1).Equals(key, StringComparison.OrdinalIgnoreCase);
        }
    }
}
