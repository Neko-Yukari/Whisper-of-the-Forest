using System;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Lets "-quicktest" build its game from a chosen scenario instead of Crashlanded:
    /// "-wotfscenario=&lt;ScenarioDef defName&gt;" (Tools/launch-lab.ps1 -QuickTest -Scenario).
    ///
    /// Vanilla Root_Play.Start calls Root_Play.SetupForQuickTestPlay when no game exists
    /// (Verse/Root_Play.cs:41, :85-98); that method hard-codes ScenarioDefOf.Crashlanded.scenario (:91)
    /// and then runs the same steps a start page would: PreConfigure, world generation (whose
    /// Scenario.PostWorldGenerate creates the scenario's player faction, RimWorld.Planet/
    /// WorldGenerator.cs:68; ScenPart_PlayerFaction.PostWorldGenerate, RimWorld/ScenPart_PlayerFaction.cs:
    /// 51-55) and PostIdeoChosen, which generates the starting-page pawns through
    /// StartingPawnUtility.AddNewPawn (RimWorld/ScenPart_ConfigPage_ConfigureStartingPawnsBase.cs:41-45).
    /// This prefix runs that same body with the chosen scenario in place of Crashlanded and nothing
    /// else changed, so the player faction, its ideo and relations, and the pawns are the scenario's own.
    ///
    /// Without the switch the vanilla method runs untouched. A name that is not a ScenarioDef throws, so
    /// a typo fails the map generation loudly instead of quietly starting Crashlanded.
    /// </summary>
    [HarmonyPatch(typeof(Root_Play), nameof(Root_Play.SetupForQuickTestPlay))]
    internal static class Patch_QuickTestScenario
    {
        private const string SwitchPrefix = "-wotfscenario=";

        private static bool Prefix()
        {
            string name = ReadSwitch();
            if (name == null)
            {
                return true;
            }

            ScenarioDef def = DefDatabase<ScenarioDef>.GetNamedSilentFail(name);
            if (def == null)
            {
                throw new InvalidOperationException("[WotF-Lab] -wotfscenario=" + name + " is not a ScenarioDef");
            }

            Log.Message("[WotF-Lab] quicktest scenario: " + def.defName);

            Current.ProgramState = ProgramState.Entry;
            Game.ClearCaches();
            Current.Game = new Game();
            Current.Game.InitData = new GameInitData();
            Current.Game.Scenario = def.scenario;
            Find.Scenario.PreConfigure();
            Current.Game.storyteller = new Storyteller(StorytellerDefOf.Cassandra, DifficultyDefOf.Rough);
            Current.Game.World = WorldGenerator.GenerateWorld(0.3f, GenText.RandomSeedString(), OverallRainfall.Normal,
                OverallTemperature.Normal, OverallPopulation.Normal, LandmarkDensity.Normal);
            Find.GameInitData.ChooseRandomStartingTile();
            Find.GameInitData.mapSize = 250;
            Find.Scenario.PostIdeoChosen();
            return false;
        }

        private static string ReadSwitch()
        {
            string name = null;
            foreach (string raw in Environment.GetCommandLineArgs())
            {
                if (raw != null && raw.StartsWith(SwitchPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    name = raw.Substring(SwitchPrefix.Length).Trim().Trim('"');
                }
            }

            return string.IsNullOrEmpty(name) ? null : name;
        }
    }
}
