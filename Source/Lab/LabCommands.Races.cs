using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Race checks for the elf: the faction icon and settlement material that world creation reads,
    /// the starting-page candidates of WotF_Scenario_Elf, and the vanilla xenogerm implant path that
    /// has to replace one gene pack with the next.
    ///
    /// All three only read game state, except "xenogerm": that one mutates the selected pawn exactly
    /// the way the vanilla implant recipe does, because the replacement is the thing being measured.
    /// </summary>
    internal static partial class LabCommands
    {
        private const string ElfKindDefName = "WotF_Elf_Colonist";

        /// <summary>
        /// Builds a settlement for the elf faction and renders both things world creation touches:
        /// Settlement.Material (Settlement.cs:69-80, the call that throws ArgumentNullException when
        /// settlementTexturePath is empty) and Settlement.ExpandingIcon (Settlement.cs:40 ->
        /// FactionDef.FactionIcon).
        /// </summary>
        private static string SettlementReport(string id, Dictionary<string, string> args)
        {
            if (Current.Game == null || Find.FactionManager == null)
            {
                throw new LabCommandException("no game loaded");
            }

            string defName = Get(args, "def", "WotF_GreenCourt");
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                throw new LabCommandException("unknown faction def '" + defName + "'");
            }

            // A bare faction carries the def and colour Settlement.Material reads. It is not put into
            // FactionManager: NewGeneratedFaction writes a relation into every real faction
            // (RimWorld/FactionGenerator.cs:180-185) and gives a non-player def a settlement, both of
            // which would outlive this probe in the loaded save.
            Faction existing = Find.FactionManager.FirstFactionOfDef(def);
            Faction faction = existing ?? new Faction { def = def };

            Settlement settlement = WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement) as Settlement;
            if (settlement == null)
            {
                throw new LabCommandException("the Settlement world object def is missing");
            }

            settlement.SetFaction(faction);

            bool materialOk = false;
            string materialError = null;
            try
            {
                materialOk = settlement.Material != null;
            }
            catch (Exception exception)
            {
                materialError = exception.GetType().Name + ": " + exception.Message;
            }

            bool iconOk = false;
            string iconError = null;
            int iconWidth = 0;
            int iconHeight = 0;
            try
            {
                var icon = settlement.ExpandingIcon;
                iconOk = icon != null;
                if (iconOk)
                {
                    iconWidth = icon.width;
                    iconHeight = icon.height;
                }
            }
            catch (Exception exception)
            {
                iconError = exception.GetType().Name + ": " + exception.Message;
            }

            // The probe is never added to Find.WorldObjects, so nothing else can reach it; Destroy is
            // deliberately skipped because it would run FactionManager.Notify_WorldObjectDestroyed on the
            // real faction (RimWorld/FactionManager.cs:356-362).

            StringBuilder json = new StringBuilder(512);
            json.Append("\"faction\":").Append(Json.Str(def.defName));
            json.Append(",\"factionInSave\":").Append(Json.Bool(existing != null));
            json.Append(",\"settlementTexturePath\":").Append(Json.Str(def.settlementTexturePath));
            json.Append(",\"factionIconPath\":").Append(Json.Str(def.factionIconPath));
            json.Append(",\"materialOk\":").Append(Json.Bool(materialOk));
            json.Append(",\"materialError\":").Append(Json.Str(materialError));
            json.Append(",\"expandingIconOk\":").Append(Json.Bool(iconOk));
            json.Append(",\"expandingIconError\":").Append(Json.Str(iconError));
            json.Append(",\"expandingIconSize\":").Append(Json.Cell(new IntVec3(iconWidth, 0, iconHeight)));
            json.Append(",\"ok\":").Append(Json.Bool(materialOk && iconOk));
            return Ok(id, json.ToString());
        }

        /// <summary>
        /// Generates the scenario's starting candidates through the same request the start page reads.
        ///
        /// Vanilla: ScenPart_ConfigPage_ConfigureStartingPawnsBase.PostIdeoChosen generates
        /// TotalPawnCount pawns, then tops the list up to pawnChoiceCount with
        /// StartingPawnUtility.AddNewPawn() (ScenPart_ConfigPage_ConfigureStartingPawnsBase.cs:41-45),
        /// and AddNewPawn(-1) reads StartingPawnUtility.DefaultStartingPawnRequest
        /// (Verse/StartingPawnUtility.cs:308-320).
        ///
        /// HAR rewrites that request: NewGeneratedStartingPawnHelper picks the kind from
        /// RaceSettings.pawnKindSettings.startingColonists for Faction.OfPlayer.def, and
        /// PickStartingPawnConfig picks the xenotype from currentStartingRequest (erdelf/AlienRaces
        /// b715c63 HarmonyPatches.cs:3761-3782). currentStartingRequest only carries a value while a
        /// candidate is rerolled (:919-940, :878-882), so the request the page reads the first time
        /// resolves xenotypeDef = Baseliner (ThingDef_AlienRace.cs:1079-1088 accepts it for WotF_Elf).
        /// Patch_ElfStartingXenotype (Race/ElfStartingXenotype.cs) postfixes the same getter and replaces
        /// that forced baseliner with WotF_ElfXenotype, so forcedXenotype below must read that xenotype
        /// and every candidate must come out as it. The reroll path (HAR transpiler on
        /// NewGeneratedStartingPawn) reads the same getter but needs the start page's stored requests,
        /// which this command does not simulate, so it is not covered here.
        ///
        /// Needs a game started from the scenario: Tools/launch-lab.ps1 -QuickTest -Scenario
        /// WotF_Scenario_Elf (Source/Lab/LabQuickScenario.cs), so Faction.OfPlayer is the scenario's own
        /// WotF_PlayerElf with its ideo and relation table. Any other player faction is an error; nothing
        /// is substituted. The request is read from the live getter and the candidates are generated
        /// from it. The same game also holds the real start page's pawns: the colonists (see "observe")
        /// and the candidates PrepForMapGen left behind (Verse/GameInitData.cs:78-86), reported as
        /// "leftBehind".
        /// </summary>
        private static string StartersReport(string id, Dictionary<string, string> args)
        {
            if (Current.Game == null || Find.FactionManager == null)
            {
                throw new LabCommandException("no game loaded");
            }

            if (!ModsConfig.BiotechActive)
            {
                throw new LabCommandException("Biotech is not active, so starting pawns carry no xenotype");
            }

            string scenName = Get(args, "scen", "WotF_Scenario_Elf");
            ScenarioDef scenDef = DefDatabase<ScenarioDef>.GetNamedSilentFail(scenName);
            if (scenDef == null)
            {
                throw new LabCommandException("unknown scenario def '" + scenName + "'");
            }

            ScenPart_ConfigPage_ConfigureStartingPawnsBase config =
                scenDef.scenario.AllParts.OfType<ScenPart_ConfigPage_ConfigureStartingPawnsBase>().FirstOrDefault();
            if (config == null)
            {
                throw new LabCommandException("scenario '" + scenName + "' has no starting-pawns config part");
            }

            int count = args.ContainsKey("count") ? RequireInt(args, "count") : config.pawnChoiceCount;

            string factionName = Get(args, "faction", "WotF_PlayerElf");
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(factionName);
            if (factionDef == null)
            {
                throw new LabCommandException("unknown faction def '" + factionName + "'");
            }

            Faction player = Faction.OfPlayer;
            if (player == null || player.def != factionDef)
            {
                throw new LabCommandException("the player faction of this game is '"
                    + (player != null ? player.def.defName : null) + "', not '" + factionName
                    + "'; start it with Tools/launch-lab.ps1 -QuickTest -Scenario " + scenName);
            }

            PawnGenerationRequest request = ReadDefaultStartingRequest();

            StringBuilder json = new StringBuilder(2048);
            json.Append("\"scenario\":").Append(Json.Str(scenName));
            json.Append(",\"pawnCount\":").Append(Json.Num(config.pawnChoiceCount));
            json.Append(",\"gameScenario\":").Append(Json.Str(Current.Game.Scenario != null ? Current.Game.Scenario.name : null));
            json.Append(",\"playerFaction\":").Append(Json.Str(player.def.defName));
            json.Append(",\"playerFactionHasIdeo\":").Append(Json.Bool(player.ideos != null && player.ideos.PrimaryIdeo != null));
            json.Append(",\"requestKind\":").Append(Json.Str(request.KindDef != null ? request.KindDef.defName : null));
            json.Append(",\"requestFaction\":")
                .Append(Json.Str(request.Faction != null && request.Faction.def != null ? request.Faction.def.defName : null));
            json.Append(",\"forcedXenotype\":")
                .Append(Json.Str(request.ForcedXenotype != null ? request.ForcedXenotype.defName : null));

            string expectedXenotype = ExpectedXenotypeDefName();
            json.Append(",\"kindXenotypeSet\":").Append(Json.Str(expectedXenotype));

            List<Pawn> candidates = new List<Pawn>();
            json.Append(",\"candidates\":[");
            for (int i = 0; i < count; i++)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(request);
                if (pawn == null)
                {
                    throw new LabCommandException("PawnGenerator returned null for candidate " + i);
                }

                candidates.Add(pawn);
                if (i > 0)
                {
                    json.Append(',');
                }

                AppendStarterPawn(json, pawn, i);
            }

            json.Append(']');

            List<Pawn> leftBehind = Find.WorldPawns.AllPawnsAliveOrDead.Where(p => p.wasLeftBehindStartingPawn).ToList();
            json.Append(",\"leftBehind\":[");
            for (int i = 0; i < leftBehind.Count; i++)
            {
                if (i > 0)
                {
                    json.Append(',');
                }

                AppendStarterPawn(json, leftBehind[i], i);
            }

            json.Append(']');
            json.Append(",\"leftBehindXenotypeMatches\":")
                .Append(Json.Num(leftBehind.Count(p => p.genes != null && p.genes.Xenotype.defName == expectedXenotype)));

            int xenotypeMatches = candidates.Count(p => p.genes != null && p.genes.Xenotype.defName == expectedXenotype);
            int geneLess = candidates.Count(p => p.genes == null || p.genes.GenesListForReading.Count == 0);
            json.Append(",\"xenotypeMatches\":").Append(Json.Num(xenotypeMatches));
            json.Append(",\"geneLess\":").Append(Json.Num(geneLess));
            json.Append(",\"allExpectedXenotype\":").Append(Json.Bool(xenotypeMatches == candidates.Count));

            // Same cleanup PawnGenerator applies to a discarded candidate (Verse/PawnGenerator.cs:1111-1116):
            // generation can hand a starter pawn to WorldPawns, so it is removed and discarded rather
            // than destroyed in place.
            foreach (Pawn candidate in candidates)
            {
                try
                {
                    candidate.relations?.ClearAllRelations();
                    if (Find.WorldPawns.Contains(candidate))
                    {
                        Find.WorldPawns.RemovePawn(candidate);
                    }

                    Find.WorldPawns.PassToWorld(candidate, PawnDiscardDecideMode.Discard);
                }
                catch (Exception exception)
                {
                    Log.Warning("[WotF-Lab] could not discard a starting-page candidate: " + exception.Message);
                }
            }

            return Ok(id, json.ToString());
        }

        private static void AppendStarterPawn(StringBuilder json, Pawn pawn, int index)
        {
            json.Append('{');
            json.Append("\"index\":").Append(Json.Num(index));
            json.Append(",\"name\":").Append(Json.Str(pawn.Name != null ? pawn.Name.ToStringShort : pawn.LabelShort));
            json.Append(",\"race\":").Append(Json.Str(pawn.def != null ? pawn.def.defName : null));
            json.Append(",\"kind\":").Append(Json.Str(pawn.kindDef != null ? pawn.kindDef.defName : null));
            AppendGeneState(json, pawn);
            json.Append('}');
        }

        /// <summary>
        /// Reads StartingPawnUtility.DefaultStartingPawnRequest in the running game. The getter
        /// dereferences Find.GameInitData (Verse/StartingPawnUtility.cs:50), which Game.InitNewGame
        /// clears once the game has started (Verse/Game.cs:553), so an empty one is created for the
        /// call exactly as Dialog_ChooseNewWanderers.PreOpen does (RimWorld/Dialog_ChooseNewWanderers.cs:62) and cleared again afterwards. The player faction is not touched.
        /// </summary>
        private static PawnGenerationRequest ReadDefaultStartingRequest()
        {
            PropertyInfo property = typeof(StartingPawnUtility).GetProperty(
                "DefaultStartingPawnRequest",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
            {
                throw new LabCommandException("StartingPawnUtility.DefaultStartingPawnRequest is missing");
            }

            bool createdInitData = Find.GameInitData == null;
            if (createdInitData)
            {
                Current.Game.InitData = new GameInitData();
            }

            try
            {
                return (PawnGenerationRequest)property.GetValue(null, null);
            }
            finally
            {
                if (createdInitData)
                {
                    Current.Game.InitData = null;
                }
            }
        }

        /// <summary>The xenotype WotF_Elf_Colonist declares in its own xenotypeSet.</summary>
        private static string ExpectedXenotypeDefName()
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(ElfKindDefName);
            if (kind == null || kind.xenotypeSet == null || kind.xenotypeSet.Count == 0)
            {
                return null;
            }

            XenotypeChance chance = kind.xenotypeSet[0];
            return chance != null && chance.xenotype != null ? chance.xenotype.defName : null;
        }

        /// <summary>
        /// Implants two gene packs into the selected pawn through GeneUtility.ImplantXenogermItem, the
        /// method Recipe_ImplantXenogerm calls for the vanilla implant bill
        /// (RimWorld/Recipe_ImplantXenogerm.cs:42), and reports the gene tracker after each one.
        ///
        /// The replacement hinges on SetXenotype(XenotypeDefOf.Baseliner) inside that method
        /// (RimWorld/GeneUtility.cs:207-222): it clears the xenogenes before the pack is added, and
        /// HAR's SetXenotype prefix drops the whole call when the race refuses the baseliner
        /// (HarmonyPatches.cs:960-961).
        /// </summary>
        private static string XenogermReport(string id, Dictionary<string, string> args)
        {
            if (!ModsConfig.BiotechActive)
            {
                throw new LabCommandException("Biotech is not active, so there is no xenogerm path");
            }

            Pawn pawn = RequireSelectedPawn();
            if (pawn.genes == null)
            {
                throw new LabCommandException("selected pawn has no gene tracker");
            }

            List<GeneDef> first = ResolveGenes(Get(args, "pack1", "WoundHealing_Fast,Immunity_Strong"));
            List<GeneDef> second = ResolveGenes(Get(args, "pack2", "DarkVision,FireResistant"));

            StringBuilder json = new StringBuilder(1024);
            json.Append("\"pawn\":").Append(Json.Num(pawn.thingIDNumber));
            json.Append(",\"before\":{");
            AppendGeneState(json, pawn);
            json.Append('}');

            ImplantPack(pawn, first, "LabPack1");
            json.Append(",\"afterPack1\":{");
            List<string> xenogenesAfterFirst = AppendXenogenes(json, pawn);
            json.Append('}');

            ImplantPack(pawn, second, "LabPack2");
            json.Append(",\"pack1\":");
            AppendGeneNames(json, first);
            json.Append(",\"afterPack2\":{");
            List<string> xenogenesAfterSecond = AppendXenogenes(json, pawn);
            json.Append('}');
            json.Append(",\"pack2\":");
            AppendGeneNames(json, second);

            bool pack1Added = first.All(g => xenogenesAfterFirst.Contains(g.defName));
            bool pack1Gone = first.All(g => !xenogenesAfterSecond.Contains(g.defName));
            bool pack2Added = second.All(g => xenogenesAfterSecond.Contains(g.defName));
            bool replaced = pack1Added && pack1Gone && pack2Added && xenogenesAfterSecond.Count == second.Count;

            json.Append(",\"pack1Added\":").Append(Json.Bool(pack1Added));
            json.Append(",\"pack1Gone\":").Append(Json.Bool(pack1Gone));
            json.Append(",\"pack2Added\":").Append(Json.Bool(pack2Added));
            json.Append(",\"replaced\":").Append(Json.Bool(replaced));
            json.Append(",\"xenotypeAfter\":")
                .Append(Json.Str(pawn.genes.Xenotype != null ? pawn.genes.Xenotype.defName : null));
            return Ok(id, json.ToString());
        }

        /// <summary>
        /// One implant through the vanilla path: a genepack holding the genes, a xenogerm built from it
        /// (Xenogerm.Initialize, RimWorld/Xenogerm.cs:109-127), then ImplantXenogermItem.
        /// </summary>
        private static void ImplantPack(Pawn pawn, List<GeneDef> genes, string xenotypeName)
        {
            Genepack pack = (Genepack)ThingMaker.MakeThing(ThingDefOf.Genepack);
            Xenogerm xenogerm = (Xenogerm)ThingMaker.MakeThing(ThingDefOf.Xenogerm);
            try
            {
                pack.Initialize(genes);
                xenogerm.Initialize(new List<Genepack> { pack }, xenotypeName, XenotypeIconDefOf.Basic);
                GeneUtility.ImplantXenogermItem(pawn, xenogerm);
            }
            finally
            {
                xenogerm.Destroy();
                pack.Destroy();
            }
        }

        private static List<GeneDef> ResolveGenes(string csv)
        {
            List<GeneDef> genes = new List<GeneDef>();
            foreach (string raw in csv.Split(','))
            {
                string name = raw.Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(name);
                if (gene == null)
                {
                    throw new LabCommandException("unknown gene def '" + name + "'");
                }

                genes.Add(gene);
            }

            if (genes.Count == 0)
            {
                throw new LabCommandException("no gene defs given");
            }

            return genes;
        }

        /// <summary>Writes the xenotype and the gene lists without the closing brace of the caller's object.</summary>
        private static void AppendGeneState(StringBuilder json, Pawn pawn)
        {
            Pawn_GeneTracker tracker = pawn.genes;
            json.Append("\"xenotype\":").Append(Json.Str(tracker != null && tracker.Xenotype != null ? tracker.Xenotype.defName : null));
            json.Append(",\"xenotypeLabel\":").Append(Json.Str(tracker != null && tracker.Xenotype != null ? tracker.Xenotype.label : null));
            json.Append(",\"xenotypeName\":").Append(Json.Str(tracker != null ? tracker.xenotypeName : null));
            json.Append(",\"geneCount\":").Append(Json.Num(tracker != null ? tracker.GenesListForReading.Count : 0));
            json.Append(",\"genes\":");
            AppendGeneNames(json, tracker != null ? tracker.GenesListForReading.Select(g => g.def).ToList() : new List<GeneDef>());
            json.Append(",\"xenogenes\":[");
            List<string> xenogenes = XenogeneNames(tracker);
            for (int i = 0; i < xenogenes.Count; i++)
            {
                if (i > 0)
                {
                    json.Append(',');
                }

                json.Append(Json.Str(xenogenes[i]));
            }

            json.Append(']');
        }

        /// <summary>Appends a "\"xenogenes\":[...]" field and returns the names it wrote.</summary>
        private static List<string> AppendXenogenes(StringBuilder json, Pawn pawn)
        {
            List<string> names = XenogeneNames(pawn.genes);
            json.Append("\"xenogenes\":[");
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0)
                {
                    json.Append(',');
                }

                json.Append(Json.Str(names[i]));
            }

            json.Append(']');
            return names;
        }

        private static List<string> XenogeneNames(Pawn_GeneTracker tracker)
        {
            List<string> names = new List<string>();
            if (tracker == null)
            {
                return names;
            }

            foreach (Gene gene in tracker.Xenogenes)
            {
                if (gene != null && gene.def != null)
                {
                    names.Add(gene.def.defName);
                }
            }

            return names;
        }

        private static void AppendGeneNames(StringBuilder json, List<GeneDef> genes)
        {
            json.Append('[');
            int written = 0;
            for (int i = 0; i < genes.Count && written < 32; i++)
            {
                GeneDef gene = genes[i];
                if (gene == null)
                {
                    continue;
                }

                if (written > 0)
                {
                    json.Append(',');
                }

                json.Append(Json.Str(gene.defName));
                written++;
            }

            json.Append(']');
        }
    }
}
