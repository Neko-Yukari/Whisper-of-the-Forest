using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Upgrade compatibility for saves created before ritual research existed: once on load, if any
    /// map already holds a player altar of tier N, every main-chain node and ritual leaf up through
    /// tier N is marked finished so existing colonies keep their capabilities.
    /// </summary>
    public sealed class GameComponent_RitualResearch : GameComponent
    {
        private bool migrated;

        public GameComponent_RitualResearch(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref migrated, "ritualResearchMigrated", false);
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            migrated = true;
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            if (migrated)
            {
                return;
            }

            migrated = true;
            MigrateExistingAltars();
        }

        /// <summary>
        /// Collects all research projects unlocked by altars and rituals up through <paramref name="maxAltarTier"/>.
        /// Exposed as a pure helper so save-upgrade coverage can be verified offline without a running game.
        /// </summary>
        public static List<ResearchProjectDef> ProjectsToUnlockForAltarTier(
            int maxAltarTier,
            IEnumerable<ThingDef> thingDefs,
            IEnumerable<RitualDef> ritualDefs)
        {
            var result = new List<ResearchProjectDef>();
            if (maxAltarTier <= 0)
            {
                return result;
            }

            var seen = new HashSet<ResearchProjectDef>();
            if (thingDefs != null)
            {
                foreach (ThingDef thingDef in thingDefs)
                {
                    CompProperties_RitualAltar altarProps = thingDef?.GetCompProperties<CompProperties_RitualAltar>();
                    if (altarProps == null || altarProps.tier > maxAltarTier || thingDef.researchPrerequisites == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < thingDef.researchPrerequisites.Count; i++)
                    {
                        ResearchProjectDef project = thingDef.researchPrerequisites[i];
                        if (project != null && seen.Add(project))
                        {
                            result.Add(project);
                        }
                    }
                }
            }

            if (ritualDefs != null)
            {
                foreach (RitualDef ritual in ritualDefs)
                {
                    if (ritual != null && ritual.tier <= maxAltarTier && ritual.researchPrerequisite != null
                        && seen.Add(ritual.researchPrerequisite))
                    {
                        result.Add(ritual.researchPrerequisite);
                    }
                }
            }

            return result;
        }

        private static void MigrateExistingAltars()
        {
            int maxTier = MaxExistingAltarTier();
            if (maxTier <= 0)
            {
                return;
            }

            List<ResearchProjectDef> projects = ProjectsToUnlockForAltarTier(
                maxTier,
                DefDatabase<ThingDef>.AllDefsListForReading,
                DefDatabase<RitualDef>.AllDefsListForReading);
            int unlocked = 0;
            for (int i = 0; i < projects.Count; i++)
            {
                ResearchProjectDef project = projects[i];
                if (!project.IsFinished)
                {
                    Find.ResearchManager.FinishProject(project, doCompletionDialog: false, researcher: null, doCompletionLetter: false);
                    unlocked++;
                }
            }

            WotfLog.Info(WotfLog.ModuleRitual,
                "migrated ritual research for existing tier-" + maxTier + " altar(s), finished " + unlocked + " project(s)");
        }

        private static int MaxExistingAltarTier()
        {
            int maxTier = 0;
            List<Map> maps = Find.Maps;
            if (maps == null)
            {
                return 0;
            }

            for (int m = 0; m < maps.Count; m++)
            {
                List<Building> buildings = maps[m].listerBuildings?.allBuildingsColonist;
                if (buildings == null)
                {
                    continue;
                }

                for (int b = 0; b < buildings.Count; b++)
                {
                    CompRitualAltar altar = buildings[b].TryGetComp<CompRitualAltar>();
                    if (altar != null && altar.Props.tier > maxTier)
                    {
                        maxTier = altar.Props.tier;
                    }
                }
            }

            return maxTier;
        }
    }
}
