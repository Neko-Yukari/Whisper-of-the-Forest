using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Rituals;

namespace WhisperOfTheForest.Tests
{
    /// <summary>
    /// Seam: ritual research gating requires every RitualDef to declare a researchPrerequisite and
    /// maps existing altar tiers on upgrade to the altar and ritual research projects up through that tier.
    /// </summary>
    internal static class RitualResearchTests
    {
        internal static IEnumerable<(string, Action)> All => new (string, Action)[]
        {
            ("ritual-research: ConfigErrors rejects a RitualDef without researchPrerequisite", RequiresResearchPrerequisite),
            ("ritual-research: save migration unlocks altar and ritual projects up to max altar tier", MigrationUnlocksUpToMaxTier),
        };

        private static void RequiresResearchPrerequisite()
        {
            var project = new ResearchProjectDef
            {
                defName = "T_Ritual_Research",
                baseCost = 100f,
                techLevel = TechLevel.Neolithic
            };
            var missing = BuildMinimalRitual("T_Ritual_MissingResearch", tier: 1, research: null);
            var valid = BuildMinimalRitual("T_Ritual_WithResearch", tier: 1, research: project);

            Check.True(
                missing.ConfigErrors().Any(e => e.Contains("researchPrerequisite")),
                "RitualDef without researchPrerequisite should report a ConfigError");
            Check.True(
                !valid.ConfigErrors().Any(e => e.Contains("researchPrerequisite")),
                "RitualDef with researchPrerequisite should pass researchPrerequisite check");
        }

        private static void MigrationUnlocksUpToMaxTier()
        {
            var pAltar1 = new ResearchProjectDef { defName = "T_Altar1" };
            var pAltar2 = new ResearchProjectDef { defName = "T_Altar2" };
            var pAltar3 = new ResearchProjectDef { defName = "T_Altar3" };
            var pRite1 = new ResearchProjectDef { defName = "T_Rite1" };
            var pRite2 = new ResearchProjectDef { defName = "T_Rite2" };
            var pRite3 = new ResearchProjectDef { defName = "T_Rite3" };

            var altars = new[]
            {
                BuildMinimalAltar("T_AltarDef1", 1, pAltar1),
                BuildMinimalAltar("T_AltarDef2", 2, pAltar2),
                BuildMinimalAltar("T_AltarDef3", 3, pAltar3),
            };
            var rituals = new[]
            {
                BuildMinimalRitual("T_RiteDef1", 1, pRite1),
                BuildMinimalRitual("T_RiteDef2", 2, pRite2),
                BuildMinimalRitual("T_RiteDef3", 3, pRite3),
            };

            Check.Equal(0, GameComponent_RitualResearch.ProjectsToUnlockForAltarTier(0, altars, rituals).Count, "tier 0 unlocks nothing");

            List<ResearchProjectDef> t1 = GameComponent_RitualResearch.ProjectsToUnlockForAltarTier(1, altars, rituals);
            Check.True(t1.Contains(pAltar1) && t1.Contains(pRite1), "tier 1 unlocks tier 1 altar and ritual");
            Check.True(!t1.Contains(pAltar2) && !t1.Contains(pRite2) && !t1.Contains(pAltar3) && !t1.Contains(pRite3), "tier 1 excludes higher tiers");

            List<ResearchProjectDef> t2 = GameComponent_RitualResearch.ProjectsToUnlockForAltarTier(2, altars, rituals);
            Check.True(t2.Contains(pAltar1) && t2.Contains(pAltar2) && t2.Contains(pRite1) && t2.Contains(pRite2), "tier 2 unlocks tier 1..2");
            Check.True(!t2.Contains(pAltar3) && !t2.Contains(pRite3), "tier 2 excludes tier 3");

            List<ResearchProjectDef> t3 = GameComponent_RitualResearch.ProjectsToUnlockForAltarTier(3, altars, rituals);
            Check.Equal(6, t3.Count, "tier 3 unlocks all 6 test projects");
        }

        private static ThingDef BuildMinimalAltar(string defName, int tier, ResearchProjectDef research)
        {
            return new ThingDef
            {
                defName = defName,
                researchPrerequisites = new List<ResearchProjectDef> { research },
                comps = new List<CompProperties> { new CompProperties_RitualAltar { tier = tier } }
            };
        }

        private static RitualDef BuildMinimalRitual(string defName, int tier, ResearchProjectDef research)
        {
            return new RitualDef
            {
                defName = defName,
                tier = tier,
                family = RitualFamily.Crafting,
                durationTicks = 60,
                effectWorkerClass = typeof(RitualEffect_ManaSpring),
                researchPrerequisite = research
            };
        }
    }
}
