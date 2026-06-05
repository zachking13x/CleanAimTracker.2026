using CleanAimTracker.Models;
using System;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// XP + level progression system for the Aim Trainer.
    /// Level thresholds: 500 → 1200 → 2500 → 4500, then ×1.6 per level.
    /// </summary>
    public static class XPService
    {
        private static readonly int[] BaseLevelThresholds = { 0, 500, 1_200, 2_500, 4_500 };

        /// <summary>Total cumulative XP required to reach <paramref name="level"/>.</summary>
        public static int ThresholdForLevel(int level)
        {
            if (level <= 1) return 0;
            if (level - 1 < BaseLevelThresholds.Length)
                return BaseLevelThresholds[level - 1];

            // Beyond level 5: multiply last threshold by 1.6^(level-5)
            double threshold = BaseLevelThresholds[^1];
            for (int i = 5; i < level; i++)
                threshold *= 1.6;
            return (int)threshold;
        }

        /// <summary>
        /// Calculate XP earned for a single session.
        /// Base = 50 XP per 30 s. Multiplied by accuracy tier × difficulty tier.
        /// </summary>
        public static int CalculateSessionXP(AimTrainerResult result)
        {
            int baseXP = Math.Max(50, result.DurationSeconds / 30 * 50);

            double accuracyMult = result.Accuracy >= 90 ? 1.50
                                : result.Accuracy >= 75 ? 1.25
                                : result.Accuracy >= 60 ? 1.00
                                : 0.75;

            double diffMult = result.Difficulty switch
            {
                "Nightmare" => 2.5,
                "Hard"      => 1.75,
                "Medium"    => 1.25,
                _           => 1.00,
            };

            return Math.Max(10, (int)(baseXP * accuracyMult * diffMult));
        }

        /// <summary>
        /// Awards XP to the user's persistent settings.
        /// Returns <c>(xpEarned, oldLevel, newLevel)</c>.
        /// </summary>
        public static (int xpEarned, int oldLevel, int newLevel) AwardXP(AimTrainerResult result)
        {
            // Read the current XP / level baseline.
            var baseline = SettingsService.Load();
            int earned   = CalculateSessionXP(result);
            int oldLevel = baseline.CurrentLevel;

            // Compute new totals without touching the baseline object.
            int newTotalXP = baseline.TotalXP + earned;
            int newLevel   = baseline.CurrentLevel;

            // Level-up loop (cap at 99 to prevent infinite while)
            while (newLevel < 99 && newTotalXP >= ThresholdForLevel(newLevel + 1))
                newLevel++;

            // Load fresh immediately before saving so any field written by a concurrent
            // save (e.g. Close_Click writing LastTomorrowPromptDate) is preserved —
            // we only stamp in the XP / level delta, nothing else.
            var settings = SettingsService.Load();
            settings.TotalXP      = newTotalXP;
            settings.CurrentLevel = newLevel;
            SettingsService.Save(settings);
            return (earned, oldLevel, newLevel);
        }

        /// <summary>
        /// Progress within the current level as (xpIntoLevel, levelRange).
        /// Used to fill an XP progress bar: fill = current / rangeSize.
        /// </summary>
        public static (int current, int rangeSize) LevelProgress(int totalXP, int currentLevel)
        {
            int levelStart = ThresholdForLevel(currentLevel);
            int levelEnd   = ThresholdForLevel(currentLevel + 1);
            int current    = totalXP - levelStart;
            int range      = Math.Max(1, levelEnd - levelStart);
            return (current, range);
        }
    }
}
