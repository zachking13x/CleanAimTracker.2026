using System.Collections.Generic;
using CleanAimTracker.Models;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Manages per-scenario difficulty unlocks based on accumulated accuracy across sessions.
    /// Thresholds are pillar-specific so tracking scenarios have a gentler curve than clicking.
    /// </summary>
    public static class ScenarioDifficultyService
    {
        // ------------------------------------------------------------------ //
        //  Update after a completed session
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Records the session accuracy and unlocks the next difficulty tier when
        /// the rolling average and minimum session count are both met.
        /// </summary>
        public static void UpdateAfterSession(AimTrainerResult result, UserSettings settings)
        {
            if (result == null || settings == null) return;

            var state = settings.GetScenarioState(result.Scenario, result.SubVariant);
            double[] thresholds = GetThresholds(result.Pillar);

            // Running average weighted by session count
            state.SessionsAtCurrent++;
            state.AvgAccuracyAtCurrent =
                (state.AvgAccuracyAtCurrent * (state.SessionsAtCurrent - 1) + result.Accuracy)
                / state.SessionsAtCurrent;

            // Medium: avg >= threshold[0] AND at least 2 sessions
            if (!state.MediumUnlocked
                && state.AvgAccuracyAtCurrent >= thresholds[0]
                && state.SessionsAtCurrent >= 2)
            {
                state.MediumUnlocked = true;
            }

            // Hard: avg >= threshold[1] AND at least 4 sessions AND Medium already unlocked
            if (!state.HardUnlocked
                && state.MediumUnlocked
                && state.AvgAccuracyAtCurrent >= thresholds[1]
                && state.SessionsAtCurrent >= 4)
            {
                state.HardUnlocked = true;
            }

            // Nightmare: avg >= threshold[2] AND at least 6 sessions AND Hard already unlocked
            if (!state.NightmareUnlocked
                && state.HardUnlocked
                && state.AvgAccuracyAtCurrent >= thresholds[2]
                && state.SessionsAtCurrent >= 6)
            {
                state.NightmareUnlocked = true;
            }

            SettingsService.Save(settings);
        }

        // ------------------------------------------------------------------ //
        //  Query helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns the ordered list of difficulty labels the player has unlocked
        /// for the given scenario + variant combination.
        /// </summary>
        public static string[] GetAvailableDifficulties(
            string scenario, string variant, UserSettings settings)
        {
            if (settings == null) return new[] { "Easy" };

            var state = settings.GetScenarioState(scenario, variant);
            var list  = new List<string> { "Easy" };

            if (state.MediumUnlocked)    list.Add("Medium");
            if (state.HardUnlocked)      list.Add("Hard");
            if (state.NightmareUnlocked) list.Add("Nightmare");

            return list.ToArray();
        }

        /// <summary>
        /// Returns a human-readable description of what the player must do to unlock
        /// <paramref name="difficulty"/> for the given scenario + variant.
        /// Returns "Unlocked" if the tier is already available.
        /// </summary>
        public static string GetUnlockRequirement(
            string scenario, string variant, string difficulty, UserSettings settings)
        {
            if (settings == null) return "";

            var    state      = settings.GetScenarioState(scenario, variant);
            string pillar     = ResolvePillar(scenario);
            double[] thresh   = GetThresholds(pillar);

            return difficulty switch
            {
                "Medium"    => state.MediumUnlocked
                                  ? "Unlocked"
                                  : $"Average {thresh[0]:0}%+ accuracy over 2+ sessions",
                "Hard"      => state.HardUnlocked
                                  ? "Unlocked"
                                  : $"Average {thresh[1]:0}%+ accuracy over 4+ sessions (Medium required)",
                "Nightmare" => state.NightmareUnlocked
                                  ? "Unlocked"
                                  : $"Average {thresh[2]:0}%+ accuracy over 6+ sessions (Hard required)",
                _           => "Unlocked"
            };
        }

        // ------------------------------------------------------------------ //
        //  Internal helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns [mediumThreshold, hardThreshold, nightmareThreshold] for the pillar.
        /// Tracking is slightly more lenient because sustained accuracy is harder to hold.
        /// </summary>
        private static double[] GetThresholds(string pillar)
        {
            return pillar switch
            {
                "Tracking"  => new[] { 55.0, 65.0, 75.0 },
                "Switching" => new[] { 50.0, 60.0, 75.0 },
                _           => new[] { 60.0, 70.0, 80.0 }   // Clicking (default)
            };
        }

        /// <summary>
        /// Maps a scenario name to its training pillar so thresholds can be
        /// looked up without requiring a full AimTrainerResult.
        /// </summary>
        private static string ResolvePillar(string scenario)
        {
            return scenario switch
            {
                "Tracking"
                or "AirTracking"            => "Tracking",

                "Switching"
                or "SpeedSwitching"
                or "Evasive"
                or "PeekTraining"           => "Switching",

                _                           => "Clicking"
            };
        }
    }
}
