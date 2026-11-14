using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.AncientRuins
{
    /// <summary>
    /// The one ruin quest root both ruin types share. It is a plain quest node: it finds a tile, puts a
    /// site there and arms the two clocks. What the ruin contains is decided entirely by the site part's
    /// data — the gen steps linked to it (ruins, temple, guards, loot, casket) and their parameters — so
    /// this class has no idea whether it is building a forest ruin or an engineering station.
    ///
    /// It does not inherit the vanilla loot-complex root: that one refuses to run without Ideology
    /// (QuestNode_Root_Loot_AncientComplex.cs:16-24) and needs a ComplexLayoutDef, neither of which this
    /// mod may depend on.
    /// </summary>
    public class QuestNode_Root_RuinSeek : QuestNode
    {
        /// <summary>
        /// Quest points to the site's threat points, mirroring the vanilla curve
        /// (QuestNode_Root_AncientComplex.cs:11-16). The per-ruin factor is already inside the points by
        /// the time they get here (RuinSeeking.PointsFor), so nothing is multiplied twice.
        /// </summary>
        private static readonly SimpleCurve SiteThreatOverPoints = new SimpleCurve
        {
            new CurvePoint(35f, 38.5f),
            new CurvePoint(400f, 165f),
            new CurvePoint(10000f, 4125f)
        };

        public SitePartDef sitePart;

        public float worldMapDays;

        public float mapDays;

        public int minDistanceFromColony;

        public int maxDistanceFromColony;

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            Quest quest = QuestGen.quest;
            float points = slate.Get("points", 0f);
            if (!slate.TryGet<PlanetTile>("siteTile", out PlanetTile tile))
            {
                TryFindSiteTile(out tile);
            }

            TryFindEnemyFaction(out Faction _);
            QuestGen.GenerateNewSignal("RaidArrives");
            string mapRemoved = QuestGenUtility.HardcodedSignalWithQuestID("site.MapRemoved");
            string noActiveThreats = QuestGenUtility.HardcodedSignalWithQuestID("site.NoActiveThreats");

            Site site = QuestGen_Sites.GenerateSite(Gen.YieldSingle(new SitePartDefWithParams(
                sitePart,
                new SitePartParams
                {
                    randomValue = Rand.Int,
                    threatPoints = Find.Storyteller.difficulty.allowViolentQuests
                        ? SiteThreatOverPoints.Evaluate(points)
                        : 0f
                })), tile, Faction.OfAncients);
            quest.SpawnWorldObject(site);

            TimedDetectionRaids detection = site.GetComponent<TimedDetectionRaids>();
            if (detection != null)
            {
                detection.alertRaidsArrivingIn = true;
            }

            // The site is cleared when no hostile active threat is left on its map, counting dormant
            // pawns as hostile (Site.cs:324-327), which is exactly what the sleeping guards are.
            quest.End(QuestEndOutcome.Success, 0, null, noActiveThreats);
            quest.End(QuestEndOutcome.Unknown, 0, null, mapRemoved);

            // Two clocks: the world-map one stops as soon as the site map is generated (vanilla
            // QuestPart_WorldObjectTimeout), and the in-map one starts on the first visit. The second
            // one lives on the game component, not on a quest part, because the quest ends the moment
            // the guards die while the deadline must still hold (Quest.cs:252-263).
            QuestPartUtility.MakeAndAddQuestTimeoutDelay(quest, Mathf.RoundToInt(worldMapDays * GenDate.TicksPerDay), site);
            GameComponent_RuinDeadlines.Current?.Track(site, Mathf.RoundToInt(mapDays * GenDate.TicksPerDay));

            slate.Set("site", site);
        }

        private bool TryFindSiteTile(out PlanetTile tile, bool exitOnFirstTileFound = false)
        {
            return TileFinder.TryFindNewSiteTile(out tile, minDistanceFromColony, maxDistanceFromColony,
                allowCaravans: false, null, 0.5f, canSelectComboLandmarks: true,
                TileFinderMode.Near, exitOnFirstTileFound);
        }

        private static bool TryFindEnemyFaction(out Faction enemyFaction)
        {
            enemyFaction = Find.FactionManager.RandomEnemyFaction();
            return enemyFaction != null;
        }

        protected override bool TestRunInt(Slate slate)
        {
            return TryFindSiteTile(out PlanetTile _, exitOnFirstTileFound: true)
                && TryFindEnemyFaction(out Faction _);
        }
    }
}
