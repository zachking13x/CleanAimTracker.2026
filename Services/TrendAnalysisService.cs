using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// TASK-3.4: the one trend object per report. Every surface that mentions a
    /// delta reads this — no surface computes its own.
    /// </summary>
    public record TrendReport(
        double? VsLastDelta,            // session quality − previous session quality
        double? VsRecentBaselineDelta,  // session quality − rolling-5 baseline
        double? BaselineValue,          // the rolling-5 baseline itself (displayable)
        double? LastValue,              // previous session quality (displayable)
        string Direction,               // "improving" | "declining" | "stable" | "insufficient"
        int PriorSessionCount);

    public static class TrendAnalysisService
    {
        /// <summary>Direction threshold in quality points between newer/older halves.</summary>
        public const double DirectionThreshold = 5.0;

        /// <summary>Minimum prior sessions before a baseline delta is claimed.</summary>
        public const int MinSessionsForBaseline = 3;

        public static TrendReport Compute(SessionSummary session, List<SessionSummary>? history)
        {
            // TASK-1.3: low-activity sessions never enter baselines or trends.
            var prior = (history ?? new List<SessionSummary>())
                .Where(h => h.Timestamp < session.Timestamp && !h.IsLowActivitySession)
                .OrderByDescending(h => h.Timestamp)
                .ToList();

            double? lastValue   = prior.Count >= 1 ? prior[0].OverallQualityScore : null;
            double? vsLastDelta = lastValue.HasValue
                ? session.OverallQualityScore - lastValue.Value
                : null;

            double? baseline = null;
            double? vsBaselineDelta = null;
            if (prior.Count >= MinSessionsForBaseline)
            {
                // Hard rule: the displayed delta must be exact arithmetic over the
                // displayed baseline — both come from this one computation.
                baseline = prior.Take(5).Average(h => h.OverallQualityScore);
                vsBaselineDelta = session.OverallQualityScore - baseline.Value;
            }

            string direction = "insufficient";
            if (prior.Count >= MinSessionsForBaseline)
            {
                var window = prior.Take(5).ToList();
                int splitIndex = Math.Max(1, window.Count / 2);
                double newerAvg = window.Take(splitIndex).Average(h => h.OverallQualityScore);
                double olderAvg = window.Skip(splitIndex).Average(h => h.OverallQualityScore);

                direction =
                    newerAvg > olderAvg + DirectionThreshold ? "improving" :
                    newerAvg < olderAvg - DirectionThreshold ? "declining" :
                    "stable";
            }

            return new TrendReport(
                VsLastDelta:           vsLastDelta,
                VsRecentBaselineDelta: vsBaselineDelta,
                BaselineValue:         baseline,
                LastValue:             lastValue,
                Direction:             direction,
                PriorSessionCount:     prior.Count);
        }

        /// <summary>
        /// Whole-history summary (no "current session" anchor) for surfaces like
        /// the recommendation window. Same exclusion and direction rules.
        /// </summary>
        public static (double AvgQuality, string Direction, int Count) Summarize(List<SessionSummary>? history)
        {
            var valid = (history ?? new List<SessionSummary>())
                .Where(h => !h.IsLowActivitySession)
                .OrderByDescending(h => h.Timestamp)
                .Take(5)
                .ToList();

            if (valid.Count < MinSessionsForBaseline)
                return (0, "insufficient", valid.Count);

            double avgQuality = valid.Average(s => s.OverallQualityScore);

            int splitIndex = Math.Max(1, valid.Count / 2);
            double newerAvg = valid.Take(splitIndex).Average(h => h.OverallQualityScore);
            double olderAvg = valid.Skip(splitIndex).Average(h => h.OverallQualityScore);

            string direction =
                newerAvg > olderAvg + DirectionThreshold ? "improving" :
                newerAvg < olderAvg - DirectionThreshold ? "declining" :
                "stable";

            return (avgQuality, direction, valid.Count);
        }
    }
}
