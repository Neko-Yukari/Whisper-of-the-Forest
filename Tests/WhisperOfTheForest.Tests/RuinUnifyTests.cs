using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.AncientRuins;
using WhisperOfTheForest.Rituals;
using WhisperOfTheForest.Teleport;

namespace WhisperOfTheForest.Tests
{
    /// <summary>
    /// Seam: single-ruin seeking binds ruinTarget directly on RitualDef and enforces its contract in
    /// ConfigErrors, while a ruin's broken teleport gate stays locked while active/dormant hostiles remain.
    /// </summary>
    internal static class RuinUnifyTests
    {
        internal static IEnumerable<(string, Action)> All => new (string, Action)[]
        {
            ("ruin-unify: RitualDef.ConfigErrors validates ruinTarget contract", ValidatesRuinTargetConfigErrors),
            ("ruin-unify: TeleportGateTransfer.CanReturnFromRuin blocks ruined gate while hostiles remain", BrokenGateReturnLock),
        };

        private static void ValidatesRuinTargetConfigErrors()
        {
            RitualDef valid = BuildSeekRuinDef("T_SeekRuin_Valid", MakeTarget(1.4f));
            Check.True(valid.SeeksRuin, "valid seek-ruin def should report SeeksRuin=true");
            Check.Equal(0, valid.ConfigErrors().Count(), "valid seek-ruin def should have zero ConfigErrors");

            RitualDef missingTarget = BuildSeekRuinDef("T_SeekRuin_MissingTarget", target: null);
            Check.True(!missingTarget.SeeksRuin, "missing ruinTarget should report SeeksRuin=false");
            Check.True(
                missingTarget.ConfigErrors().Any(e => e.Contains("ruinTarget")),
                "RitualEffect_SeekRuin without ruinTarget must report a ConfigError");

            RitualDef missingQuest = BuildSeekRuinDef("T_SeekRuin_MissingQuest", new RuinSeekOption
            {
                quest = null,
                sitePart = new SitePartDef { defName = "T_Site" },
                threatPointsFactor = 1f
            });
            Check.True(
                missingQuest.ConfigErrors().Any(e => e.Contains("ruinTarget")),
                "ruinTarget without quest must report a ConfigError");

            RitualDef missingSite = BuildSeekRuinDef("T_SeekRuin_MissingSite", new RuinSeekOption
            {
                quest = new QuestScriptDef { defName = "T_Quest" },
                sitePart = null,
                threatPointsFactor = 1f
            });
            Check.True(
                missingSite.ConfigErrors().Any(e => e.Contains("ruinTarget")),
                "ruinTarget without sitePart must report a ConfigError");

            RitualDef zeroFactor = BuildSeekRuinDef("T_SeekRuin_ZeroFactor", MakeTarget(0f));
            Check.True(
                zeroFactor.ConfigErrors().Any(e => e.Contains("threatPointsFactor")),
                "ruinTarget with threatPointsFactor == 0 must report a ConfigError");

            RitualDef negativeFactor = BuildSeekRuinDef("T_SeekRuin_NegFactor", MakeTarget(-0.5f));
            Check.True(
                negativeFactor.ConfigErrors().Any(e => e.Contains("threatPointsFactor")),
                "ruinTarget with negative threatPointsFactor must report a ConfigError");

            RitualDef nonSeekWithTarget = new RitualDef
            {
                defName = "T_NonSeek_WithTarget",
                tier = 2,
                family = RitualFamily.Domain,
                ringSize = 5,
                durationTicks = 60,
                effectWorkerClass = typeof(RitualEffect_ManaSpring),
                researchPrerequisite = new ResearchProjectDef { defName = "T_Research_NonSeek" },
                ruinTarget = MakeTarget(1f)
            };
            Check.True(
                nonSeekWithTarget.ConfigErrors().Any(e => e.Contains("sets ruinTarget without using RitualEffect_SeekRuin.")),
                "non-SeekRuin def setting ruinTarget must report exact mismatch ConfigError");

            RitualDef sustainedSeek = BuildSeekRuinDef("T_SeekRuin_Sustained", MakeTarget(1.4f));
            sustainedSeek.sustained = true;
            Check.True(
                sustainedSeek.ConfigErrors().Any(e => e.Contains("sustained")),
                "sustained RitualDef with ruinTarget must report a ConfigError");
        }

        private static void BrokenGateReturnLock()
        {
            Check.Equal(false, TeleportGateTransfer.CanReturnFromRuin(ruined: true, hasActiveHostiles: true),
                "ruined gate must refuse return while active hostiles remain");
            Check.Equal(true, TeleportGateTransfer.CanReturnFromRuin(ruined: true, hasActiveHostiles: false),
                "ruined gate must allow return once hostiles are cleared");
            Check.Equal(true, TeleportGateTransfer.CanReturnFromRuin(ruined: false, hasActiveHostiles: true),
                "non-ruined gate is unaffected by active hostiles");
            Check.Equal(true, TeleportGateTransfer.CanReturnFromRuin(ruined: false, hasActiveHostiles: false),
                "non-ruined gate allows transfer without hostiles");
        }

        private static RuinSeekOption MakeTarget(float threatFactor)
        {
            return new RuinSeekOption
            {
                quest = new QuestScriptDef { defName = "T_RuinQuest" },
                sitePart = new SitePartDef { defName = "T_RuinSite" },
                threatPointsFactor = threatFactor
            };
        }

        private static RitualDef BuildSeekRuinDef(string defName, RuinSeekOption target)
        {
            return new RitualDef
            {
                defName = defName,
                tier = 2,
                family = RitualFamily.Domain,
                ringSize = 5,
                durationTicks = 60,
                effectWorkerClass = typeof(RitualEffect_SeekRuin),
                researchPrerequisite = new ResearchProjectDef { defName = "T_RuinResearch" },
                ruinTarget = target
            };
        }
    }
}
