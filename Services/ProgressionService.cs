using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// TASK-15: Bronze → Silver → Gold → Elite tier system.
    /// Tier is based on the user's lifetime average quality score across all sessions.
    /// </summary>
    public static class ProgressionService
    {
        public record TierInfo(string Name, string Emoji, string Color, string NextGoal);

        public static TierInfo GetTier(IEnumerable<SessionSummary> sessions)
        {
            var list = sessions?.ToList();
            if (list == null || list.Count == 0)
                return new TierInfo("Rookie", "🎮", "#888888", "Complete your first session");

            double avg = list.Average(s => s.OverallQualityScore);
            return GetTierForAvg(avg, list.Count);
        }

        public static TierInfo GetTierForAvg(double avgQuality, int totalSessions)
        {
            return avgQuality switch
            {
                >= 82 => new TierInfo("Elite",  "💎", "#00D4FF",
                                       "You've mastered consistency — maintain it"),
                >= 70 => new TierInfo("Gold",   "🥇", "#FFD700",
                                       $"Avg {82 - avgQuality:F0} pts to Elite"),
                >= 55 => new TierInfo("Silver", "🥈", "#C0C0C0",
                                       $"Avg {70 - avgQuality:F0} pts to Gold"),
                >= 40 => new TierInfo("Bronze", "🥉", "#CD7F32",
                                       $"Avg {55 - avgQuality:F0} pts to Silver"),
                _     => new TierInfo("Rookie", "🎮", "#888888",
                                       $"Avg {40 - avgQuality:F0} pts to Bronze")
            };
        }

        /// <summary>Loads all sessions and returns the current tier.</summary>
        public static TierInfo GetCurrentTier()
        {
            try
            {
                var sessions = SessionStorage.LoadAll();
                return GetTier(sessions);
            }
            catch
            {
                return new TierInfo("Rookie", "🎮", "#888888", "Complete your first session");
            }
        }
    }
}
