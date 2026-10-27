using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Experimental.Missiles;
using WhisperOfTheForest.Experimental.Movement;

namespace WhisperOfTheForest.Experimental
{
    /// <summary>
    /// Whitelisted lab commands. Every command is a small, explicit game operation; there is no
    /// eval, no file access outside the lab folder and no network.
    /// </summary>
    internal static class LabCommands
    {
        private const int MaxColonists = 16;
        private const int MaxExperimentThings = 32;
        private const int MaxTraitEntries = 16;
        private const int MaxHediffEntries = 32;
        private const int MaxObservedFrames = 16;

        private static int labFiredMissiles;
        private static readonly Dictionary<int, SetupRecord> Setups = new Dictionary<int, SetupRecord>();
        private static readonly Dictionary<int, BehaviorRecord> BehaviorFixtures = new Dictionary<int, BehaviorRecord>();

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
                case "cast":
                    return Cast(id, args);
                case "fire":
                    return Fire(id, args);
                case "missiles":
                    return MissileStats(id, args);
                case "path":
                    return PathInfo(id, args);
                case "setup":
                    return Setup(id, args);
                case "behaviors":
                    return Behaviors(id, args);
                case "warp":
                    return Warp(id, args);
                case "hurt":
                    return Hurt(id, args);
                case "camera":
                    return Camera(id);
                case "ui":
                    return Ui(id, args);
                case "time":
                    return Time(id, args);
                case "save":
                    return Save(id, args);
                case "load":
                    return Load(id, args);
                case "step":
                    return Step(id, args);
                default:
                    throw new LabCommandException("unknown command '" + command + "'");
            }
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
            json.Append(",\"defs\":{");
            json.Append("\"WotF_ArcaneMissiles\":").Append(Json.Bool(DefExists<AbilityDef>(LabActions.ArcaneMissilesDefName)));
            json.Append(",\"WotF_ArcaneMissiles_Heavy\":").Append(Json.Bool(DefExists<AbilityDef>(LabActions.ArcaneMissilesHeavyDefName)));
            json.Append(",\"WotF_ArcaneMissiles_Mender\":").Append(Json.Bool(DefExists<AbilityDef>(LabActions.ArcaneMissilesMenderDefName)));
            json.Append(",\"WotF_TerrainStride\":").Append(Json.Bool(DefExists<ThingDef>(LabActions.TerrainStrideDefName)));
            json.Append(",\"WotF_BlinkStride\":").Append(Json.Bool(DefExists<ThingDef>(LabActions.BlinkStrideDefName)));
            json.Append(",\"WotF_Visual_RimUnity\":").Append(Json.Bool(DefExists<ArcaneMissileVisualDef>(ArcaneMissileDefs.RimUnityVisualDefName)));
            json.Append(",\"WotF_Visual_Native\":").Append(Json.Bool(DefExists<ArcaneMissileVisualDef>(ArcaneMissileDefs.NativeVisualDefName)));
            json.Append(",\"WotF_Impact_Damage\":").Append(Json.Bool(DefExists<ArcaneMissileImpactDef>(ArcaneMissileDefs.DamageImpactDefName)));
            json.Append(",\"WotF_Impact_Heal\":").Append(Json.Bool(DefExists<ArcaneMissileImpactDef>(ArcaneMissileDefs.HealImpactDefName)));
            json.Append(",\"WotF_Impact_Explode\":").Append(Json.Bool(DefExists<ArcaneMissileImpactDef>(ArcaneMissileDefs.ExplodeImpactDefName)));
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

        /// <summary>
        /// Resolves an ability preset and returns a fresh copy of its missile settings. The Def is
        /// never handed out, and a Def without the missile comp is a hard error instead of a
        /// silent fallback to the built-in defaults.
        /// </summary>
        private static ArcaneMissileSettings PresetSettings(string preset)
        {
            AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail(preset);
            if (def == null)
            {
                throw new LabCommandException("preset ability def " + preset + " is not loaded");
            }

            CompProperties_AbilityWotFArcaneMissiles props = def.comps != null
                ? def.comps.OfType<CompProperties_AbilityWotFArcaneMissiles>().FirstOrDefault()
                : null;
            if (props == null || props.settings == null)
            {
                throw new LabCommandException("preset ability def " + preset + " has no CompProperties_AbilityWotFArcaneMissiles settings");
            }

            return props.settings.Copy();
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
            json.Append("\"mode\":").Append(Json.Str(settings.impactMode.ToString().ToLowerInvariant()));
            json.Append(",\"count\":").Append(Json.Num(settings.missileCount));
            json.Append(",\"speed\":").Append(Json.Num(settings.speedCellsPerSecond));
            json.Append(",\"turn\":").Append(Json.Num(settings.maxTurnDegreesPerSecond));
            json.Append(",\"wobble\":").Append(Json.Num(settings.wobbleAmplitude));
            json.Append(",\"hitRadius\":").Append(Json.Num(settings.hitRadius));
            json.Append(",\"lifetime\":").Append(Json.Num(settings.lifetimeSeconds));
            json.Append(",\"maxRange\":").Append(Json.Num(settings.maxRangeCells));
            json.Append(",\"radius\":").Append(Json.Num(settings.explosionRadius));
            json.Append(",\"damage\":").Append(Json.Num(settings.damageAmount));
            json.Append(",\"heal\":").Append(Json.Num(settings.healAmount));
            json.Append(",\"ignoreFlightLimits\":").Append(Json.Bool(settings.ignoreFlightLimits));
            json.Append(",\"expireOnTargetLost\":").Append(Json.Bool(settings.expireOnTargetLost));
            json.Append(",\"impactDef\":").Append(Json.Str(settings.impactDef != null ? settings.impactDef.defName : null));
            json.Append(",\"impactMode\":").Append(Json.Str(settings.impactMode.ToString()));
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

            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("ElfColonist");
            if (kind == null)
            {
                kind = PawnKindDefOf.Colonist;
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
            bool ok = LabActions.TryGrantPsycast(pawn);
            return ActionResult(id, ok, "grant", pawn);
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

        private static string Cast(string id, Dictionary<string, string> args)
        {
            Map map = RequireMap();
            Pawn pawn = RequireSelectedPawn();
            string presetName = Get(args, "preset", LabActions.ArcaneMissilesDefName);
            AbilityDef abilityDef = DefDatabase<AbilityDef>.GetNamedSilentFail(presetName);
            if (abilityDef == null)
            {
                throw new LabCommandException("ability def " + presetName + " is not loaded");
            }

            ArcaneMissileSettings effectiveSettings = PresetSettings(presetName);
            effectiveSettings.Clamp();

            Ability ability = pawn.abilities != null ? pawn.abilities.GetAbility(abilityDef, true) : null;
            if (ability == null)
            {
                throw new LabCommandException("selected pawn has no " + presetName + " (run grant first)");
            }

            LocalTargetInfo target;
            if (args.ContainsKey("id"))
            {
                Thing thing = FindThing(map, RequireInt(args, "id"));
                if (thing == null)
                {
                    throw new LabCommandException("no spawned thing for cast target");
                }

                target = new LocalTargetInfo(thing);
            }
            else if (args.ContainsKey("x") && args.ContainsKey("z"))
            {
                IntVec3 cell = new IntVec3(RequireInt(args, "x"), 0, RequireInt(args, "z"));
                if (!cell.InBounds(map))
                {
                    throw new LabCommandException("cast cell out of bounds");
                }

                target = new LocalTargetInfo(cell);
            }
            else
            {
                throw new LabCommandException("cast needs id=<thingId> or x=<int> z=<int>");
            }

            bool activated = ability.Activate(target, target);
            ExperimentLog.Record(
                "lab",
                pawn,
                "cast " + abilityDef.defName + " target=" + target.Cell + " activated=" + activated +
                " ignoreFlightLimits=" + effectiveSettings.ignoreFlightLimits +
                " expireOnTargetLost=" + effectiveSettings.expireOnTargetLost);
            if (!activated)
            {
                throw new LabCommandException("ability.Activate returned false");
            }

            StringBuilder json = new StringBuilder(320);
            json.Append("\"target\":").Append(Json.Cell(target.Cell));
            json.Append(",\"ability\":").Append(Json.Str(abilityDef.defName));
            json.Append(",\"preset\":").Append(Json.Str(presetName));
            json.Append(",\"settings\":");
            AppendMissileSettings(json, effectiveSettings);
            return Ok(id, json.ToString());
        }

        private static string Fire(string id, Dictionary<string, string> args)
        {
            Map map = RequireMap();
            Pawn pawn = RequireSelectedPawn();
            string presetName = Get(args, "preset", null);
            ArcaneMissileSettings settings = string.IsNullOrEmpty(presetName)
                ? ArcaneMissileLauncher.CreateDefaultSettings()
                : PresetSettings(presetName);

            string requestedMode = Get(args, "mode", null);
            if (!string.IsNullOrEmpty(requestedMode))
            {
                switch (requestedMode.ToLowerInvariant())
                {
                    case "damage":
                        settings.impactMode = ArcaneMissileImpactMode.Damage;
                        break;
                    case "heal":
                        settings.impactMode = ArcaneMissileImpactMode.Heal;
                        break;
                    case "explode":
                        settings.impactMode = ArcaneMissileImpactMode.Explode;
                        break;
                    default:
                        throw new LabCommandException("mode must be damage|heal|explode");
                }

                // Only an explicit mode overrides the preset's impactDef; otherwise the preset wins.
                settings.impactDef = ArcaneMissileDefs.ImpactForMode(settings.impactMode);
            }

            string mode = settings.impactMode.ToString().ToLowerInvariant();
            if (settings.impactDef == null)
            {
                // Keep the legacy log/response readable; the launcher would resolve the same Def.
                settings.impactDef = ArcaneMissileDefs.ImpactForMode(settings.impactMode);
            }

            string visual = Get(args, "visual", null);
            if (!string.IsNullOrEmpty(visual))
            {
                string visualDefName;
                switch (visual.ToLowerInvariant())
                {
                    case "rimunity":
                        visualDefName = ArcaneMissileDefs.RimUnityVisualDefName;
                        break;
                    case "native":
                        visualDefName = ArcaneMissileDefs.NativeVisualDefName;
                        break;
                    default:
                        throw new LabCommandException("visual must be 'rimunity' or 'native'");
                }

                ArcaneMissileVisualDef visualDef = ArcaneMissileDefs.VisualByName(visualDefName);
                if (visualDef == null)
                {
                    throw new LabCommandException("visual def " + visualDefName + " is not loaded");
                }

                settings.visualDef = visualDef;
            }

            if (args.ContainsKey("count"))
            {
                settings.missileCount = RequireInt(args, "count");
            }

            if (args.ContainsKey("speed"))
            {
                settings.speedCellsPerSecond = RequireFloat(args, "speed");
            }

            if (args.ContainsKey("turn"))
            {
                settings.maxTurnDegreesPerSecond = RequireFloat(args, "turn");
            }

            if (args.ContainsKey("wobble"))
            {
                settings.wobbleAmplitude = RequireFloat(args, "wobble");
            }

            if (args.ContainsKey("damage"))
            {
                settings.damageAmount = RequireFloat(args, "damage");
            }

            if (args.ContainsKey("heal"))
            {
                settings.healAmount = RequireFloat(args, "heal");
            }

            if (args.ContainsKey("radius"))
            {
                settings.explosionRadius = RequireFloat(args, "radius");
            }

            settings.Clamp();

            MapComponent_ArcaneMissiles component = map.GetComponent<MapComponent_ArcaneMissiles>();
            if (Get(args, "reset", "0") == "1")
            {
                if (component != null)
                {
                    component.ResetLabCounters();
                }

                labFiredMissiles = 0;
            }

            LocalTargetInfo target = ResolveTarget(map, args, "fire");
            int launched = ArcaneMissileLauncher.Fire(pawn, target, settings);
            labFiredMissiles += launched;
            ExperimentLog.Record(
                "lab",
                pawn,
                "fire launched=" + launched + " preset=" + (presetName ?? "default") + " mode=" + mode + " target=" + target.Cell +
                " speed=" + settings.speedCellsPerSecond + " turn=" + settings.maxTurnDegreesPerSecond +
                " count=" + settings.missileCount + " wobble=" + settings.wobbleAmplitude +
                " ignoreFlightLimits=" + settings.ignoreFlightLimits + " expireOnTargetLost=" + settings.expireOnTargetLost +
                " visual=" + (settings.visualDef != null ? settings.visualDef.defName : "default") +
                " impact=" + (settings.impactDef != null ? settings.impactDef.defName : "enum:" + settings.impactMode));

            StringBuilder json = new StringBuilder(512);
            json.Append("\"launched\":").Append(Json.Num(launched));
            json.Append(",\"preset\":").Append(Json.Str(presetName));
            json.Append(",\"target\":").Append(Json.Cell(target.Cell));
            json.Append(",\"settings\":");
            AppendMissileSettings(json, settings);
            json.Append(",\"visualDef\":").Append(Json.Str(settings.visualDef != null ? settings.visualDef.defName : null));
            json.Append(",\"visualEffective\":").Append(Json.Str(
                settings.visualDef != null ? settings.visualDef.defName : ArcaneMissileDefs.DefaultVisual?.defName));
            json.Append(",\"impactDef\":").Append(Json.Str(settings.impactDef != null ? settings.impactDef.defName : null));
            json.Append(",\"impactMode\":").Append(Json.Str(settings.impactMode.ToString()));
            json.Append(",\"impactEffective\":").Append(Json.Str(
                settings.impactDef != null ? settings.impactDef.defName : "enum:" + settings.impactMode));
            json.Append(",\"missiles\":").Append(MissileState(map));
            return Ok(id, json.ToString());
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
            PawnKindDef elfKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("ElfColonist") ?? PawnKindDefOf.Colonist;
            Pawn elf = SpawnSetupPawn(map, elfKind, new IntVec3(origin.x + 2, 0, laneZ), "Lab Elf");

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
            setup.Append(',');
            AppendSetupPawn(setup, "elf", elf);
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
            record.PawnIds.Add(elf.thingIDNumber);
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

        private static string Camera(string id)
        {
            Pawn pawn = RequireSelectedPawn();
            if (Find.CameraDriver == null)
            {
                throw new LabCommandException("camera driver unavailable");
            }

            Find.CameraDriver.JumpToCurrentMapLoc(pawn.Position);
            return Ok(id, "\"cameraPos\":" + Json.Cell(pawn.Position));
        }

        private static string Ui(string id, Dictionary<string, string> args)
        {
            WindowStack stack = Find.WindowStack;
            if (stack == null)
            {
                throw new LabCommandException("no window stack");
            }

            TechLabSettings settings = TechLabMod.Settings;
            if (settings == null)
            {
                throw new LabCommandException("TechLabMod.Settings is not initialized");
            }

            string action = Get(args, "action", "status").ToLowerInvariant();
            switch (action)
            {
                case "open":
                    if (stack.WindowOfType<Window_TechLab>() == null)
                    {
                        stack.Add(new Window_TechLab());
                    }

                    break;
                case "close":
                    Window_TechLab open = stack.WindowOfType<Window_TechLab>();
                    if (open != null)
                    {
                        open.Close(true);
                    }

                    break;
                case "show":
                    settings.showFloatingEntry = true;
                    settings.Write();
                    break;
                case "hide":
                    settings.showFloatingEntry = false;
                    settings.Write();
                    break;
                case "reset":
                    TechLabUi.ResetEntryPosition(settings);
                    break;
                case "restore":
                    // Same callback the native play-settings icon invokes; this exercises the
                    // callback path, not a mouse click on the rendered icon.
                    TechLabUi.OpenWindowAndRestoreEntry();
                    break;
                case "status":
                    break;
                default:
                    throw new LabCommandException("ui action must be open|close|show|hide|reset|restore|status");
            }

            Window_TechLab current = stack.WindowOfType<Window_TechLab>();
            Pawn selected = Find.Selector != null ? Find.Selector.SingleSelectedThing as Pawn : null;
            StringBuilder json = new StringBuilder(256);
            json.Append("\"action\":").Append(Json.Str(action));
            json.Append(",\"windowOpen\":").Append(Json.Bool(current != null));
            json.Append(",\"showFloatingEntry\":").Append(Json.Bool(settings.showFloatingEntry));
            json.Append(",\"hasCustomEntryPosition\":").Append(Json.Bool(settings.hasCustomEntryPosition));
            json.Append(",\"entryX\":").Append(Json.Num(settings.entryX));
            json.Append(",\"entryY\":").Append(Json.Num(settings.entryY));
            json.Append(",\"entryLayoutVersion\":").Append(Json.Num(TechLabUi.EntryLayoutVersion));
            json.Append(",\"hasRow\":").Append(Json.Bool(Patch_PlaySettingsRowEntry.HasRow));
            json.Append(",\"lastRowLeftX\":").Append(Json.Num(Patch_PlaySettingsRowEntry.LastRowLeftX));
            json.Append(",\"topY\":").Append(Json.Num(Patch_PlaySettingsRowEntry.LastRowTopY));
            json.Append(",\"screenWidth\":").Append(Json.Num(Patch_PlaySettingsRowEntry.LastScreenWidth));
            json.Append(",\"screenHeight\":").Append(Json.Num(Patch_PlaySettingsRowEntry.LastScreenHeight));
            json.Append(",\"rowEntryDraws\":").Append(Json.Num(Patch_PlaySettingsRowEntry.RowEntryDraws));
            json.Append(",\"selectedId\":").Append(selected != null ? Json.Num(selected.thingIDNumber) : "null");
            return Ok(id, json.ToString());
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
