using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace WhisperOfTheForest.AncientRuins
{
    /// <summary>
    /// The single place that answers "may the colony point at a ruin" and "run that quest". The altar
    /// asks <see cref="CanSeek"/> before anybody pays, and the effect worker calls
    /// <see cref="RunQuest"/> only after the chant completed, so a quest that cannot place a site is
    /// refused up front instead of costing mana for nothing.
    /// </summary>
    public static class RuinSeeking
    {
        /// <summary>
        /// True while a ruin this mod sought is still un-resolved on the world map. Both the offered
        /// letter and the ongoing quest count: either way the colony already knows of a ruin it has
        /// not cleared, and a second one is refused.
        /// </summary>
        public static bool HasOutstandingRuin()
        {
            List<Quest> quests = Find.QuestManager.QuestsListForReading;
            for (int i = 0; i < quests.Count; i++)
            {
                Quest quest = quests[i];
                if (quest.State != QuestState.NotYetAccepted && quest.State != QuestState.Ongoing)
                {
                    continue;
                }

                if (quest.root.root is QuestNode_Root_RuinSeek)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Threat points the ruin is generated at; the per-ruin factor lives in the ritual Def.</summary>
        public static float PointsFor(Rituals.RuinSeekOption option, Map map)
        {
            return StorytellerUtility.DefaultThreatPointsNow(map) * option.threatPointsFactor;
        }

        /// <summary>
        /// The same gate the storyteller uses for random quests: the quest's own root test run plus the
        /// planet-layer restrictions. It finds a site tile and an enemy faction, so a pass here means
        /// <see cref="RunQuest"/> has everything it needs.
        /// </summary>
        public static bool CanSeek(Rituals.RuinSeekOption option, Map map)
        {
            var slate = new Slate();
            slate.Set("points", PointsFor(option, map));
            return option.quest.CanRun(slate, map);
        }

        /// <summary>
        /// Generates and accepts the quest, then hands back the site it put on the world map. A null
        /// return means the engine refused to place the world object; the caller reports that instead
        /// of pretending a ruin was found.
        /// </summary>
        public static Site RunQuest(Rituals.RuinSeekOption option, Map map)
        {
            float points = PointsFor(option, map);
            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(option.quest, points);
            if (quest == null)
            {
                // QuestGen returns null when generation threw (QuestGen.cs:120-180); it already logged
                // the exception, so this line names what was being generated. Nothing is retried and
                // nothing is compensated: the rite simply finds no ruin and says so.
                WotfLog.Error(WotfLog.ModuleRuin, "quest generation returned no quest for "
                    + option.quest.defName + " at points " + points.ToString("0.#")
                    + "; the QuestGen error earlier in the log is the cause");
                return null;
            }

            if (!quest.TryGetFirstPartOfType(out QuestPart_SpawnWorldObject spawn)
                || !(spawn.worldObject is Site site)
                || !site.Spawned)
            {
                WotfLog.Info(WotfLog.ModuleRuin, "the seeking quest produced no site: " + option.quest.defName);
                return null;
            }

            WotfLog.Info(WotfLog.ModuleRuin, "sought " + option.sitePart.defName + " at tile " + site.Tile
                + " for quest " + quest.id + " (" + option.quest.defName + ")");
            return site;
        }
    }
}
