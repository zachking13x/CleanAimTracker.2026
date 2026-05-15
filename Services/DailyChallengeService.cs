using CleanAimTracker.Models;
using System;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Generates and evaluates a single daily challenge.
    /// The challenge is deterministic for the calendar day (date-seeded RNG),
    /// so every call to GetToday() returns the same challenge until midnight.
    /// </summary>
    public static class DailyChallengeService
    {
        // ── Possible challenge templates ─────────────────────────────────────
        private static readonly string[] Scenarios   = { "Tracking", "Flicking", "Precision", "Switching" };
        private static readonly string[] Difficulties = { "Easy", "Medium", "Hard" };

        private record Template(string GoalType, string Label, double[] Values);

        private static readonly Template[] Templates =
        {
            new("Accuracy",  "Hit {0}% accuracy",                   new[] { 75.0, 80, 85, 90 }),
            new("Accuracy",  "Finish with at least {0}% accuracy",  new[] { 70.0, 75, 80, 85 }),
            new("Reaction",  "Average reaction time under {0}ms",   new[] { 350.0, 300, 260, 220 }),
            new("MaxStreak", "Build a streak of {0} in one drill",  new[] { 10.0, 15, 20, 30 }),
        };

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Returns today's challenge (same object all day).</summary>
        public static DailyChallenge GetToday()
        {
            // Seed with today's date so the challenge resets at midnight
            int seed = DateTime.Today.Year * 10000 +
                       DateTime.Today.Month * 100  +
                       DateTime.Today.Day;
            var rng = new Random(seed);

            string scenario   = Scenarios[rng.Next(Scenarios.Length)];
            string difficulty = Difficulties[rng.Next(Difficulties.Length)];
            var    tpl        = Templates[rng.Next(Templates.Length)];
            double goalValue  = tpl.Values[rng.Next(tpl.Values.Length)];
            string desc       = string.Format(tpl.Label, goalValue);

            return new DailyChallenge
            {
                Scenario    = scenario,
                Difficulty  = difficulty,
                GoalType    = tpl.GoalType,
                GoalValue   = goalValue,
                Description = $"{scenario} · {difficulty} — {desc}",
                ShortDesc   = desc,
            };
        }

        /// <summary>True if the user already completed today's challenge.</summary>
        public static bool IsTodayComplete(UserSettings settings) =>
            settings.LastChallengeDate.Date == DateTime.Today;

        /// <summary>
        /// Checks whether <paramref name="result"/> satisfies <paramref name="challenge"/>.
        /// If so, increments ChallengesCompleted, stamps LastChallengeDate, saves settings,
        /// and returns true.  Does nothing and returns false if already complete or not met.
        /// </summary>
        public static bool TryComplete(DailyChallenge challenge, AimTrainerResult result, UserSettings settings)
        {
            if (IsTodayComplete(settings)) return false;

            bool met = challenge.GoalType switch
            {
                "Accuracy"  => result.Accuracy  >= challenge.GoalValue,
                "Reaction"  => result.AvgReactionMs > 0 && result.AvgReactionMs < challenge.GoalValue,
                "MaxStreak" => result.MaxStreak  >= (int)challenge.GoalValue,
                _           => false,
            };

            if (!met) return false;

            settings.LastChallengeDate    = DateTime.Today;
            settings.ChallengesCompleted += 1;
            SettingsService.Save(settings);
            return true;
        }
    }
}
