using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Race
{
    /// <summary>
    /// The forest bloodline: the race's mana bar and its own caster ladder. Level and experience
    /// are saved on the gene, so no extra hediff or game component carries them. The vanilla psylink
    /// is only a mirror of the level: the gene writes it, vanilla sources can only raise the level
    /// through <see cref="Notify_VanillaPsylinkChanged"/>.
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

        // Set while the gene writes its own mirror, so the psylink patches do not read that write
        // back as a vanilla psylink gain.
        private static bool writingMirror;

        public int MaxLevel => Def.maxLevel;

        public int Level => Mathf.Clamp(level, 1, MaxLevel);

        public float Experience => experience;

        public float ExperienceToNextLevel => Level >= MaxLevel ? 0f : Def.xpToNextLevelCurve.Evaluate(Level);

        public float LevelManaMax => Def.manaMaxCurve.Evaluate(Level);

        public float LevelEntropyMaxBonus => Def.entropyMaxCurve.Evaluate(Level);

        public float LevelEntropyRecoveryFactor => Def.entropyRecoveryFactorCurve.Evaluate(Level);

        /// <summary>Vanilla psylink level this bloodline level mirrors.</summary>
        public int MirrorPsylinkLevel
        {
            get
            {
                int mirror = 0;
                List<int> unlocks = Def.psylinkUnlockLevels;
                while (mirror < unlocks.Count && Level >= unlocks[mirror])
                {
                    mirror++;
                }

                return Mathf.Min(mirror, (int)HediffDefOf.PsychicAmplifier.maxSeverity);
            }
        }

        /// <summary>Lowest bloodline level whose mirror reaches <paramref name="psylinkLevel"/>.</summary>
        public int LevelForPsylink(int psylinkLevel)
        {
            List<int> unlocks = Def.psylinkUnlockLevels;
            return psylinkLevel <= 0 ? 1 : unlocks[Mathf.Min(psylinkLevel, unlocks.Count) - 1];
        }

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
            // A pawn that already has a psylink starts at the lowest level that mirrors it.
            SetLevel(Mathf.Max(Level, LevelForPsylink(pawn.GetPsylinkLevel())));
        }

        /// <summary>
        /// A vanilla source (neuroformer, anima tree, title) added or raised the psylink: the
        /// bloodline jumps to the lowest level that mirrors the new link, never down.
        /// </summary>
        internal void Notify_VanillaPsylinkChanged()
        {
            if (writingMirror)
            {
                return;
            }

            SetLevel(Mathf.Max(Level, LevelForPsylink(pawn.GetPsylinkLevel())));
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            Value += pawn.GetStatValue(Def.manaRegenStat) * delta / GenTicks.TicksPerRealSecond;
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
                experience += ExperienceToNextLevel;
            }

            experience = Mathf.Max(experience, 0f);
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
            int previous = level;
            level = Mathf.Clamp(newLevel, 1, MaxLevel);
            RefreshMaxFromStats();
            SyncPsylinkMirror();
            if (level != previous)
            {
                WotfLog.Info(LogModule, pawn.LabelShortCap + " forest bloodline level " + level + " (xp " + experience.ToString("0.#") + ")");
            }
        }

        /// <summary>
        /// Writes the mirror level straight into the psylink. Going through <c>ChangeLevel</c> would
        /// hand out psycasts and re-enter the psylink patches.
        /// </summary>
        private void SyncPsylinkMirror()
        {
            int target = MirrorPsylinkLevel;
            Hediff_Psylink psylink = pawn.GetMainPsylinkSource();
            if (psylink == null && target <= 0)
            {
                return;
            }

            writingMirror = true;
            try
            {
                if (psylink == null)
                {
                    psylink = (Hediff_Psylink)HediffMaker.MakeHediff(HediffDefOf.PsychicAmplifier, pawn);
                    psylink.suppressPostAddLetter = true;
                    psylink.level = target;
                    pawn.health.AddHediff(psylink, pawn.health.hediffSet.GetBrain());
                }
                else
                {
                    psylink.level = target;
                }

                // Vanilla only copies level into severity on tick; the stage must follow right away.
                psylink.Severity = target;
            }
            finally
            {
                writingMirror = false;
            }
        }
    }
}
