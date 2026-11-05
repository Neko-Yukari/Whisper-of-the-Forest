using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Missiles;
using WhisperOfTheForest.Movement;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Whitelisted lab commands. Every command is a small, explicit game operation; there is no
    /// eval, no file access outside the lab folder and no network.
    /// </summary>
    internal static class LabCommands
    {
        private const string ElfPawnKindDefName = "WotF_Elf_Colonist";

        private const int MaxColonists = 16;
        private const int MaxExperimentThings = 32;
        private const int MaxTraitEntries = 16;
        private const int MaxHediffEntries = 32;
        private const int MaxObservedFrames = 16;
        private const int MaxObservedPlants = 16;

        private static int labFiredMissiles;
        private static readonly Dictionary<int, SetupRecord> Setups = new Dictionary<int, SetupRecord>();
        private static readonly Dictionary<int, BehaviorRecord> BehaviorFixtures = new Dictionary<int, BehaviorRecord>();
        private static readonly Dictionary<string, int> PlantFixtures = new Dictionary<string, int>();

        public static string Execute(string id, string command, Dictionary<string, string> args)
        {
            switch (command.ToLowerInvariant())
            {
                case "status":
                    return Status(id);
                case "observe":
                    return Observe(id);
                case "select":
                    return SelectPawn(id, args);
                case "spawn":
                    return SpawnSubject(id, args);
                case "grant":
                    return Grant(id);
                case "equip":
                    return Equip(id, args);
                case "unequip":
                    return Unequip(id);
                case "move":
                    return Move(id, args);
                case "draft":
                    return Draft(id, args);
                case "sow":
                    return Sow(id, args);
                case "attack":
                    return Attack(id, args);
                case "missiles":
                    return MissileStats(id, args);
                case "path":
                    return PathInfo(id, args);
                case "setup":
                    return Setup(id, args);
                case "behaviors":
                    return Behaviors(id, args);
                case "plants":
                    return Plants(id, args);
                case "warp":
                    return Warp(id, args);
                case "hurt":
                    return Hurt(id, args);
                case "camera":
                    return Camera(id, args);
                case "spellbook":
                    return Spellbook(id, args);
                case "time":
                    return Time(id, args);
                case "save":
                    return Save(id, args);
                case "load":
                    return Load(id, args);
                case "step":
                    return Step(id, args);
                case "test":
                    return Test(id, args);
                case "log":
                    return LogLevel(id, args);
                default:
                    throw new LabCommandException("unknown command '" + command + "'");
            }
        }

        private static string LogLevel(string id, Dictionary<string, string> args)
        {
            if (args.ContainsKey("tail"))
            {
                int count = Math.Min(Math.Max(1, RequireInt(args, "tail")), 200);
                var json = new StringBuilder(4096);
                json.Append("\"level\":").Append(Json.Str(WotfLog.LevelName));
                json.Append(",\"entries\":[");
                List<LogMessage> messages = new List<LogMessage>(Log.Messages);
                int start = Math.Max(0, messages.Count - count);
                for (int i = start; i < messages.Count; i++)
                {
                    if (i > start)
                    {
                        json.Append(',');
                    }

                    LogMessage message = messages[i];
                    json.Append("{\"type\":").Append(Json.Str(message.type.ToString()))
                        .Append(",\"text\":").Append(Json.Str(message.text.ToString())).Append('}');
                }

                json.Append(']');
                return Ok(id, json.ToString());
            }

            string level = Get(args, "level", null);
            if (level != null)
            {
                switch (level.ToLowerInvariant())
                {
                    case "debug":
                        WotfLog.DebugEnabled = true;
                        break;
                    case "info":
                        WotfLog.DebugEnabled = false;
                        break;
                    default:
                        throw new LabCommandException("log level must be debug or info");
                }
            }

            return Ok(id, "\"level\":" + Json.Str(WotfLog.LevelName)
                          + ",\"debug\":" + Json.Bool(WotfLog.DebugEnabled));
        }

        private static string Status(string id)
        {
            StringBuilder json = new StringBuilder(512);
            json.Append('{');
            json.Append("\"id\":").Append(Json.Str(id));
            json.Append(",\"ok\":true");
            json.Append(",\"lab\":").Append(Json.Str(LabHost.Version));
            json.Append(",\"protocol\":").Append(Json.Num(LabHost.ProtocolVersion));
            json.Append(",\"harmonyPatched\":").Append(Json.Bool(LabHost.IsHarmonyPatched));
            json.Append(",\"gameLoaded\":").Append(Json.Bool(Current.Game != null));
            // A quick-test Game exists before its map is generated; tests need the map itself.
            json.Append(",\"mapReady\":").Append(Json.Bool(Current.ProgramState == ProgramState.Playing
                && !LongEventHandler.AnyEventNowOrWaiting && Find.CurrentMap != null));
            json.Append(",\"defs\":{");
            json.Append("\"WotF_SpellbookCast\":").Append(Json.Bool(DefExists<AbilityDef>(SpellDefs.CastAbilityDefName)));
            json.Append(",\"WotF_TerrainStride\":").Append(Json.Bool(DefExists<ThingDef>(LabActions.TerrainStrideDefName)));
            json.Append(",\"WotF_BlinkStride\":").Append(Json.Bool(DefExists<ThingDef>(LabActions.BlinkStrideDefName)));
            json.Append(",\"WotF_Visual_RimUnity\":").Append(Json.Bool(DefExists<ArcaneMissileVisualDef>(ArcaneMissileDefs.RimUnityVisualDefName)));
            json.Append(",\"WotF_Visual_Native\":").Append(Json.Bool(DefExists<ArcaneMissileVisualDef>(ArcaneMissileDefs.NativeVisualDefName)));
            json.Append('}');
            if (Current.Game != null && Find.TickManager != null)
            {
                json.Append(",\"tick\":").Append(Json.Num(Find.TickManager.TicksGame));
                json.Append(",\"timeSpeed\":").Append(Json.Str(Find.TickManager.CurTimeSpeed.ToString()));
            }

            Pawn selected = Current.Game != null ? Find.Selector.SingleSelectedThing as Pawn : null;
            json.Append(",\"selectedId\":").Append(selected != null ? Json.Num(selected.thingIDNumber) : "null");
            if (Current.Game != null && Find.CurrentMap != null)
            {
                json.Append(",\"setup\":").Append(SetupState(Find.CurrentMap));
                json.Append(",\"railgun\":{\"pending\":")
                    .Append(Json.Num(GameComponent_SpellCasts.Current?.Count ?? 0))
                    .Append(",\"last\":").Append(Json.Str(RailgunCastService.LastResolve)).Append('}');
            }
            else
            {
                json.Append(",\"setup\":null");
            }

            json.Append(",\"dir\":").Append(Json.Str(LabHost.LabDirectory));
            json.Append(",\"request\":").Append(Json.Str(LabHost.RequestPath));
            json.Append(",\"results\":").Append(Json.Str(LabHost.ResultPath));
            json.Append('}');
            return json.ToString();
        }

        private static string Observe(string id)
        {
            Map map = RequireMap();
            StringBuilder json = new StringBuilder(2048);
            json.Append('{');
            json.Append("\"id\":").Append(Json.Str(id));
            json.Append(",\"ok\":true");
            json.Append(",\"lab\":").Append(Json.Str(LabHost.Version));
            json.Append(",\"protocol\":").Append(Json.Num(LabHost.ProtocolVersion));
            json.Append(",\"tick\":").Append(Json.Num(Find.TickManager.TicksGame));
            json.Append(",\"timeSpeed\":").Append(Json.Str(Find.TickManager.CurTimeSpeed.ToString()));
            json.Append(",\"map\":{");
            json.Append("\"uniqueId\":").Append(Json.Num(map.uniqueID));
            json.Append(",\"size\":").Append(Json.Cell(map.Size));
            json.Append(",\"isPlayerHome\":").Append(Json.Bool(map.IsPlayerHome));
            json.Append('}');

            Pawn selected = Find.Selector != null ? Find.Selector.SingleSelectedThing as Pawn : null;
            json.Append(",\"selected\":");
            if (selected == null)
            {
                json.Append("null");
            }
            else
            {
                AppendPawn(json, selected, detailed: true);
            }

            json.Append(",\"colonists\":[");
            bool first = true;
            int colonistCount = 0;
            int shown = 0;
            foreach (Thing thing in map.spawnedThings)
            {
                Pawn pawn = thing as Pawn;
                if (pawn == null || !pawn.IsColonistPlayerControlled)
                {
                    continue;
                }

                colonistCount++;
                if (shown >= MaxColonists)
                {
                    continue;
                }

                if (!first)
                {
                    json.Append(',');
                }

                first = false;
                shown++;
                AppendPawn(json, pawn, detailed: false);
            }

            json.Append(']');
            json.Append(",\"colonistCount\":").Append(Json.Num(colonistCount));
            json.Append(",\"wotfThings\":[");
            first = true;
            int wotfTotal = 0;
            int wotfShown = 0;
            foreach (Thing thing in map.spawnedThings)
            {
                if (thing == null || thing.def == null || thing.def.defName == null || !thing.def.defName.StartsWith("WotF_", StringComparison.Ordinal))
                {
                    continue;
                }

                wotfTotal++;
                if (wotfShown >= MaxExperimentThings)
                {
                    continue;
                }

                if (!first)
                {
                    json.Append(',');
                }

                first = false;
                wotfShown++;
                json.Append('{');
                json.Append("\"id\":").Append(Json.Num(thing.thingIDNumber));
                json.Append(",\"def\":").Append(Json.Str(thing.def.defName));
                json.Append(",\"pos\":").Append(Json.Cell(thing.Position));
                json.Append(",\"hp\":").Append(Json.Num(thing.HitPoints));
                json.Append('}');
            }

            json.Append(']');
            json.Append(",\"wotfThingCount\":").Append(Json.Num(wotfTotal));
            json.Append(",\"missiles\":").Append(MissileState(map));
            json.Append(",\"setup\":").Append(SetupState(map));
            json.Append(",\"behaviors\":").Append(LabBehaviorTracker.SnapshotAll());
            json.Append('}');
            return json.ToString();
        }

        private static string MissileState(Map map)
        {
            MapComponent_ArcaneMissiles component = map.GetComponent<MapComponent_ArcaneMissiles>();
            StringBuilder json = new StringBuilder(512);
            json.Append('{');
            json.Append("\"live\":").Append(Json.Num(component != null ? component.LiveMissileCount : 0));
            json.Append(",\"launched\":").Append(Json.Num(component != null ? component.TotalLaunched : 0));
            json.Append(",\"impacts\":").Append(Json.Num(component != null ? component.TotalImpacts : 0));
            json.Append(",\"expired\":").Append(Json.Num(component != null ? component.TotalExpired : 0));
            json.Append(",\"labFired\":").Append(Json.Num(labFiredMissiles));

            IReadOnlyList<ArcaneMissileLabFrame> frames = component != null ? component.GetLabFrames() : null;
            json.Append(",\"frameCount\":").Append(Json.Num(frames != null ? frames.Count : 0));
            json.Append(",\"frames\":[");
            int shown = 0;
            for (int i = 0; frames != null && i < frames.Count && shown < MaxObservedFrames; i++)
            {
                ArcaneMissileLabFrame frame = frames[i];
                if (shown > 0)
                {
                    json.Append(',');
                }

                json.Append('{');
                json.Append("\"x\":").Append(Json.Num(frame.X));
                json.Append(",\"z\":").Append(Json.Num(frame.Z));
                json.Append(",\"headingX\":").Append(Json.Num(frame.HeadingX));
                json.Append(",\"headingZ\":").Append(Json.Num(frame.HeadingZ));
                json.Append(",\"targetX\":").Append(Json.Num(frame.TargetX));
                json.Append(",\"targetZ\":").Append(Json.Num(frame.TargetZ));
                json.Append(",\"ageTicks\":").Append(Json.Num(frame.AgeTicks));
                json.Append(",\"targetThingId\":").Append(Json.Num(frame.TargetThingId));
                json.Append(",\"targetCell\":").Append(Json.Cell(frame.TargetCell));
                json.Append(",\"settings\":");
                AppendMissileSettings(json, frame.Settings);
                json.Append('}');
                shown++;
            }

            json.Append(']');
            json.Append('}');
            return json.ToString();
        }

        /// <summary>Effective numbers and both flight flags for one volley or Def preset.</summary>
        private static void AppendMissileSettings(StringBuilder json, ArcaneMissileSettings settings)
        {
            if (settings == null)
            {
                json.Append("null");
                return;
            }

            json.Append('{');
            json.Append("\"count\":").Append(Json.Num(settings.missileCount));
            json.Append(",\"speed\":").Append(Json.Num(settings.speedCellsPerSecond));
            json.Append(",\"turn\":").Append(Json.Num(settings.maxTurnDegreesPerSecond));
            json.Append(",\"wobble\":").Append(Json.Num(settings.wobbleAmplitude));
            json.Append(",\"hitRadius\":").Append(Json.Num(settings.hitRadius));
            json.Append(",\"lifetime\":").Append(Json.Num(settings.lifetimeSeconds));
            json.Append(",\"maxRange\":").Append(Json.Num(settings.maxRangeCells));
            json.Append(",\"expireOnTargetLost\":").Append(Json.Bool(settings.expireOnTargetLost));
            json.Append(",\"payload\":").Append(Json.Str(settings.payload != null ? settings.payload.GetType().Name : null));
            json.Append(",\"visualDef\":").Append(Json.Str(settings.visualDef != null ? settings.visualDef.defName : null));
            json.Append('}');
        }

        private static void AppendPawn(StringBuilder json, Pawn pawn, bool detailed)
        {
            json.Append('{');
            json.Append("\"id\":").Append(Json.Num(pawn.thingIDNumber));
            json.Append(",\"name\":").Append(Json.Str(pawn.Name != null ? pawn.Name.ToStringShort : pawn.LabelShort));
            json.Append(",\"def\":").Append(Json.Str(pawn.def != null ? pawn.def.defName : null));
            json.Append(",\"pos\":").Append(pawn.Spawned ? Json.Cell(pawn.Position) : "null");
            json.Append(",\"drafted\":").Append(Json.Bool(pawn.Drafted));
            json.Append(",\"dead\":").Append(Json.Bool(pawn.Dead));
            json.Append(",\"downed\":").Append(Json.Bool(pawn.Downed));
            json.Append(",\"job\":").Append(Json.Str(pawn.CurJobDef != null ? pawn.CurJobDef.defName : null));

            json.Append(",\"pather\":{");
            bool moving = pawn.pather != null && pawn.pather.Moving;
            json.Append("\"moving\":").Append(Json.Bool(moving));
            json.Append(",\"dest\":").Append(moving ? Json.Cell(pawn.pather.Destination.Cell) : "null");
            json.Append('}');

            Hediff_Psylink psylink = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PsychicAmplifier, false) as Hediff_Psylink;
            json.Append(",\"psylink\":").Append(Json.Num(psylink != null ? psylink.level : 0));
            json.Append(",\"psyfocus\":").Append(Json.Num(pawn.psychicEntropy != null ? pawn.psychicEntropy.CurrentPsyfocus : 0f));

            json.Append(",\"abilities\":[");
            List<Ability> abilities = pawn.abilities != null ? pawn.abilities.AllAbilitiesForReading : null;
            if (abilities != null)
            {
                int written = 0;
                for (int i = 0; i < abilities.Count && written < 16; i++)
                {
                    Ability ability = abilities[i];
                    if (ability == null || ability.def == null)
                    {
                        continue;
                    }

                    if (written > 0)
                    {
                        json.Append(',');
                    }

                    json.Append(Json.Str(ability.def.defName));
                    written++;
                }
            }

            json.Append(']');

            json.Append(",\"apparel\":[");
            int apparelWritten = 0;
            if (pawn.apparel != null)
            {
                foreach (Apparel apparel in pawn.apparel.WornApparel)
                {
                    if (apparel == null || apparel.def == null)
                    {
                        continue;
                    }

                    if (apparelWritten > 0)
                    {
                        json.Append(',');
                    }

                    json.Append(Json.Str(apparel.def.defName));
                    apparelWritten++;
                }
            }

            json.Append(']');

            if (detailed)
            {
                json.Append(",\"equipment\":").Append(Json.Str(pawn.equipment != null && pawn.equipment.Primary != null ? pawn.equipment.Primary.def.defName : null));
                json.Append(",\"hediffCount\":").Append(Json.Num(pawn.health.hediffSet.hediffs != null ? pawn.health.hediffSet.hediffs.Count : 0));
                json.Append(",\"faction\":").Append(Json.Str(pawn.Faction != null ? pawn.Faction.Name : null));
                json.Append(",\"moveSpeed\":").Append(Json.Num(StatExtension.GetStatValue(pawn, StatDefOf.MoveSpeed, true)));
                json.Append(",\"ticksPerMoveCardinal\":").Append(Json.Num(pawn.TicksPerMoveCardinal));
                UnityEngine.Vector3 drawPos = pawn.DrawPos;
                json.Append(",\"drawPos\":{\"x\":").Append(Json.Num(drawPos.x)).Append(",\"z\":").Append(Json.Num(drawPos.z)).Append('}');

                json.Append(",\"traits\":[");
                int traitWritten = 0;
                int traitTotal = 0;
                if (pawn.story != null && pawn.story.traits != null && pawn.story.traits.allTraits != null)
                {
                    foreach (Trait trait in pawn.story.traits.allTraits)
                    {
                        traitTotal++;
                        if (trait == null || trait.def == null || traitWritten >= MaxTraitEntries)
                        {
                            continue;
                        }

                        if (traitWritten > 0)
                        {
                            json.Append(',');
                        }

                        json.Append("{\"def\":").Append(Json.Str(trait.def.defName));
                        json.Append(",\"degree\":").Append(Json.Num(trait.Degree));
                        json.Append('}');
                        traitWritten++;
                    }
                }

                json.Append(']');
                json.Append(",\"traitCount\":").Append(Json.Num(traitTotal));

                json.Append(",\"hediffs\":[");
                int hediffWritten = 0;
                List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
                if (hediffs != null)
                {
                    foreach (Hediff hediff in hediffs)
                    {
                        if (hediff == null || hediff.def == null || hediffWritten >= MaxHediffEntries)
                        {
                            continue;
                        }

                        if (hediffWritten > 0)
                        {
                            json.Append(',');
                        }

                        json.Append("{\"def\":").Append(Json.Str(hediff.def.defName));
                        json.Append(",\"severity\":").Append(Json.Num(hediff.Severity));
                        json.Append(",\"permanent\":").Append(Json.Bool(HediffUtility.IsPermanent(hediff)));
                        json.Append(",\"part\":").Append(Json.Str(hediff.Part != null ? hediff.Part.def.defName : null));
                        json.Append('}');
                        hediffWritten++;
                    }
                }

                json.Append(']');

                int woundCount = 0;
                float woundSeverity = 0f;
                if (pawn.health.hediffSet.hediffs != null)
                {
                    foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                    {
                        if (hediff is Hediff_Injury)
                        {
                            woundCount++;
                            woundSeverity += hediff.Severity;
                        }
                    }
                }

                json.Append(",\"wounds\":{");
                json.Append("\"count\":").Append(Json.Num(woundCount));
                json.Append(",\"totalSeverity\":").Append(Json.Num(woundSeverity));
                json.Append('}');

                StrideSnapshot stride = StrideDiagnostics.GetSnapshot(pawn);
                json.Append(",\"stride\":{");
                json.Append("\"walked\":").Append(Json.Num(stride.WalkedCells));
                json.Append(",\"blinked\":").Append(Json.Num(stride.BlinkedCells));
                json.Append(",\"blocked\":").Append(Json.Num(stride.BlockedHops));
                json.Append(",\"terrainCellsFreed\":").Append(Json.Num(stride.TerrainCellsFreed));
                json.Append(",\"lastBlock\":").Append(Json.Str(stride.LastBlockReason));
                json.Append(",\"firstTick\":").Append(Json.Num(stride.FirstTick));
                json.Append(",\"lastTick\":").Append(Json.Num(stride.LastTick));
                json.Append(",\"terrainStride\":").Append(Json.Bool(StrideApparel.IsWearingTerrainStride(pawn)));
                json.Append(",\"blinkStride\":").Append(Json.Bool(StrideApparel.IsWearingBlinkStride(pawn)));
                json.Append('}');

                json.Append(",\"cell\":").Append(CellState(pawn.Map, pawn.Position, pawn));
            }

            json.Append('}');
        }

        private static string CellState(Map map, IntVec3 cell, Pawn pawn)
        {
            TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
            int pathCost = terrain != null ? terrain.pathCost : 0;
            bool stride = pawn != null && StrideApparel.IsWearingTerrainStride(pawn);
            StringBuilder builder = new StringBuilder(160);
            builder.Append('{');
            builder.Append("\"pos\":").Append(Json.Cell(cell));
            builder.Append(",\"terrain\":").Append(Json.Str(terrain != null ? terrain.defName : null));
            builder.Append(",\"pathCost\":").Append(Json.Num(pathCost));
            builder.Append(",\"terrainStride\":").Append(Json.Bool(stride));
            builder.Append(",\"effectiveTerrainCost\":").Append(Json.Num(stride ? 0 : pathCost));
            builder.Append(",\"standable\":").Append(Json.Bool(cell.Standable(map)));
            builder.Append('}');
            return builder.ToString();
        }

        private static string SelectPawn(string id, Dictionary<string, string> args)
        {
            Map map = RequireMap();
            int thingId = RequireInt(args, "id");
            Pawn pawn = FindPawn(map, thingId);
            if (pawn == null)
            {
                throw new LabCommandException("no spawned pawn with id " + thingId);
            }

            Find.Selector.ClearSelection();
            Find.Selector.Select(pawn, false, false);
            ExperimentLog.Record("lab", pawn, "selected via lab");
            return Ok(id, "\"selected\":" + Json.Num(pawn.thingIDNumber));
        }

        private static string SpawnSubject(string id, Dictionary<string, string> args)
        {
            Map map = RequireMap();
            IntVec3 cell;
            if (args.ContainsKey("x") && args.ContainsKey("z"))
            {
                cell = new IntVec3(RequireInt(args, "x"), 0, RequireInt(args, "z"));
                if (!cell.InBounds(map))
                {
                    throw new LabCommandException("cell out of bounds");
                }
            }
            else
            {
                cell = FindFreeCell(map);
            }

            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(ElfPawnKindDefName);
            if (kind == null)
            {
                Log.Error("[WotF-Lab] pawn kind " + ElfPawnKindDefName + " is missing; spawn skipped.");
                throw new LabCommandException("pawn kind " + ElfPawnKindDefName + " is missing");
            }

            Pawn pawn = PawnGenerator.GeneratePawn(kind, Faction.OfPlayer, null);
            if (pawn == null)
            {
                throw new LabCommandException("PawnGenerator returned null for " + kind.defName);
            }

            GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);
            pawn.Name = new NameSingle("Lab Subject " + pawn.thingIDNumber);
            Find.Selector.ClearSelection();
            Find.Selector.Select(pawn, false, false);
            ExperimentLog.Record("lab", pawn, "spawned experiment subject kind=" + kind.defName + " at " + cell);
            return Ok(id, "\"pawn\":{" + "\"id\":" + Json.Num(pawn.thingIDNumber) + ",\"name\":" + Json.Str(pawn.Name.ToStringShort) + ",\"def\":" + Json.Str(pawn.def.defName) + ",\"pos\":" + Json.Cell(pawn.Position) + "}");
        }

        private static string Grant(string id)
        {
            Pawn pawn = RequireSelectedPawn();
            bool ok = LabActions.TryGrantPsylink(pawn);
            return ActionResult(id, ok, "grant-psylink", pawn);
        }

        private static string Equip(string id, Dictionary<string, string> args)
        {
            Pawn pawn = RequireSelectedPawn();
            string which = Get(args, "which", "terrain").ToLowerInvariant();
            bool ok;
            string action;
            if (which == "terrain")
            {
                ok = LabActions.TryEquipTerrainStride(pawn);
                action = "equip-terrain";
            }
            else if (which == "blink")
            {
                ok = LabActions.TryEquipBlinkStride(pawn);
                action = "equip-blink";
            }
            else
            {
                throw new LabCommandException("which must be 'terrain' or 'blink'");
            }

            return ActionResult(id, ok, action, pawn);
        }

        private static string Unequip(string id)
        {
            Pawn pawn = RequireSelectedPawn();
            bool ok = LabActions.TryRemoveExperimentApparel(pawn);
            return ActionResult(id, ok, "unequip", pawn);
        }

        private static string Move(string id, Dictionary<string, string> args)
        {
            Map map = RequireMap();
            Pawn pawn = RequireSelectedPawn();
            IntVec3 cell = new IntVec3(RequireInt(args, "x"), 0, RequireInt(args, "z"));
            if (!cell.InBounds(map))
            {
                throw new LabCommandException("destination out of bounds");
            }

            bool canReach = CanReach(map, pawn, cell, TraverseMode.PassDoors);
            if (pawn.CurJobDef == JobDefOf.Goto && pawn.CurJob != null && pawn.CurJob.targetA.Cell == cell)
            {
                LogOrderedMove(pawn, cell, canReach, true);
                return Ok(id, "\"dest\":" + Json.Cell(cell) + ",\"canReach\":" + Json.Bool(canReach) +
                    ",\"queued\":true,\"already\":true,\"drafted\":" + Json.Bool(pawn.Drafted) +
                    ",\"job\":" + Json.Str(pawn.CurJobDef.defName));
            }

            // Match vanilla FloatMenuOptionProvider_DraftedMove: a Goto job works while drafted and
            // is not overwritten by the think tree the way a bare pather.StartPath call is.
            Job job = JobMaker.MakeJob(JobDefOf.Goto, new LocalTargetInfo(cell));
            bool queued = pawn.jobs.TryTakeOrderedJob(job, null, false);
            LogOrderedMove(pawn, cell, canReach, queued);

            return Ok(id, "\"dest\":" + Json.Cell(cell) + ",\"canReach\":" + Json.Bool(canReach) +
                ",\"queued\":" + Json.Bool(queued) + ",\"drafted\":" + Json.Bool(pawn.Drafted) +
                ",\"job\":" + Json.Str(pawn.CurJobDef != null ? pawn.CurJobDef.defName : null));
        }

        private static void LogOrderedMove(Pawn pawn, IntVec3 cell, bool canReach, bool queued)
        {
            ExperimentLog.Record(
                "lab",
                pawn,
                "orderedGoto dest=" + cell + " canReach=" + canReach + " queued=" + queued + " drafted=" + pawn.Drafted);
        }

        private static bool CanReach(Map map, Pawn pawn, IntVec3 cell, TraverseMode mode)
        {
            try
            {
                TraverseParms parms = TraverseParms.For(pawn, Danger.Deadly, mode, false, false, false, false);
                return map.reachability.CanReach(pawn.Position, new LocalTargetInfo(cell), PathEndMode.OnCell, parms);
            }
            catch
            {
                return false;
            }
        }

        private static string Draft(string id, Dictionary<string, string> args)
        {
            Pawn pawn = RequireSelectedPawn();
            bool value = Get(args, "value", "1") != "0";
            pawn.drafter.Drafted = value;
            ExperimentLog.Record("lab", pawn, "draft=" + value);
            return Ok(id, "\"drafted\":" + Json.Bool(pawn.Drafted));
        }

        private static string Sow(string id, Dictionary<string, string> args)
        {
            Map map = RequireMap();
            Pawn pawn = RequireSelectedPawn();
            IntVec3 cell = new IntVec3(RequireInt(args, "x"), 0, RequireInt(args, "z"));
            if (!cell.InBounds(map))
            {
                throw new LabCommandException("sow cell out of bounds");
            }

            if (pawn.Drafted)
            {
                pawn.drafter.Drafted = false;
            }

            IPlantToGrowSettable settable = cell.GetPlantToGrowSettable(map);
            ThingDef crop = settable != null ? settable.GetPlantDefToGrow() : null;
            if (crop == null)
            {
                throw new LabCommandException("sow cell has no growing zone/crop; create a growing zone with a selected plant first");
            }

            Job job = JobMaker.MakeJob(JobDefOf.Sow, new LocalTargetInfo(cell));
            // 1.7 JobDriver_PlantSow reads job.plantDefToSow in MakeNewToils; WorkGiver normally sets it.
            job.plantDefToSow = crop;
            bool queued = pawn.jobs.TryTakeOrderedJob(job, null, false);
            ExperimentLog.Record("lab", pawn, "sow ordered at " + cell + " crop=" + crop.defName + " queued=" + queued);
            if (!queued)
            {
                throw new LabCommandException("pawn refused Sow job");
            }

            return Ok(id, "\"cell\":" + Json.Cell(cell) + ",\"crop\":" + Json.Str(crop.defName) +
                ",\"job\":" + Json.Str(pawn.CurJobDef != null ? pawn.CurJobDef.defName : null));
        }

        private static string Attack(string id, Dictionary<string, string> args)
        {
            Map map = RequireMap();
            Pawn pawn = RequireSelectedPawn();
            int targetId = RequireInt(args, "id");
            Thing target = FindThing(map, targetId);
            if (target == null)
            {
                throw new LabCommandException("no spawned thing with id " + targetId);
            }

            pawn.drafter.Drafted = true;
            bool queued = pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.AttackStatic, target), null, false);
            ExperimentLog.Record("lab", pawn, "attack ordered target=" + target.def.defName + "#" + targetId + " queued=" + queued);
            if (!queued)
            {
                throw new LabCommandException("pawn refused AttackStatic job");
            }

            return Ok(id, "\"target\":" + Json.Num(targetId) + ",\"job\":" + Json.Str(pawn.CurJobDef != null ? pawn.CurJobDef.defName : null));
        }

        private static string MissileStats(string id, Dictionary<string, string> args)
        {
            Map map = RequireMap();
            MapComponent_ArcaneMissiles component = map.GetComponent<MapComponent_ArcaneMissiles>();
            bool reset = Get(args, "reset", "0") == "1";
            if (reset)
            {
                if (component != null)
                {
                    component.ResetLabCounters();
                }

                labFiredMissiles = 0;
            }

            return Ok(id, "\"reset\":" + Json.Bool(reset) + ",\"missiles\":" + MissileState(map));
        }

        private static LocalTargetInfo ResolveTarget(Map map, Dictionary<string, string> args, string command)
        {
            if (args.ContainsKey("id"))
            {
                Thing thing = FindThing(map, RequireInt(args, "id"));
                if (thing == null)
                {
                    throw new LabCommandException("no spawned thing for " + command + " target");
                }

                return new LocalTargetInfo(thing);
            }

            if (args.ContainsKey("x") && args.ContainsKey("z"))
            {
                IntVec3 cell = new IntVec3(RequireInt(args, "x"), 0, RequireInt(args, "z"));
                if (!cell.InBounds(map))
                {
                    throw new LabCommandException(command + " cell out of bounds");
                }

                return new LocalTargetInfo(cell);
            }

            throw new LabCommandException(command + " needs id=<thingId> or x=<int> z=<int>");
        }

        private static string PathInfo(string id, Dictionary<string, string> args)
        {
            Map map = RequireMap();
            Pawn pawn = RequireSelectedPawn();
            IntVec3 cell = new IntVec3(RequireInt(args, "x"), 0, RequireInt(args, "z"));
            if (!cell.InBounds(map))
            {
                throw new LabCommandException("path cell out of bounds");
            }

            Building_Door door = null;
            foreach (Thing thing in map.thingGrid.ThingsListAt(cell))
            {
                door = thing as Building_Door;
                if (door != null)
                {
                    break;
                }
            }

            StringBuilder json = new StringBuilder(320);
            json.Append("\"canReach\":").Append(Json.Bool(CanReach(map, pawn, cell, TraverseMode.PassDoors)));
            json.Append(",\"canReachNoClosedDoors\":").Append(Json.Bool(CanReach(map, pawn, cell, TraverseMode.NoPassClosedDoors)));
            json.Append(",\"cell\":").Append(CellState(map, cell, pawn));
            json.Append(",\"door\":");
            if (door == null)
            {
                json.Append("null");
            }
            else
            {
                json.Append('{');
                json.Append("\"id\":").Append(Json.Num(door.thingIDNumber));
                json.Append(",\"open\":").Append(Json.Bool(door.Open));
                json.Append(",\"blocksPawn\":").Append(Json.Bool(door.BlocksPawn(pawn)));
                json.Append('}');
            }

            json.Append(",\"pawnPos\":").Append(Json.Cell(pawn.Position));
            json.Append(",\"job\":").Append(Json.Str(pawn.CurJobDef != null ? pawn.CurJobDef.defName : null));
            return Ok(id, json.ToString());
        }

        private static string Setup(string id, Dictionary<string, string> args)
        {
            Map map = RequireMap();
            if (Get(args, "confirm", "0") != "1")
            {
                throw new LabCommandException("setup mutates the current map; pass confirm=1 and use only a disposable lab/quicktest map");
            }

            SetupRecord existing;
            if (Setups.TryGetValue(map.uniqueID, out existing))
            {
                return Ok(id, "\"reused\":true,\"setup\":" + existing.Json + ",\"current\":" + SetupState(map));
            }

            const int width = 15;
            const int height = 5;
            IntVec3 origin = IntVec3.Invalid;
            for (int dx = -24; dx <= 24 && !origin.IsValid; dx += 4)
            {
                for (int dz = -12; dz <= 12; dz += 2)
                {
                    IntVec3 candidate = new IntVec3(map.Center.x + dx, 0, map.Center.z + dz);
                    if (RectClear(map, candidate, width, height))
                    {
                        origin = candidate;
                        break;
                    }
                }
            }

            if (!origin.IsValid)
            {
                throw new LabCommandException("no clear 15x5 rectangle near map center; move the camera/pawns or use a fresh quicktest map");
            }

            int[] cleared = ClearPlantsAndFilth(map, origin, width, height);

            TerrainDef normal = TerrainDefOf.Concrete;
            TerrainDef mud = TerrainDefOf.Mud;
            if (normal == null || mud == null)
            {
                throw new LabCommandException("TerrainDefOf.Concrete or Mud is not loaded");
            }

            int laneZ = origin.z + 2;
            for (int x = 0; x <= 7; x++)
            {
                map.terrainGrid.SetTerrain(new IntVec3(origin.x + x, 0, laneZ), normal);
            }

            for (int x = 8; x <= 11; x++)
            {
                map.terrainGrid.SetTerrain(new IntVec3(origin.x + x, 0, laneZ), mud);
            }

            List<Thing> walls = new List<Thing>();
            int[][] wallOffsets =
            {
                new[] { 12, -1 }, new[] { 12, 1 },
                new[] { 13, -1 }, new[] { 13, 0 }, new[] { 13, 1 },
                new[] { 9, 1 }
            };

            foreach (int[] offset in wallOffsets)
            {
                IntVec3 cell = new IntVec3(origin.x + offset[0], 0, laneZ + offset[1]);
                walls.Add(SpawnWall(map, cell));
            }

            IntVec3 doorCell = new IntVec3(origin.x + 12, 0, laneZ);
            Building_Door door = SpawnDoor(map, doorCell);
            if (door.Open)
            {
                door.StartManualCloseBy(null);
            }

            Pawn human = SpawnSetupPawn(map, PawnKindDefOf.Colonist, new IntVec3(origin.x + 1, 0, laneZ), "Lab Human");
            PawnKindDef elfKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(ElfPawnKindDefName);
            Pawn elf = null;
            if (elfKind == null)
            {
                Log.Error("[WotF-Lab] pawn kind " + ElfPawnKindDefName + " is missing; setup skips the elf.");
            }
            else
            {
                elf = SpawnSetupPawn(map, elfKind, new IntVec3(origin.x + 2, 0, laneZ), "Lab Elf");
            }

            StringBuilder setup = new StringBuilder(512);
            setup.Append('{');
            setup.Append("\"origin\":").Append(Json.Cell(origin));
            setup.Append(",\"lane\":{");
            setup.Append("\"z\":").Append(Json.Num(laneZ));
            setup.Append(",\"normalFrom\":").Append(Json.Num(origin.x));
            setup.Append(",\"normalTo\":").Append(Json.Num(origin.x + 7));
            setup.Append(",\"mudFrom\":").Append(Json.Num(origin.x + 8));
            setup.Append(",\"mudTo\":").Append(Json.Num(origin.x + 11));
            setup.Append(",\"doorX\":").Append(Json.Num(origin.x + 12));
            setup.Append('}');

            setup.Append(",\"pawns\":[");
            AppendSetupPawn(setup, "human", human);
            if (elf != null)
            {
                setup.Append(',');
                AppendSetupPawn(setup, "elf", elf);
            }

            setup.Append(']');

            setup.Append(",\"walls\":[");
            for (int i = 0; i < walls.Count; i++)
            {
                if (i > 0)
                {
                    setup.Append(',');
                }

                Thing wall = walls[i];
                setup.Append('{');
                setup.Append("\"id\":").Append(Json.Num(wall.thingIDNumber));
                setup.Append(",\"pos\":").Append(Json.Cell(wall.Position));
                setup.Append('}');
            }

            setup.Append(']');
            setup.Append(",\"door\":{");
            setup.Append("\"id\":").Append(Json.Num(door.thingIDNumber));
            setup.Append(",\"pos\":").Append(Json.Cell(door.Position));
            setup.Append(",\"open\":").Append(Json.Bool(door.Open));
            setup.Append('}');
            setup.Append(",\"terrain\":{\"normal\":").Append(Json.Str(normal.defName));
            setup.Append(",\"mud\":").Append(Json.Str(mud.defName)).Append('}');
            setup.Append(",\"cleared\":{\"plants\":").Append(Json.Num(cleared[0]));
            setup.Append(",\"filth\":").Append(Json.Num(cleared[1])).Append('}');
            setup.Append(",\"note\":").Append(Json.Str("mutates current map; disposable lab/quicktest map only"));
            setup.Append('}');

            string json = setup.ToString();
            SetupRecord record = new SetupRecord
            {
                Json = json,
                Origin = origin,
                LaneZ = laneZ,
                DoorId = door.thingIDNumber,
                ClearedPlants = cleared[0],
                ClearedFilth = cleared[1]
            };
            foreach (Thing wall in walls)
            {
                record.WallIds.Add(wall.thingIDNumber);
            }

            record.PawnIds.Add(human.thingIDNumber);
            if (elf != null)
            {
                record.PawnIds.Add(elf.thingIDNumber);
            }
            Setups[map.uniqueID] = record;
            map.reachability.ClearCache();
            ExperimentLog.Record("lab", human, "setup lane origin=" + origin + " laneZ=" + laneZ + " door=" + door.thingIDNumber +
                " clearedPlants=" + cleared[0] + " clearedFilth=" + cleared[1]);
            return Ok(id, "\"reused\":false,\"setup\":" + json + ",\"current\":" + SetupState(map));
        }

        private static string Behaviors(string id, Dictionary<string, string> args)
        {
            Map map = RequireMap();
            if (Get(args, "confirm", "0") != "1")
            {
                throw new LabCommandException("behaviors prepares the disposable setup fixture; pass confirm=1");
            }

            SetupRecord setup;
            if (!Setups.TryGetValue(map.uniqueID, out setup))
            {
                throw new LabCommandException("run setup confirm=1 first");
            }

            BehaviorRecord existing;
            if (BehaviorFixtures.TryGetValue(map.uniqueID, out existing))
            {
                return Ok(id, "\"reused\":true,\"fixture\":" + BehaviorState(map, existing, true));
            }

            if (setup.PawnIds.Count < 2)
            {
                throw new LabCommandException("setup record has no human/elf pawn IDs");
            }

            Pawn human = FindPawn(map, setup.PawnIds[0]);
            Pawn elf = FindPawn(map, setup.PawnIds[1]);
            if (human == null || elf == null)
            {
                throw new LabCommandException("setup pawns are no longer spawned; rerun setup");
            }

            ThingWithComps weapon = human.equipment != null ? human.equipment.Primary : null;
            if (weapon == null)
            {
                ThingDef weaponDef = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_BoltActionRifle")
                    ?? DefDatabase<ThingDef>.GetNamedSilentFail("Gun_Revolver");
                if (weaponDef == null)
                {
                    throw new LabCommandException("no vanilla ranged weapon def (Gun_BoltActionRifle/Gun_Revolver) is loaded");
                }

                weapon = ThingMaker.MakeThing(weaponDef, null) as ThingWithComps;
                if (weapon == null)
                {
                    throw new LabCommandException("could not create ranged weapon " + weaponDef.defName);
                }

                human.equipment.AddEquipment(weapon);
            }
            else if (weapon.def.Verbs == null || weapon.def.Verbs.Count == 0)
            {
                throw new LabCommandException("setup human already carries non-ranged equipment; rerun setup for an empty test pawn");
            }

            ThingDef crop = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Potato") ?? ThingDefOf.Plant_Potato;
            TerrainDef soil = TerrainDefOf.Soil;
            if (crop == null || soil == null)
            {
                throw new LabCommandException("Plant_Potato or TerrainDefOf.Soil is not loaded");
            }

            IntVec3 zoneOrigin = new IntVec3(setup.Origin.x + 1, 0, setup.Origin.z + 4);
            Zone_Growing zone = new Zone_Growing(map.zoneManager);
            if (!map.zoneManager.AllZones.Contains(zone))
            {
                map.zoneManager.RegisterZone(zone);
            }

            for (int x = 0; x < 3; x++)
            {
                IntVec3 cell = new IntVec3(zoneOrigin.x + x, 0, zoneOrigin.z);
                if (cell.GetZone(map) != null)
                {
                    throw new LabCommandException("zone cell already belongs to a zone: " + cell + "; rerun setup on a fresh map");
                }

                map.terrainGrid.SetTerrain(cell, soil);
                zone.AddCell(cell);
            }

            zone.SetPlantDefToGrow(crop);

            IntVec3 targetCell = IntVec3.Invalid;
            for (int x = 5; x <= 10 && !targetCell.IsValid; x++)
            {
                IntVec3 candidate = new IntVec3(setup.Origin.x + x, 0, setup.LaneZ - 2);
                if (candidate.InBounds(map) && candidate.Standable(map) && map.thingGrid.ThingsListAt(candidate).Count == 0)
                {
                    targetCell = candidate;
                }
            }

            if (!targetCell.IsValid)
            {
                throw new LabCommandException("no free cell for the fixed non-pawn target wall");
            }

            Thing target = SpawnWall(map, targetCell);
            BehaviorRecord record = new BehaviorRecord
            {
                HumanId = human.thingIDNumber,
                ElfId = elf.thingIDNumber,
                Zone = zone,
                ZoneCells = new List<IntVec3>
                {
                    new IntVec3(zoneOrigin.x, 0, zoneOrigin.z),
                    new IntVec3(zoneOrigin.x + 1, 0, zoneOrigin.z),
                    new IntVec3(zoneOrigin.x + 2, 0, zoneOrigin.z)
                },
                CropDefName = crop.defName,
                WeaponDefName = weapon.def.defName,
                WeaponId = weapon.thingIDNumber,
                TargetId = target.thingIDNumber
            };
            BehaviorFixtures[map.uniqueID] = record;
            LabBehaviorTracker.RegisterPrepared(human);
            LabBehaviorTracker.RegisterPrepared(elf);
            map.reachability.ClearCache();
            ExperimentLog.Record(
                "lab",
                human,
                "behaviors fixture human=" + human.thingIDNumber + " elf=" + elf.thingIDNumber +
                " weapon=" + weapon.def.defName + " target=" + target.thingIDNumber +
                " crop=" + crop.defName + " zone=" + zoneOrigin);

            return Ok(id, "\"reused\":false,\"fixture\":" + BehaviorState(map, record, false));
        }

        private static string BehaviorState(Map map, BehaviorRecord record, bool reused)
        {
            Pawn human = FindPawn(map, record.HumanId);
            Pawn elf = FindPawn(map, record.ElfId);
            ThingWithComps weapon = human != null && human.equipment != null ? human.equipment.Primary : null;
            StringBuilder json = new StringBuilder(512);
            json.Append('{');
            json.Append("\"reused\":").Append(Json.Bool(reused));
            json.Append(",\"human\":").Append(ThingState(map, record.HumanId));
            json.Append(",\"elf\":").Append(ThingState(map, record.ElfId));
            json.Append(",\"weapon\":");
            if (weapon == null)
            {
                json.Append("null");
            }
            else
            {
                json.Append('{');
                json.Append("\"id\":").Append(Json.Num(weapon.thingIDNumber));
                json.Append(",\"def\":").Append(Json.Str(weapon.def.defName));
                json.Append(",\"hp\":").Append(Json.Num(weapon.HitPoints));
                json.Append('}');
            }

            json.Append(",\"zone\":{");
            json.Append("\"present\":").Append(Json.Bool(record.Zone != null && map.zoneManager.AllZones.Contains(record.Zone)));
            json.Append(",\"crop\":").Append(Json.Str(record.CropDefName));
            json.Append(",\"cells\":[");
            for (int i = 0; i < record.ZoneCells.Count; i++)
            {
                if (i > 0)
                {
                    json.Append(',');
                }

                json.Append(Json.Cell(record.ZoneCells[i]));
            }

            json.Append("]}");
            json.Append(",\"target\":").Append(ThingState(map, record.TargetId));
            json.Append(",\"behaviors\":").Append(LabBehaviorTracker.SnapshotForPawn(human));
            json.Append('}');
            return json.ToString();
        }

        /// <summary>
        /// Bounded plant view: the shortest path between a real cast and what it did.
        /// No arguments lists up to 16 immature plants plus counts, <c>id=</c> reports one target
        /// (plant or not), and <c>confirm=1</c> plants one fixture inside the registered setup
        /// rectangle. Growth is only ever read here; maturing must come from the ability itself.
        /// </summary>
        private static string Plants(string id, Dictionary<string, string> args)
        {
            Map map = RequireMap();
            if (args.ContainsKey("id"))
            {
                return Ok(id, "\"plant\":" + PlantState(FindThing(map, RequireInt(args, "id"))));
            }

            if (Get(args, "confirm", "0") == "1")
            {
                return PlantsFixture(id, map, args);
            }

            List<Plant> immature = new List<Plant>();
            int plantCount = 0;
            foreach (Thing thing in map.spawnedThings)
            {
                if (!(thing is Plant plant) || plant.Destroyed)
                {
                    continue;
                }

                plantCount++;
                if (plant.LifeStage != PlantLifeStage.Mature)
                {
                    immature.Add(plant);
                }
            }

            immature.Sort((a, b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
            StringBuilder json = new StringBuilder(1024);
            json.Append("\"plantCount\":").Append(Json.Num(plantCount));
            json.Append(",\"immatureCount\":").Append(Json.Num(immature.Count));
            json.Append(",\"shown\":").Append(Json.Num(Math.Min(immature.Count, MaxObservedPlants)));
            json.Append(",\"plants\":[");
            for (int i = 0; i < immature.Count && i < MaxObservedPlants; i++)
            {
                if (i > 0)
                {
                    json.Append(',');
                }

                json.Append(PlantState(immature[i]));
            }

            json.Append(']');
            return Ok(id, json.ToString());
        }

        /// <summary>
        /// One plant fixture for the isolated lab map: a real vanilla plant spawned at growth 0 so the
        /// only way to see it mature is a real cast. Reused for the same map and def.
        /// </summary>
        private static string PlantsFixture(string id, Map map, Dictionary<string, string> args)
        {
            SetupRecord setup;
            if (!Setups.TryGetValue(map.uniqueID, out setup))
            {
                throw new LabCommandException("run setup confirm=1 first");
            }

            string defName = Get(args, "kind", null) == "tree" ? "Plant_TreeOak" : "Plant_Potato";
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null || def.plant == null)
            {
                throw new LabCommandException("plant def " + defName + " is not loaded");
            }

            string key = map.uniqueID + ":" + def.defName;
            int existingId;
            if (PlantFixtures.TryGetValue(key, out existingId))
            {
                Plant existing = FindThing(map, existingId) as Plant;
                if (existing != null && !existing.Destroyed)
                {
                    return Ok(id, "\"reused\":true,\"def\":" + Json.Str(def.defName) + ",\"plant\":" + PlantState(existing));
                }
            }

            IntVec3 cell = IntVec3.Invalid;
            for (int z = 0; z < 5 && !cell.IsValid; z++)
            {
                for (int x = 0; x < 15; x++)
                {
                    IntVec3 candidate = new IntVec3(setup.Origin.x + x, 0, setup.Origin.z + z);
                    if (candidate.InBounds(map) && candidate.Standable(map) &&
                        map.thingGrid.ThingsListAt(candidate).Count == 0)
                    {
                        cell = candidate;
                        break;
                    }
                }
            }

            if (!cell.IsValid)
            {
                throw new LabCommandException("no free cell for a plant fixture inside the setup rectangle");
            }

            Plant plant = ThingMaker.MakeThing(def, null) as Plant;
            if (plant == null)
            {
                throw new LabCommandException(defName + " did not create a Plant");
            }

            GenSpawn.Spawn(plant, cell, map, Rot4.North, WipeMode.Vanish);
            PlantFixtures[key] = plant.thingIDNumber;
            ExperimentLog.Record("lab", null, "plants fixture def=" + def.defName + " id=" + plant.thingIDNumber + " cell=" + cell);
            return Ok(id, "\"reused\":false,\"def\":" + Json.Str(def.defName) + ",\"plant\":" + PlantState(plant));
        }

        /// <summary>Growth/stage/harvest state of one thing; a non-plant reports <c>isPlant:false</c>.</summary>
        private static string PlantState(Thing thing)
        {
            StringBuilder json = new StringBuilder(256);
            json.Append('{');
            if (thing == null)
            {
                json.Append("\"present\":false");
                json.Append('}');
                return json.ToString();
            }

            json.Append("\"present\":true");
            json.Append(",\"id\":").Append(Json.Num(thing.thingIDNumber));
            json.Append(",\"def\":").Append(Json.Str(thing.def != null ? thing.def.defName : null));
            json.Append(",\"pos\":").Append(Json.Cell(thing.Position));
            json.Append(",\"hp\":").Append(Json.Num(thing.HitPoints));
            Plant plant = thing as Plant;
            if (plant == null)
            {
                json.Append(",\"isPlant\":false");
                json.Append('}');
                return json.ToString();
            }

            json.Append(",\"isPlant\":true");
            json.Append(",\"growth\":").Append(Json.Num(plant.Growth));
            json.Append(",\"lifeStage\":").Append(Json.Str(plant.LifeStage.ToString()));
            json.Append(",\"harvestableNow\":").Append(Json.Bool(plant.HarvestableNow));
            json.Append(",\"isTree\":").Append(Json.Bool(plant.def.plant.IsTree));
            json.Append(",\"sown\":").Append(Json.Bool(plant.sown));
            json.Append('}');
            return json.ToString();
        }

        private static string SetupState(Map map)
        {
            SetupRecord record;
            if (!Setups.TryGetValue(map.uniqueID, out record))
            {
                return "{\"present\":false}";
            }

            StringBuilder json = new StringBuilder(512);
            json.Append('{');
            json.Append("\"present\":true");
            json.Append(",\"origin\":").Append(Json.Cell(record.Origin));
            json.Append(",\"laneZ\":").Append(Json.Num(record.LaneZ));
            json.Append(",\"cleared\":{\"plants\":").Append(Json.Num(record.ClearedPlants));
            json.Append(",\"filth\":").Append(Json.Num(record.ClearedFilth)).Append('}');
            json.Append(",\"door\":").Append(ThingState(map, record.DoorId));
            json.Append(",\"walls\":[");
            for (int i = 0; i < record.WallIds.Count; i++)
            {
                if (i > 0)
                {
                    json.Append(',');
                }

                json.Append(ThingState(map, record.WallIds[i]));
            }

            json.Append(']');
            json.Append(",\"pawns\":[");
            for (int i = 0; i < record.PawnIds.Count; i++)
            {
                if (i > 0)
                {
                    json.Append(',');
                }

                json.Append(ThingState(map, record.PawnIds[i]));
            }

            json.Append(']');
            json.Append('}');
            return json.ToString();
        }

        private static string ThingState(Map map, int thingId)
        {
            Thing thing = FindThing(map, thingId);
            StringBuilder json = new StringBuilder(128);
            json.Append('{');
            json.Append("\"id\":").Append(Json.Num(thingId));
            if (thing == null)
            {
                json.Append(",\"present\":false");
                json.Append('}');
                return json.ToString();
            }

            json.Append(",\"present\":true");
            json.Append(",\"def\":").Append(Json.Str(thing.def.defName));
            json.Append(",\"pos\":").Append(Json.Cell(thing.Position));
            json.Append(",\"hp\":").Append(Json.Num(thing.HitPoints));
            json.Append(",\"maxHp\":").Append(Json.Num(thing.MaxHitPoints));
            if (thing is Pawn pawn)
            {
                json.Append(",\"name\":").Append(Json.Str(pawn.Name != null ? pawn.Name.ToStringShort : pawn.LabelShort));
            }

            json.Append('}');
            return json.ToString();
        }

        private static void AppendSetupPawn(StringBuilder json, string role, Pawn pawn)
        {
            json.Append('{');
            json.Append("\"role\":").Append(Json.Str(role));
            json.Append(",\"id\":").Append(Json.Num(pawn.thingIDNumber));
            json.Append(",\"def\":").Append(Json.Str(pawn.def.defName));
            json.Append(",\"pos\":").Append(Json.Cell(pawn.Position));
            json.Append('}');
        }

        private static bool RectClear(Map map, IntVec3 origin, int width, int height)
        {
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    IntVec3 cell = new IntVec3(origin.x + x, 0, origin.z + z);
                    if (!cell.InBounds(map))
                    {
                        return false;
                    }

                    TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
                    if (terrain == null || terrain.passability == Traversability.Impassable)
                    {
                        return false;
                    }

                    foreach (Thing thing in map.thingGrid.ThingsListAt(cell))
                    {
                        // Natural vegetation and filth are allowed in the disposable lab map;
                        // setup clears them only inside this rectangle. Anything else is a hard no.
                        if (thing is Plant || thing is Filth)
                        {
                            continue;
                        }

                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Removes only wild plants and filth inside the requested rectangle.</summary>
        private static int[] ClearPlantsAndFilth(Map map, IntVec3 origin, int width, int height)
        {
            int plants = 0;
            int filth = 0;
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    IntVec3 cell = new IntVec3(origin.x + x, 0, origin.z + z);
                    foreach (Thing thing in map.thingGrid.ThingsListAt(cell).ToList())
                    {
                        if (thing is Plant)
                        {
                            thing.Destroy(DestroyMode.Vanish);
                            plants++;
                        }
                        else if (thing is Filth)
                        {
                            thing.Destroy(DestroyMode.Vanish);
                            filth++;
                        }
                    }
                }
            }

            return new[] { plants, filth };
        }

        private static Thing SpawnWall(Map map, IntVec3 cell)
        {
            Thing wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog);
            GenSpawn.Spawn(wall, cell, map, Rot4.North, WipeMode.Vanish);
            return wall;
        }

        private static Building_Door SpawnDoor(Map map, IntVec3 cell)
        {
            Thing thing = ThingMaker.MakeThing(ThingDefOf.Door, ThingDefOf.WoodLog);
            GenSpawn.Spawn(thing, cell, map, Rot4.North, WipeMode.Vanish);
            Building_Door door = thing as Building_Door;
            if (door == null)
            {
                throw new LabCommandException("ThingDefOf.Door did not create Building_Door");
            }

            return door;
        }

        private static Pawn SpawnSetupPawn(Map map, PawnKindDef kind, IntVec3 cell, string name)
        {
            Pawn pawn = PawnGenerator.GeneratePawn(kind, Faction.OfPlayer, null);
            if (pawn == null)
            {
                throw new LabCommandException("PawnGenerator returned null for " + kind.defName);
            }

            GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);
            pawn.Name = new NameSingle(name);
            return pawn;
        }

        private static string Warp(string id, Dictionary<string, string> args)
        {
            Map map = RequireMap();
            Pawn pawn = RequireSelectedPawn();
            IntVec3 cell = new IntVec3(RequireInt(args, "x"), 0, RequireInt(args, "z"));
            if (!cell.InBounds(map))
            {
                throw new LabCommandException("warp cell out of bounds");
            }

            if (!cell.Standable(map))
            {
                throw new LabCommandException("warp cell is not standable: " + cell);
            }

            if (cell.GetFirstPawn(map) != null)
            {
                throw new LabCommandException("warp cell is occupied by a pawn: " + cell);
            }

            IntVec3 old = pawn.Position;
            pawn.Position = cell;
            pawn.Notify_Teleported(true, true);
            ExperimentLog.Record("lab", pawn, "warped from " + old + " to " + cell);
            return Ok(id, "\"from\":" + Json.Cell(old) + ",\"to\":" + Json.Cell(pawn.Position));
        }

        private static string Hurt(string id, Dictionary<string, string> args)
        {
            Pawn pawn = RequireSelectedPawn();
            float amount = args.ContainsKey("amount") ? RequireFloat(args, "amount") : 2f;
            amount = Math.Max(0.1f, Math.Min(4f, amount));

            IEnumerable<BodyPartRecord> parts = pawn.health.hediffSet.GetNotMissingParts();
            BodyPartRecord part = parts.FirstOrDefault(p => p.def.defName == "LeftArm")
                ?? parts.FirstOrDefault(p => p.def.defName == "RightArm")
                ?? parts.FirstOrDefault(p => p.def.defName == "LeftLeg")
                ?? parts.FirstOrDefault(p => p.def.defName == "RightLeg")
                ?? parts.FirstOrDefault(p => p.coverageAbs > 0f && p.def.defName != "Brain" && p.def.defName != "Heart");

            if (part == null)
            {
                throw new LabCommandException("no suitable non-vital body part for a test wound");
            }

            Hediff injury = HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, part);
            injury.Severity = amount;
            pawn.health.AddHediff(injury, part, null, null);

            float total = 0f;
            int count = 0;
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_Injury)
                {
                    count++;
                    total += hediff.Severity;
                }
            }

            ExperimentLog.Record("lab", pawn, "hurt part=" + part.def.defName + " severity=" + amount + " total=" + total);
            return Ok(id, "\"part\":" + Json.Str(part.def.defName) + ",\"severity\":" + Json.Num(amount) +
                ",\"wounds\":{\"count\":" + Json.Num(count) + ",\"totalSeverity\":" + Json.Num(total) + "}");
        }

        private static string Camera(string id, Dictionary<string, string> args)
        {
            Pawn pawn = RequireSelectedPawn();
            if (Find.CameraDriver == null)
            {
                throw new LabCommandException("camera driver unavailable");
            }

            if (Get(args, "action", "jump") == "status")
            {
                CameraDriver driver = Find.CameraDriver;
                CellRect view = driver.CurrentViewRect;
                return Ok(id, "\"camera\":{\"mapPos\":" + Json.Cell(driver.MapPosition) +
                    ",\"rootSize\":" + Json.Num(driver.RootSize) +
                    ",\"zoom\":" + Json.Str(driver.CurrentZoom.ToString()) +
                    ",\"view\":{\"minX\":" + Json.Num(view.minX) + ",\"maxX\":" + Json.Num(view.maxX) +
                    ",\"minZ\":" + Json.Num(view.minZ) + ",\"maxZ\":" + Json.Num(view.maxZ) + "}}");
            }

            Find.CameraDriver.JumpToCurrentMapLoc(pawn.Position);
            return Ok(id, "\"cameraPos\":" + Json.Cell(pawn.Position));
        }

        /// <summary>
        /// Small entry point for the book. Every branch calls the same shared services the window
        /// uses (<see cref="GameComponent_Spellbook"/> and <see cref="SpellService"/>); nothing here
        /// reinterprets a recipe, so a lab scenario cannot drift from what the page does.
        /// </summary>
        private static string Spellbook(string id, Dictionary<string, string> args)
        {
            if (Current.Game == null)
            {
                throw new LabCommandException("no game loaded");
            }

            string action = Get(args, "action", "status").ToLowerInvariant();
            GameComponent_Spellbook component = GameComponent_Spellbook.Current;
            Pawn pawn = Find.Selector != null ? Find.Selector.SingleSelectedThing as Pawn : null;
            WindowStack stack = Find.WindowStack;

            StringBuilder json = new StringBuilder(1024);
            json.Append("\"action\":").Append(Json.Str(action));
            json.Append(",\"component\":").Append(Json.Bool(component != null));

            switch (action)
            {
                case "status":
                    break;

                case "save":
                {
                    RequireSpellbookComponent(component);
                    int slot = RequireSpellSlot(args);
                    SpellRecipe recipe = SpellRecipeFromArgs(args);
                    string reason;
                    bool saved = component.SaveSlot(pawn, slot, recipe, out reason);
                    json.Append(",\"save\":{\"ok\":").Append(Json.Bool(saved));
                    json.Append(",\"slot\":").Append(Json.Num(slot));
                    json.Append(",\"name\":").Append(Json.Str(recipe.name));
                    json.Append(",\"reason\":").Append(Json.Str(reason));
                    json.Append('}');
                    break;
                }

                case "clear":
                {
                    RequireSpellbookComponent(component);
                    int slot = RequireSpellSlot(args);
                    component.ClearSlot(pawn, slot);
                    json.Append(",\"cleared\":").Append(Json.Num(slot));
                    break;
                }

                case "active":
                {
                    RequireSpellbookComponent(component);
                    if (pawn == null)
                    {
                        throw new LabCommandException("spellbook active needs a selected pawn");
                    }

                    int slot = RequireSpellSlot(args);
                    component.SetActiveSlot(pawn, slot);
                    json.Append(",\"setActive\":").Append(Json.Num(slot));
                    json.Append(",\"readBack\":").Append(Json.Num(component.GetActiveSlot(pawn)));
                    break;
                }

                case "select":
                {
                    // Same entry the bookmark strip uses: open the page, then SelectBookmark.
                    RequireSpellbookComponent(component);
                    if (pawn == null)
                    {
                        throw new LabCommandException("spellbook select needs a selected pawn");
                    }

                    int slot = RequireSpellSlot(args);
                    Window_Spellbook window = stack != null ? stack.WindowOfType<Window_Spellbook>() : null;
                    if (window != null && window.Caster != pawn)
                    {
                        window.Close(true);
                        window = null;
                    }

                    if (window == null)
                    {
                        SpellService.OpenSpellbook(pawn);
                        window = stack != null ? stack.WindowOfType<Window_Spellbook>() : null;
                    }

                    if (window == null)
                    {
                        throw new LabCommandException("spellbook window did not open");
                    }

                    window.SelectBookmark(slot);
                    json.Append(",\"select\":{\"slot\":").Append(Json.Num(slot));
                    json.Append(",\"readBack\":").Append(Json.Num(component.GetActiveSlot(pawn))).Append('}');
                    break;
                }

                case "category":
                {
                    // Evidence-only hook for the screenshot: the category is a private UI field and
                    // has no public setter. This changes no game state outside the open page.
                    Window_Spellbook window = stack != null ? stack.WindowOfType<Window_Spellbook>() : null;
                    if (window == null)
                    {
                        if (pawn == null)
                        {
                            throw new LabCommandException("spellbook category needs an open book or a selected pawn");
                        }

                        SpellService.OpenSpellbook(pawn);
                        window = stack != null ? stack.WindowOfType<Window_Spellbook>() : null;
                    }

                    if (window == null)
                    {
                        throw new LabCommandException("spellbook window did not open");
                    }

                    int index = RequireInt(args, "index");
                    if (index < 0 || index >= CatalogLayout.CategoryCount)
                    {
                        throw new LabCommandException("category index must be 0.." + (CatalogLayout.CategoryCount - 1));
                    }

                    FieldInfo field = typeof(Window_Spellbook).GetField("category", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field == null)
                    {
                        throw new LabCommandException("Window_Spellbook.category field not found");
                    }

                    field.SetValue(window, index == 0 ? null : (object)(SpellGlyphKind)(index - 1));
                    json.Append(",\"category\":").Append(Json.Num(index));
                    break;
                }

                case "validate":
                {
                    SpellRecipe recipe = args.ContainsKey("slot")
                        ? RequireSpellbookRecipe(component, pawn, RequireSpellSlot(args))
                        : SpellRecipeFromArgs(args);
                    string reason;
                    bool valid = SpellService.Validate(recipe, out reason);
                    json.Append(",\"validate\":{\"ok\":").Append(Json.Bool(valid));
                    json.Append(",\"reason\":").Append(Json.Str(reason));
                    json.Append(",\"glyphs\":");
                    AppendGlyphNames(json, recipe.glyphs);
                    json.Append('}');
                    break;
                }

                case "can":
                {
                    if (pawn == null)
                    {
                        throw new LabCommandException("spellbook can needs a selected pawn");
                    }

                    SpellRecipe recipe = args.ContainsKey("glyphs")
                        ? SpellRecipeFromArgs(args)
                        : RequireSpellbookRecipe(component, pawn, RequireSpellSlot(args));
                    string reason;
                    bool can = SpellService.CanCast(pawn, recipe, out reason);
                    json.Append(",\"canCast\":{\"ok\":").Append(Json.Bool(can));
                    json.Append(",\"reason\":").Append(Json.Str(reason));
                    json.Append('}');
                    break;
                }

                case "cast":
                {
                    if (pawn == null)
                    {
                        throw new LabCommandException("spellbook cast needs a selected pawn");
                    }

                    SpellRecipe recipe = args.ContainsKey("glyphs")
                        ? SpellRecipeFromArgs(args)
                        : RequireSpellbookRecipe(component, pawn, RequireSpellSlot(args));
                    LocalTargetInfo target = args.ContainsKey("id") || (args.ContainsKey("x") && args.ContainsKey("z"))
                        ? ResolveTarget(RequireMap(), args, "spellbook cast")
                        : new LocalTargetInfo(pawn);
                    string reason;
                    bool cast = SpellService.TryCast(pawn, recipe, target, out reason);
                    ExperimentLog.Record("lab", pawn,
                        "spellbook cast name=" + (recipe.name ?? "") + " target=" + target.Cell + " ok=" + cast +
                        " reason=" + (reason ?? ""));
                    json.Append(",\"cast\":{\"ok\":").Append(Json.Bool(cast));
                    json.Append(",\"reason\":").Append(Json.Str(reason));
                    json.Append(",\"target\":").Append(Json.Cell(target.Cell));
                    json.Append('}');
                    break;
                }

                case "begin":
                {
                    if (pawn == null)
                    {
                        throw new LabCommandException("spellbook begin needs a selected pawn");
                    }

                    // The shared player entry point the page button and the gizmo both use.
                    SpellRecipe recipe = args.ContainsKey("glyphs")
                        ? SpellRecipeFromArgs(args)
                        : RequireSpellbookRecipe(component, pawn, RequireSpellSlot(args));
                    SpellService.BeginTargeting(pawn, recipe);
                    Targeter targeter = Find.Targeter;
                    json.Append(",\"targeter\":{\"isTargeting\":").Append(Json.Bool(targeter != null && targeter.IsTargeting));
                    json.Append('}');
                    break;
                }

                case "cancel":
                    if (Find.Targeter != null && Find.Targeter.IsTargeting)
                    {
                        Find.Targeter.StopTargeting();
                    }

                    break;

                case "open":
                    if (pawn == null)
                    {
                        throw new LabCommandException("spellbook open needs a selected pawn");
                    }

                    SpellService.OpenSpellbook(pawn);
                    break;

                case "close":
                {
                    Window_Spellbook open = stack != null ? stack.WindowOfType<Window_Spellbook>() : null;
                    if (open != null)
                    {
                        open.Close(true);
                    }

                    break;
                }

                case "copyprobe":
                {
                    // GetSlot must hand out an independent copy: mutating it cannot touch the slot.
                    RequireSpellbookComponent(component);
                    int slot = RequireSpellSlot(args);
                    SpellRecipe first = component.GetSlot(pawn, slot);
                    int before = first != null ? first.glyphs.Count : -1;
                    if (first != null)
                    {
                        first.glyphs.Add(SpellDefs.EffectHarm);
                        first.name = "copyprobe-mutated";
                    }

                    SpellRecipe second = component.GetSlot(pawn, slot);
                    int after = second != null ? second.glyphs.Count : -1;
                    json.Append(",\"copyProbe\":{\"slot\":").Append(Json.Num(slot));
                    json.Append(",\"countBefore\":").Append(Json.Num(before));
                    json.Append(",\"countAfter\":").Append(Json.Num(after));
                    json.Append(",\"isolated\":").Append(Json.Bool(before == after));
                    json.Append('}');
                    break;
                }

                case "plan":
                {
                    // Read-only inspection of what the shared compiler would execute. This is the
                    // same call TryCast/BeginTargeting make, so the numbers cannot drift from a cast.
                    SpellRecipe recipe = args.ContainsKey("glyphs")
                        ? SpellRecipeFromArgs(args)
                        : RequireSpellbookRecipe(component, pawn, RequireSpellSlot(args));
                    string reason;
                    SpellCastPlan plan = SpellCompiler.Compile(recipe, out reason);
                    json.Append(",\"plan\":");
                    if (plan == null)
                    {
                        json.Append("null,\"planReason\":").Append(Json.Str(reason));
                    }
                    else
                    {
                        json.Append('{');
                        json.Append("\"name\":").Append(Json.Str(plan.recipeName));
                        json.Append(",\"form\":").Append(Json.Str(plan.form.ToString()));
                        json.Append(",\"steps\":[");
                        for (int i = 0; i < plan.steps.Count; i++)
                        {
                            if (i > 0)
                            {
                                json.Append(',');
                            }

                            SpellPlanStep step = plan.steps[i];
                            json.Append('{');
                            json.Append("\"effect\":").Append(Json.Str(step.effect.ToString()));
                            json.Append(",\"amount\":").Append(Json.Num(step.amount));
                            json.Append(",\"radius\":").Append(Json.Num(step.radius));
                            json.Append(",\"amplifyStacks\":").Append(Json.Num(step.amplifyStacks));
                            json.Append(",\"aoeStacks\":").Append(Json.Num(step.aoeStacks));
                            json.Append(",\"aoe\":").Append(Json.Bool(step.aoe));
                            json.Append('}');
                        }

                        json.Append("],\"range\":").Append(Json.Num(plan.range));
                        json.Append(",\"missileCount\":").Append(Json.Num(plan.projectile != null ? plan.projectile.missileCount : 0));
                        json.Append(",\"missileSpeed\":").Append(Json.Num(plan.projectile != null ? plan.projectile.speedCellsPerSecond : 0f));
                        json.Append(",\"pierceStacks\":").Append(Json.Num(plan.pierceStacks));

                        json.Append("],\"glyphs\":");
                        AppendGlyphNames(json, plan.glyphs);
                        json.Append('}');
                    }

                    break;
                }

                default:
                    throw new LabCommandException(
                        "spellbook action must be status|save|clear|active|select|category|validate|can|plan|cast|begin|cancel|open|close|copyprobe");
            }

            AppendSpellbookState(json, component, pawn, stack);
            return Ok(id, json.ToString());
        }

        private static void RequireSpellbookComponent(GameComponent_Spellbook component)
        {
            if (component == null)
            {
                throw new LabCommandException("GameComponent_Spellbook.Current is null (no book in this game)");
            }
        }

        private static int RequireSpellSlot(Dictionary<string, string> args)
        {
            int slot = RequireInt(args, "slot");
            if (slot < 0 || slot >= GameComponent_Spellbook.SlotCount)
            {
                throw new LabCommandException("slot must be 0.." + (GameComponent_Spellbook.SlotCount - 1));
            }

            return slot;
        }

        private static SpellRecipe RequireSpellbookRecipe(GameComponent_Spellbook component, Pawn pawn, int slot)
        {
            RequireSpellbookComponent(component);
            if (pawn == null)
            {
                throw new LabCommandException("spells belong to a pawn: select one first");
            }

            SpellRecipe recipe = component.GetSlot(pawn, slot);
            if (recipe == null)
            {
                throw new LabCommandException("slot " + slot + " is empty");
            }

            return recipe;
        }

        /// <summary>Builds a recipe from name= and glyphs=&lt;defName,...&gt;; unknown glyphs are refused.</summary>
        private static SpellRecipe SpellRecipeFromArgs(Dictionary<string, string> args)
        {
            SpellRecipe recipe = new SpellRecipe();
            recipe.name = Get(args, "name", "lab spell");
            string raw = Get(args, "glyphs", "");
            if (string.IsNullOrEmpty(raw))
            {
                return recipe;
            }

            string[] parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string defName = parts[i].Trim();
                if (defName.Length == 0)
                {
                    continue;
                }

                if (SpellDefs.Glyph(defName) == null)
                {
                    throw new LabCommandException("unknown glyph def '" + defName + "'");
                }

                recipe.glyphs.Add(defName);
            }

            return recipe;
        }

        private static void AppendGlyphNames(StringBuilder json, List<string> glyphs)
        {
            json.Append('[');
            for (int i = 0; glyphs != null && i < glyphs.Count; i++)
            {
                if (i > 0)
                {
                    json.Append(',');
                }

                json.Append(Json.Str(glyphs[i]));
            }

            json.Append(']');
        }

        private static void AppendSpellbookState(StringBuilder json, GameComponent_Spellbook component, Pawn pawn,
            WindowStack stack)
        {
            json.Append(",\"slotCount\":").Append(Json.Num(GameComponent_Spellbook.SlotCount));
            json.Append(",\"pawnId\":").Append(pawn != null ? Json.Num(pawn.thingIDNumber) : "null");
            json.Append(",\"activeSlot\":").Append(component != null && pawn != null
                ? Json.Num(component.GetActiveSlot(pawn))
                : "null");

            SpellAbility spellAbility = null;
            if (pawn != null && pawn.abilities != null)
            {
                List<Ability> abilities = pawn.abilities.AllAbilitiesForReading;
                for (int i = 0; i < abilities.Count; i++)
                {
                    if (abilities[i] is SpellAbility found)
                    {
                        spellAbility = found;
                        break;
                    }
                }
            }

            Command castGizmo = null;
            if (spellAbility != null)
            {
                // Last gizmo is the cast button; the first is "open book".
                foreach (Command command in spellAbility.GetGizmos())
                {
                    castGizmo = command;
                }
            }

            json.Append(",\"gizmo\":{");
            if (castGizmo == null)
            {
                json.Append("\"present\":false");
            }
            else
            {
                json.Append("\"present\":true");
                json.Append(",\"label\":").Append(Json.Str(castGizmo.defaultLabel));
                json.Append(",\"disabled\":").Append(Json.Bool(castGizmo.Disabled));
                json.Append(",\"disabledReason\":").Append(Json.Str(castGizmo.disabledReason));
            }

            json.Append('}');

            json.Append(",\"slots\":[");
            for (int slot = 0; component != null && slot < GameComponent_Spellbook.SlotCount; slot++)
            {
                if (slot > 0)
                {
                    json.Append(',');
                }

                SpellRecipe recipe = component.GetSlot(pawn, slot);
                json.Append('{');
                json.Append("\"slot\":").Append(Json.Num(slot));
                json.Append(",\"empty\":").Append(Json.Bool(recipe == null));
                json.Append(",\"name\":").Append(Json.Str(recipe != null ? recipe.name : null));
                json.Append(",\"glyphs\":");
                AppendGlyphNames(json, recipe != null ? recipe.glyphs : null);
                json.Append('}');
            }

            json.Append(']');

            Window_Spellbook window = stack != null ? stack.WindowOfType<Window_Spellbook>() : null;
            json.Append(",\"window\":{\"open\":").Append(Json.Bool(window != null));
            if (window != null)
            {
                json.Append(",\"casterId\":").Append(window.Caster != null ? Json.Num(window.Caster.thingIDNumber) : "null");
                json.Append(",\"casterName\":").Append(Json.Str(window.Caster != null ? window.Caster.LabelShort : null));
                json.Append(",\"hasUnsavedDraft\":").Append(Json.Bool(window.HasUnsavedDraft));
            }

            json.Append('}');
        }

        private static string Time(string id, Dictionary<string, string> args)
        {
            if (Current.Game == null || Find.TickManager == null)
            {
                throw new LabCommandException("no game loaded");
            }

            int speed = RequireInt(args, "speed");
            if (speed < (int)TimeSpeed.Paused || speed > (int)TimeSpeed.Ultrafast)
            {
                throw new LabCommandException("speed must be 0..4 (Paused..Ultrafast)");
            }

            Find.TickManager.CurTimeSpeed = (TimeSpeed)speed;
            return Ok(id, "\"speed\":" + Json.Num(speed) + ",\"name\":" + Json.Str(((TimeSpeed)speed).ToString()));
        }

        private static string Load(string id, Dictionary<string, string> args)
        {
            if (Current.Game == null)
            {
                throw new LabCommandException("no game loaded");
            }

            string raw = Get(args, "name", "wotf-lab");
            string name = ValidateLabSaveName(raw);
            string path;
            try
            {
                path = GenFilePaths.FilePathForSavedGame(name);
            }
            catch (Exception exception)
            {
                throw new LabCommandException("cannot resolve save '" + name + "': " + exception.Message);
            }

            if (!File.Exists(path))
            {
                throw new LabCommandException("save not found in isolated Saves: " + name);
            }

            // The queued load disposes the current game. Clear lab-only map state first; the response
            // is written immediately after this method returns, before LongEventHandler executes.
            Setups.Clear();
            BehaviorFixtures.Clear();
            LabBehaviorTracker.Clear();
            labFiredMissiles = 0;
            GameDataSaveLoader.LoadGame(name);
            return Ok(id, "\"queued\":true,\"name\":" + Json.Str(name) + ",\"save\":" + Json.Str(path) +
                ",\"note\":\"load queued; result written before LongEvent loads the save\"");
        }

        private static string ValidateLabSaveName(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.Length > 40 || raw == "." || raw == ".." || raw.Contains(".."))
            {
                throw new LabCommandException("save name must be 1..40 safe characters and must not contain '..'");
            }

            foreach (char c in raw)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.')
                {
                    throw new LabCommandException("save name may contain only letters, digits, '_', '-' and '.'");
                }
            }

            return raw;
        }

        private static string Save(string id, Dictionary<string, string> args)
        {
            if (Current.Game == null)
            {
                throw new LabCommandException("no game loaded");
            }

            string name = SanitizeSaveName(Get(args, "name", "wotf-lab"));
            GameDataSaveLoader.SaveGame(name);
            ExperimentLog.Record("lab", null, "saved game " + name);
            return Ok(id, "\"save\":" + Json.Str(name) + ",\"folder\":" + Json.Str(GenFilePaths.SaveDataFolderPath));
        }

        private static string Step(string id, Dictionary<string, string> args)
        {
            if (Current.Game == null || Find.TickManager == null)
            {
                throw new LabCommandException("no game loaded");
            }

            int count = 1;
            if (args.ContainsKey("n"))
            {
                count = RequireInt(args, "n");
            }

            if (count < 1 || count > 600)
            {
                throw new LabCommandException("n must be 1..600");
            }

            for (int i = 0; i < count; i++)
            {
                Find.TickManager.DoSingleTick();
            }

            return Ok(id, "\"stepped\":" + Json.Num(count) + ",\"tick\":" + Json.Num(Find.TickManager.TicksGame));
        }

        /// <summary>
        /// L2 scenario tests: "list" (discovery only), "status" (does a run still own the game?) and
        /// "run" (start a run; it continues frame by frame through the lab pump).
        /// </summary>
        private static string Test(string id, Dictionary<string, string> args)
        {
            string action = Get(args, "action", "run").ToLowerInvariant();
            string filter = Get(args, "filter", string.Empty);

            if (action == "list")
            {
                return Ok(id, "\"tests\":" + Testing.LabTestRunner.List(filter));
            }

            if (action == "status")
            {
                return Ok(id, "\"running\":" + Json.Bool(Testing.LabTestRunner.Running)
                              + ",\"last\":" + Json.Str(Testing.LabTestRunner.Status));
            }

            if (action == "cancel")
            {
                return Ok(id, Testing.LabTestRunner.Cancel());
            }

            if (action != "run")
            {
                throw new LabCommandException("test action must be list, status, cancel or run");
            }

            string fixtureName = Get(args, "fixture", "a25-fixture");
            string fixture = fixtureName.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : fixtureName;
            int repeats = 1;
            string repeatRaw = Get(args, "repeat", null);
            if (!string.IsNullOrEmpty(repeatRaw)
                && !int.TryParse(repeatRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out repeats))
            {
                throw new LabCommandException("invalid integer 'repeat'");
            }

            bool reloadFixture = Get(args, "reload", "0") == "1";
            if (!Testing.LabTestRunner.Begin(filter, fixture, repeats, reloadFixture, out string detail, out string error))
            {
                throw new LabCommandException(error);
            }

            return Ok(id, detail);
        }

        private static string ActionResult(string id, bool ok, string action, Pawn pawn)
        {
            StringBuilder json = new StringBuilder(192);
            json.Append('{');
            json.Append("\"id\":").Append(Json.Str(id));
            json.Append(",\"ok\":").Append(Json.Bool(ok));
            json.Append(",\"action\":").Append(Json.Str(action));
            json.Append(",\"pawn\":").Append(Json.Num(pawn.thingIDNumber));
            if (!ok)
            {
                json.Append(",\"error\":").Append(Json.Str(LabActions.LastError ?? "action failed"));
            }

            json.Append('}');
            return json.ToString();
        }

        private static string Ok(string id, string extra)
        {
            return "{\"id\":" + Json.Str(id) + ",\"ok\":true" + (string.IsNullOrEmpty(extra) ? string.Empty : "," + extra) + "}";
        }

        private static Map RequireMap()
        {
            if (Current.Game == null || Find.CurrentMap == null)
            {
                throw new LabCommandException("no map loaded");
            }

            return Find.CurrentMap;
        }

        private static Pawn RequireSelectedPawn()
        {
            Pawn pawn = Current.Game != null && Find.Selector != null ? Find.Selector.SingleSelectedThing as Pawn : null;
            if (pawn == null)
            {
                throw new LabCommandException("no pawn selected");
            }

            if (!pawn.Spawned || pawn.Map == null)
            {
                throw new LabCommandException("selected pawn is not spawned");
            }

            return pawn;
        }

        private static Pawn FindPawn(Map map, int thingId)
        {
            foreach (Thing thing in map.spawnedThings)
            {
                if (thing != null && thing.thingIDNumber == thingId && thing is Pawn pawn)
                {
                    return pawn;
                }
            }

            return null;
        }

        private static Thing FindThing(Map map, int thingId)
        {
            foreach (Thing thing in map.spawnedThings)
            {
                if (thing != null && thing.thingIDNumber == thingId)
                {
                    return thing;
                }
            }

            return null;
        }

        private static IntVec3 FindFreeCell(Map map)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(map.Center, 25f, true))
            {
                if (cell.InBounds(map) && cell.Standable(map) && !cell.Fogged(map) && cell.GetFirstPawn(map) == null)
                {
                    return cell;
                }
            }

            return map.Center;
        }

        private static bool DefExists<T>(string defName) where T : Def
        {
            try
            {
                return DefDatabase<T>.GetNamedSilentFail(defName) != null;
            }
            catch
            {
                // DefDatabase may not be initialized in a non-game context; report "not loaded".
                return false;
            }
        }

        private static int RequireInt(Dictionary<string, string> args, string key)
        {
            string raw = Get(args, key, null);
            if (raw == null || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                throw new LabCommandException("missing or invalid integer '" + key + "'");
            }

            return value;
        }

        private static float RequireFloat(Dictionary<string, string> args, string key)
        {
            string raw = Get(args, key, null);
            if (raw == null || !float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                throw new LabCommandException("missing or invalid number '" + key + "'");
            }

            return value;
        }

        private static string Get(Dictionary<string, string> args, string key, string fallback)
        {
            return args.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value) ? value : fallback;
        }

        private static string SanitizeSaveName(string name)
        {
            StringBuilder builder = new StringBuilder(40);
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')
                {
                    builder.Append(c);
                }

                if (builder.Length >= 40)
                {
                    break;
                }
            }

            return builder.Length == 0 ? "wotf-lab" : builder.ToString();
        }

        private sealed class BehaviorRecord
        {
            public int HumanId;
            public int ElfId;
            public Zone_Growing Zone;
            public List<IntVec3> ZoneCells;
            public string CropDefName;
            public string WeaponDefName;
            public int WeaponId;
            public int TargetId;
        }

        private sealed class SetupRecord
        {
            public string Json;
            public IntVec3 Origin;
            public int LaneZ;
            public int DoorId;
            public int ClearedPlants;
            public int ClearedFilth;
            public readonly List<int> WallIds = new List<int>();
            public readonly List<int> PawnIds = new List<int>();
        }
    }
}
