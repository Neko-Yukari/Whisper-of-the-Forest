using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Experimental.Race
{
    /// <summary>
    /// The forest bloodline: the race's mana bar and its own caster ladder. Level and experience
    /// are saved on the gene, so no extra hediff or game component carries them.
    /// </summary>
    public class Gene_ForestBloodline : Gene_Resource
    {
        private const string LogModule = "Race";

        // Equipment and books change the mana cap through its StatDef, so the cached max is
        // re-read a few times per second instead of only on level change.
        private const int MaxSyncIntervalTicks = 250;

        public int level = 1;
        public float experience;

        private WotF_ForestBloodlineGeneDef Def => (WotF_ForestBloodlineGeneDef)def;

        public int MaxLevel => Def.levelTable.Count;

        public int Level => Mathf.Clamp(level, 1, MaxLevel);

        public WotF_CasterLevelDef LevelData => Def.levelTable[Level - 1];

        public float Experience => experience;

        public float ExperienceToNextLevel => Level >= MaxLevel ? 0f : LevelData.xpToNextLevel;

        public float CurrentMana => Value;

        public float MaxMana => Max;

        public override float InitialResourceMax => pawn.GetStatValue(Def.manaMaxStat);

        public override float MinLevelForAlert => 0f;

        protected override Color BarColor => Def.manaBarColor;

        protected override Color BarHighlightColor => Def.manaBarHighlightColor;

        /// <summary>The bloodline gene of a pawn, or null when the pawn has none.</summary>
        public static Gene_ForestBloodline Get(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive || pawn?.genes == null)
            {
                return null;
            }

            return pawn.genes.GetFirstGeneOfType<Gene_ForestBloodline>();
        }

        public override void PostAdd()
        {
            base.PostAdd();
            ApplyLegacyPsylinkLevel();
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (pawn.IsHashIntervalTick(MaxSyncIntervalTicks, delta))
            {
                RefreshMaxFromStats();
            }
        }

        /// <summary>Re-reads the mana cap stat, so anything that modifies it takes effect.</summary>
        public void RefreshMaxFromStats()
        {
            float statMax = pawn.GetStatValue(Def.manaMaxStat);
            if (!Mathf.Approximately(statMax, Max))
            {
                SetMax(statMax);
            }
        }

        public bool HasMana(float amount)
        {
            return Value >= amount;
        }

        /// <summary>Takes the mana cost and returns false without spending when it is not covered.</summary>
        public bool TrySpendMana(float amount)
        {
            if (amount < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Mana cost must not be negative.");
            }

            if (Value < amount)
            {
                return false;
            }

            Value -= amount;
            return true;
        }

        public void OffsetMana(float delta)
        {
            Value += delta;
        }

        public void GainExperience(float amount)
        {
            if (amount < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Experience gain must not be negative.");
            }

            experience += amount;
            while (Level < MaxLevel && experience >= ExperienceToNextLevel)
            {
                experience -= ExperienceToNextLevel;
                SetLevel(Level + 1);
            }

            if (Level >= MaxLevel)
            {
                experience = 0f;
            }
        }

        public void LoseExperience(float amount)
        {
            if (amount < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Experience loss must not be negative.");
            }

            experience -= amount;
            while (Level > 1 && experience < 0f)
            {
                SetLevel(Level - 1);
                experience += LevelData.xpToNextLevel;
            }

            experience = Mathf.Max(experience, 0f);
        }

        /// <summary>
        /// Legacy compatibility: a vanilla psylink never imports its own level into this ladder and
        /// never pushes a bloodline pawn past the first level. The hediff itself stays untouched;
        /// removing it would delete the vanilla psycasts the pawn already learned.
        /// </summary>
        public void ApplyLegacyPsylinkLevel()
        {
            if (level >= 1 || !pawn.health.hediffSet.HasHediff(HediffDefOf.PsychicAmplifier))
            {
                return;
            }

            SetLevel(1);
            WotfLog.Info(LogModule, pawn.LabelShortCap + " converted a psylink into forest bloodline level 1");
        }

        public override int PostProcessValue(float value)
        {
            return Mathf.RoundToInt(value);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref level, "wotfBloodlineLevel", 1);
            Scribe_Values.Look(ref experience, "wotfBloodlineExperience", 0f);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // The mana cap itself is scribed by Gene_Resource; the periodic stat sync catches
                // up with any table or equipment change after the save was written.
                level = Mathf.Clamp(level, 1, MaxLevel);
                experience = Mathf.Max(experience, 0f);
            }
        }

        private void SetLevel(int newLevel)
        {
            level = Mathf.Clamp(newLevel, 1, MaxLevel);
            RefreshMaxFromStats();
            WotfLog.Info(LogModule, pawn.LabelShortCap + " forest bloodline level " + level + " (xp " + experience.ToString("0.#") + ")");
        }
    }
}
