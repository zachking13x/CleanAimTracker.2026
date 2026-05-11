using CleanAimTracker.Models;
using System;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// TASK-16: Maintains the daily session streak and surfaces retention messages.
    /// Call UpdateStreak() immediately after a session is saved.
    /// </summary>
    public static class StreakService
    {
        public record StreakResult(
            int CurrentStreak,
            int BestStreak,
            bool JustBrokeStreak,      // true if streak reset from ≥2 to 1
            bool JustHitNewBest,       // true if we beat previous best
            string Message             // human-readable retention message
        );

        /// <summary>
        /// Reads and updates streak state from UserSettings.
        /// Should be called immediately after each session is saved to storage.
        /// </summary>
        public static StreakResult UpdateStreak()
        {
            var settings = SettingsService.Load();
            var today    = DateTime.Today;

            int  prevStreak      = settings.CurrentStreak;
            int  prevBest        = settings.BestStreakDays;
            bool justBroke       = false;
            bool justHitNewBest  = false;

            // Already logged a session today — no change to streak
            if (settings.LastSessionDate.Date == today)
            {
                string sameMsg = prevStreak >= 7
                    ? $"🔥 {prevStreak}-day streak — you're on fire!"
                    : $"Session logged. {prevStreak}-day streak.";
                return new StreakResult(prevStreak, prevBest, false, false, sameMsg);
            }

            // Streak extends (yesterday was last session)
            if (settings.LastSessionDate.Date == today.AddDays(-1))
            {
                settings.CurrentStreak++;
            }
            // Streak breaks (gap > 1 day) — but only if user had a streak worth noting
            else
            {
                justBroke = settings.CurrentStreak >= 2;
                settings.CurrentStreak = 1;
            }

            // New best?
            if (settings.CurrentStreak > settings.BestStreakDays)
            {
                settings.BestStreakDays = settings.CurrentStreak;
                justHitNewBest = true;
            }

            settings.LastSessionDate = today;
            SettingsService.Save(settings);

            string message = BuildMessage(settings.CurrentStreak, settings.BestStreakDays,
                                          justBroke, justHitNewBest, prevStreak);

            return new StreakResult(settings.CurrentStreak, settings.BestStreakDays,
                                    justBroke, justHitNewBest, message);
        }

        private static string BuildMessage(int current, int best,
                                            bool justBroke, bool justHitNewBest,
                                            int prevStreak)
        {
            if (justBroke)
                return $"Streak reset — you had a {prevStreak}-day run. Start fresh today! 💪";

            if (justHitNewBest && current >= 7)
                return $"🏆 New best streak: {current} days! You're building a real habit.";

            if (justHitNewBest && current >= 3)
                return $"🎉 New personal best: {current}-day streak!";

            return current switch
            {
                >= 30 => $"💎 {current}-day streak — Elite consistency!",
                >= 14 => $"🔥 {current}-day streak — incredible discipline!",
                >= 7  => $"🔥 {current}-day streak — keep it going!",
                >= 3  => $"✅ {current}-day streak — nice consistency!",
                _     => $"Day {current} — build the habit one session at a time."
            };
        }

        /// <summary>Returns streak info without modifying state (read-only).</summary>
        public static (int current, int best) GetStreakInfo()
        {
            var s = SettingsService.Load();
            return (s.CurrentStreak, s.BestStreakDays);
        }
    }
}
