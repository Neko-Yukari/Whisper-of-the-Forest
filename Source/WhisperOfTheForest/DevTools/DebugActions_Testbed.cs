using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI.Group;
using WhisperOfTheForest.Equipment;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Race;
using WhisperOfTheForest.Rituals;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.DevTools
{
    /// <summary>
    /// Dev-mode-only debug actions for setting up ritual and spell testbeds in one click.
    /// </summary>
    public static class DebugActions_Testbed
    {
        private const string Category = "Whisper of the Forest";
        private const float DefaultManaLinkRadius = 12.9f;

        private static readonly (string Name, string[] Glyphs)[] DamageSpellPresets =
        {
            ("Arcane Missile", new[] { "WotF_Form_Projectile", "WotF_Effect_Harm" }),
            ("Lightning", new[] { "WotF_Form_Projectile", "WotF_Effect_Lightning" }),
            ("Explosion", new[] { "WotF_Form_Projectile", "WotF_Effect_Explosion" }),
            ("Pierce", new[] { "WotF_Form_Projectile", "WotF_Augment_Pierce", "WotF_Effect_Harm" }),
            ("Cold Snap", new[] { "WotF_Form_Projectile", "WotF_Effect_Freeze", "WotF_Effect_ColdSnap" }),
            ("Railgun", new[] { "WotF_Form_Railgun" }),
            ("Flame Tornado", new[] { "WotF_Super_Tornado" })
        };

        [DebugAction(Category, "Spawn T3 ritual testbed", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnT3RitualTestbed()
        {
            Map map = Find.CurrentMap;
            IntVec3 center = UI.MouseCell();
            if (map == null || !center.InBounds(map))
            {
                return;
            }

            ThingDef altarDef = DefDatabase<ThingDef>.GetNamed("WotF_RitualAltar_Starlight");
            Thing altar = SpawnBuilding(altarDef, center, map);
            if (altar == null)
            {
                WotfLog.Warning("DevTools", $"Failed to spawn {altarDef.defName} at {center}.");
                return;
            }

            // Star 7x7 ring covers SkyWrath & Thunderstorm; Green 5x5 ring covers ClearSky.
            PlaceChalkRing(map, center, 7, DefDatabase<ThingDef>.GetNamed("WotF_ChalkMark_Star"));
            PlaceChalkRing(map, center, 5, DefDatabase<ThingDef>.GetNamed("WotF_ChalkMark_Green"));

            Thing node = SpawnManaCluster(map, center);
            if (node != null)
            {
                MapComponent_ManaNetwork manaComp = MapComponent_ManaNetwork.For(map);
                manaComp?.MarkDirty();
                ManaNetwork network = manaComp?.NetworkNear(node.Position, DefaultManaLinkRadius);
                network?.Add(network.Capacity);
            }

            // SkyWrath requires minParticipants=1 (1 host + 1 helper), so spawn 2 max-bloodline elf colonists.
            int spawnedCasters = SpawnElfCasters(map, center, 2);
            WotfLog.Info("DevTools", $"Spawned T3 ritual testbed at {center} with {spawnedCasters} elf casters.");
            Messages.Message("T3 ritual testbed ready.", altar, MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction(Category, "Spawn test enemies (3)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnTestEnemies()
        {
            Map map = Find.CurrentMap;
            IntVec3 center = UI.MouseCell();
            if (map == null || !center.InBounds(map))
            {
                return;
            }

            Faction faction = Find.FactionManager.RandomEnemyFaction(allowHidden: false, allowDefeated: true, allowNonHumanlike: false)
                ?? Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.HostileTo(Faction.OfPlayer) && !f.Hidden);
            if (faction == null)
            {
                Messages.Message("No hostile faction available.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            PawnKindDef kind = faction.RandomPawnKind() ?? PawnKindDefOf.Pirate;
            var spawned = new List<Pawn>(3);
            for (int i = 0; i < 3; i++)
            {
                IntVec3 cell = FindOpenCellNear(map, center, 3, requireUnroofed: true);
                if (!cell.IsValid)
                {
                    cell = FindOpenCellNear(map, center, 5, requireUnroofed: false);
                    if (cell.IsValid && cell.Roofed(map))
                    {
                        RoofDef roof = map.roofGrid.RoofAt(cell);
                        if (roof != null && !roof.isThickRoof)
                        {
                            map.roofGrid.SetRoof(cell, null);
                        }
                    }
                }

                if (!cell.IsValid)
                {
                    continue;
                }

                var request = new PawnGenerationRequest(
                    kind,
                    faction,
                    PawnGenerationContext.NonPlayer,
                    map.Tile,
                    forceGenerateNewPawn: true);
                Pawn enemy = PawnGenerator.GeneratePawn(request);
                GenSpawn.Spawn(enemy, cell, map);
                spawned.Add(enemy);
            }

            if (spawned.Count > 0)
            {
                LordMaker.MakeNewLord(
                    faction,
                    new LordJob_DefendPoint(center, wanderRadius: 2f, defendRadius: 3f, isCaravanSendable: false, addFleeToil: false),
                    map,
                    spawned);
                WotfLog.Info("DevTools", $"Spawned {spawned.Count} test enemies ({faction.Name}) at {center}.");
                Messages.Message($"Spawned {spawned.Count} test enemies.", spawned[0], MessageTypeDefOf.NeutralEvent, false);
            }
        }

        [DebugAction(Category, "Fill mana network", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FillManaNetwork()
        {
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();
            if (map == null || !cell.InBounds(map))
            {
                return;
            }

            ManaNetwork network = FindNetworkAtOrNear(map, cell);
            if (network == null)
            {
                Messages.Message("No mana network found near cell.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            float added = network.Add(network.Capacity);
            WotfLog.Info("DevTools", $"Filled mana network at {cell}: {network.Stored:0.#}/{network.Capacity:0.#} (+{added:0.#}).");
            Messages.Message($"Mana network filled ({network.Stored:0.#}/{network.Capacity:0.#}).", MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction(Category, "Make spell tester", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MakeSpellTester(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned)
            {
                return;
            }

            Gene_ForestBloodline bloodline = EnsureMaxBloodline(pawn);
            if (bloodline == null)
            {
                Messages.Message("Failed to grant Forest Bloodline gene.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            EnsureArchmageSpellbook(pawn);
            EnsureSpellStaff(pawn);

            GameComponent_Spellbook bookComp = GameComponent_Spellbook.Current;
            if (bookComp == null)
            {
                Messages.Message("GameComponent_Spellbook is missing.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            int savedCount = 0;
            for (int i = 0; i < DamageSpellPresets.Length; i++)
            {
                (string name, string[] glyphNames) = DamageSpellPresets[i];
                var glyphs = new List<string>(glyphNames.Length);
                for (int g = 0; g < glyphNames.Length; g++)
                {
                    SpellGlyphDef glyphDef = DefDatabase<SpellGlyphDef>.GetNamed(glyphNames[g]);
                    bookComp.Unlock(glyphDef);
                    glyphs.Add(glyphDef.defName);
                }

                var recipe = new SpellRecipe
                {
                    name = name,
                    glyphs = glyphs
                };
                if (bookComp.SaveSlot(pawn, i, recipe, out string reason))
                {
                    savedCount++;
                }
                else
                {
                    WotfLog.Warning("DevTools", $"Failed to save preset spell '{name}' in slot {i}: {reason}");
                }
            }

            bookComp.SetActiveSlot(pawn, 0);
            SpellService.GrantCastAbilityToEligiblePawns();

            bloodline.RefreshMaxFromStats();
            bloodline.OffsetMana(bloodline.MaxMana - bloodline.CurrentMana);

            WotfLog.Info("DevTools", $"Configured {pawn.LabelShort} as spell tester ({savedCount} spells, mana {bloodline.CurrentMana:0.#}/{bloodline.MaxMana:0.#}).");
            Messages.Message($"{pawn.LabelShort} ready with {savedCount} damage spells.", pawn, MessageTypeDefOf.NeutralEvent, false);
        }

        private static Thing SpawnBuilding(ThingDef def, IntVec3 cell, Map map)
        {
            if (def == null || !cell.InBounds(map))
            {
                return null;
            }

            ThingDef stuff = GenStuff.DefaultStuffFor(def);
            Thing thing = ThingMaker.MakeThing(def, stuff);
            if (def.CanHaveFaction)
            {
                thing.SetFaction(Faction.OfPlayer);
            }

            return GenSpawn.Spawn(thing, cell, map, WipeMode.Vanish);
        }

        private static void PlaceChalkRing(Map map, IntVec3 center, int size, ThingDef chalkDef)
        {
            List<IntVec3> cells = RitualRing.Cells(center, size);
            for (int i = 0; i < cells.Count; i++)
            {
                IntVec3 cell = cells[i];
                if (!cell.InBounds(map))
                {
                    continue;
                }

                bool alreadyHas = map.thingGrid.ThingsListAt(cell).Any(t => t.def == chalkDef);
                if (!alreadyHas)
                {
                    SpawnBuilding(chalkDef, cell, map);
                }
            }
        }

        private static Thing SpawnManaCluster(Map map, IntVec3 center)
        {
            IntVec3[] nodeOffsets =
            {
                new IntVec3(5, 0, 0),
                new IntVec3(-5, 0, 0),
                new IntVec3(0, 0, 5),
                new IntVec3(0, 0, -5),
                new IntVec3(4, 0, 4),
                new IntVec3(-4, 0, -4)
            };

            IntVec3 nodeCell = IntVec3.Invalid;
            for (int i = 0; i < nodeOffsets.Length; i++)
            {
                IntVec3 candidate = center + nodeOffsets[i];
                if (candidate.InBounds(map) && candidate.Standable(map))
                {
                    nodeCell = candidate;
                    break;
                }
            }

            if (!nodeCell.IsValid)
            {
                nodeCell = FindOpenCellNear(map, center, 8, requireUnroofed: false);
            }

            if (!nodeCell.IsValid)
            {
                return null;
            }

            Thing node = SpawnBuilding(DefDatabase<ThingDef>.GetNamed("WotF_ManaNode"), nodeCell, map);
            ThingDef batteryDef = DefDatabase<ThingDef>.GetNamed("WotF_ManaBattery");

            int batteriesPlaced = 0;
            foreach (IntVec3 adj in GenAdj.CardinalDirections)
            {
                IntVec3 batCell = nodeCell + adj;
                if (!batCell.InBounds(map) || !batCell.Standable(map))
                {
                    continue;
                }

                SpawnBuilding(batteryDef, batCell, map);
                batteriesPlaced++;
                if (batteriesPlaced >= 2)
                {
                    break;
                }
            }

            return node;
        }

        private static int SpawnElfCasters(Map map, IntVec3 center, int count)
        {
            PawnKindDef elfKind = DefDatabase<PawnKindDef>.GetNamed("WotF_Elf_Colonist");
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                IntVec3 cell = FindOpenCellNear(map, center, 2, requireUnroofed: false);
                if (!cell.IsValid)
                {
                    cell = FindOpenCellNear(map, center, 5, requireUnroofed: false);
                }

                if (!cell.IsValid)
                {
                    continue;
                }

                var request = new PawnGenerationRequest(
                    elfKind,
                    Faction.OfPlayer,
                    PawnGenerationContext.NonPlayer,
                    map.Tile,
                    forceGenerateNewPawn: true);
                Pawn elf = PawnGenerator.GeneratePawn(request);
                GenSpawn.Spawn(elf, cell, map);
                EnsureMaxBloodline(elf);
                spawned++;
            }

            return spawned;
        }

        private static Gene_ForestBloodline EnsureMaxBloodline(Pawn pawn)
        {
            Gene_ForestBloodline bloodline = Gene_ForestBloodline.Get(pawn);
            if (bloodline == null)
            {
                if (!ModsConfig.BiotechActive || pawn.genes == null)
                {
                    return null;
                }

                GeneDef bloodlineDef = DefDatabase<GeneDef>.GetNamed("WotF_ForestBloodline");
                bloodline = pawn.genes.AddGene(bloodlineDef, false) as Gene_ForestBloodline ?? Gene_ForestBloodline.Get(pawn);
            }

            if (bloodline == null)
            {
                return null;
            }

            while (bloodline.Level < bloodline.MaxLevel)
            {
                bloodline.GainExperience(10000f);
            }

            bloodline.RefreshMaxFromStats();
            bloodline.OffsetMana(bloodline.MaxMana - bloodline.CurrentMana);
            return bloodline;
        }

        private static void EnsureArchmageSpellbook(Pawn pawn)
        {
            if (pawn.apparel == null)
            {
                return;
            }

            ThingDef archmageDef = DefDatabase<ThingDef>.GetNamed("WotF_Spellbook_Archmage");
            Apparel existing = SpellService.WornSpellbook(pawn);
            if (existing != null)
            {
                if (existing.def == archmageDef)
                {
                    return;
                }

                pawn.apparel.Remove(existing);
                existing.Destroy();
            }

            Apparel book = (Apparel)ThingMaker.MakeThing(archmageDef, GenStuff.DefaultStuffFor(archmageDef));
            pawn.apparel.Wear(book, dropReplacedApparel: false);
        }

        private static void EnsureSpellStaff(Pawn pawn)
        {
            if (pawn.equipment == null)
            {
                return;
            }

            if (pawn.equipment.Primary?.GetComp<CompSpellStaff>() != null)
            {
                return;
            }

            if (pawn.equipment.Primary != null)
            {
                pawn.equipment.TryDropEquipment(pawn.equipment.Primary, out _, pawn.Position);
            }

            ThingDef staffDef = DefDatabase<ThingDef>.GetNamed("WotF_SpellStaff");
            ThingWithComps staff = (ThingWithComps)ThingMaker.MakeThing(staffDef, GenStuff.DefaultStuffFor(staffDef));
            pawn.equipment.AddEquipment(staff);
        }

        private static ManaNetwork FindNetworkAtOrNear(Map map, IntVec3 cell)
        {
            List<Thing> things = map.thingGrid.ThingsListAt(cell);
            for (int i = 0; i < things.Count; i++)
            {
                CompManaNode node = things[i].TryGetComp<CompManaNode>();
                if (node?.Network != null)
                {
                    return node.Network;
                }

                CompManaBattery battery = things[i].TryGetComp<CompManaBattery>();
                if (battery?.Network != null)
                {
                    return battery.Network;
                }
            }

            return MapComponent_ManaNetwork.For(map)?.NetworkNear(cell, DefaultManaLinkRadius);
        }

        private static IntVec3 FindOpenCellNear(Map map, IntVec3 center, int radius, bool requireUnroofed)
        {
            if (CellFinder.TryFindRandomCellNear(
                center,
                map,
                radius,
                c => c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map) == null && (!requireUnroofed || !c.Roofed(map)),
                out IntVec3 result))
            {
                return result;
            }

            return IntVec3.Invalid;
        }
    }
}
