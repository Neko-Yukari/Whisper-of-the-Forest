using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Race
{
    /// <summary>
    /// Gives the elf start page elf candidates instead of baseliners.
    ///
    /// Vanilla: StartingPawnUtility.cs:53 builds every starting-pawn request with
    /// <c>forcedXenotype = ModsConfig.BiotechActive ? XenotypeDefOf.Baseliner : null</c>, and only the
    /// KindDefs scen part clears that (:118-130). WotF_Scenario_Elf uses the plain
    /// ScenPart_ConfigPage_ConfigureStartingPawns, whose GenerateStartingPawns (:64-77) and the base
    /// class top-up (ScenPart_ConfigPage_ConfigureStartingPawnsBase.cs:41-45) both reach
    /// AddNewPawn() -> NewGeneratedStartingPawn(-1) -> DefaultStartingPawnRequest
    /// (Verse/StartingPawnUtility.cs:311-313), so both paths read the getter patched here. A reroll
    /// reaches it too: HAR's NewGeneratedStartingPawnTranspiler re-reads the getter through the request
    /// it installs (erdelf/AlienRaces@b715c63ea7 Source/AlienRace/AlienRace/HarmonyPatches.cs:915-940,
    /// :878-882).
    ///
    /// HAR transpiles that getter (HarmonyPatches.cs:140-141): it swaps Faction.OfPlayer.def.basicMemberKind
    /// for NewGeneratedStartingPawnHelper and the baseliner literal for PickStartingPawnConfig
    /// (:3709-3745, :3761-3766). PickStartingPawnConfig resolves
    /// <c>currentStartingRequest.ForcedXenotype ?? XenotypeDefOf.Baseliner</c>, and currentStartingRequest
    /// only carries a value while a stored candidate is rerolled (:912, :927-940), so the first request
    /// stays the baseliner. CanUseXenotype then accepts it, because Elf.xml keeps Baseliner in
    /// whiteXenotypeList (ThingDef_AlienRace.cs:1079-1088).
    ///
    /// Baseliner has to stay on that whitelist: GeneUtility.ImplantXenogermItem resets the tracker with
    /// SetXenotype(XenotypeDefOf.Baseliner) before adding a pack's genes (RimWorld/GeneUtility.cs:207-222),
    /// and HAR's SetXenotype prefix drops the whole call when CanUseXenotype refuses it
    /// (HarmonyPatches.cs:960-961), so without Baseliner a second pack would stack on the first.
    ///
    /// This postfix therefore overrides the result after HAR is done: for an elf kind of the player's elf
    /// faction, a baseliner (or nothing) forced becomes the elf xenotype from the Defs. The player faction
    /// test is enough to keep it off other scenarios because the startingColonists entry maps
    /// WotF_Elf_Colonist to WotF_PlayerElf only (1.7/Defs/Factions/AlienRaceSettings.xml:21-35), and HAR
    /// offers a kind only to scenarios whose player faction def is listed there
    /// (HarmonyPatches.cs:3785-3788).
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_ElfStartingXenotype
    {
        private const string ElfRaceDefName = "WotF_Elf";
        private const string PlayerFactionDefName = "WotF_PlayerElf";
        private const string ElfXenotypeDefName = "WotF_ElfXenotype";

        private static bool resolved;
        private static bool active;
        private static ThingDef elfRace;
        private static FactionDef playerElfFaction;
        private static XenotypeDef elfXenotype;

        // StartingPawnUtility.DefaultStartingPawnRequest is a private static property, so it is bound by
        // name instead of by attribute.
        private static MethodBase TargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(StartingPawnUtility), "DefaultStartingPawnRequest")
                ?? throw new InvalidOperationException("StartingPawnUtility.DefaultStartingPawnRequest getter not found");
        }

        private static void Postfix(ref PawnGenerationRequest __result)
        {
            if (!ModsConfig.BiotechActive || !Resolve() || __result.KindDef?.race != elfRace)
                return;

            if (Faction.OfPlayerSilentFail?.def != playerElfFaction)
                return;

            XenotypeDef forced = __result.ForcedXenotype;
            if (forced != null && forced != XenotypeDefOf.Baseliner)
                return;

            __result.ForcedXenotype = elfXenotype;
        }

        // Defs are looked up once, after the game has loaded them; the getter runs for every candidate.
        private static bool Resolve()
        {
            if (resolved)
                return active;

            resolved = true;
            elfRace = DefDatabase<ThingDef>.GetNamedSilentFail(ElfRaceDefName);
            playerElfFaction = DefDatabase<FactionDef>.GetNamedSilentFail(PlayerFactionDefName);
            // The xenotype Def carries MayRequire="Ludeon.RimWorld.Biotech"; without Biotech there is no
            // XenotypeDef database entry and the check above already returned.
            elfXenotype = DefDatabase<XenotypeDef>.GetNamedSilentFail(ElfXenotypeDefName);

            active = elfRace != null && playerElfFaction != null && elfXenotype != null;
            if (!active)
            {
                Log.Error("[WotF] Elf starting-xenotype patch is inactive: missing Def "
                    + ElfRaceDefName + ", " + PlayerFactionDefName + " or " + ElfXenotypeDefName);
            }

            return active;
        }
    }
}
