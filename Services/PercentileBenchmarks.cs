using CleanAimTracker.Models;
using System.Collections.Generic;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// TASK-3.3: Voltaic-derived benchmark thresholds, replacing the disabled
    /// TASK-0.2 population-percentile feature.
    ///
    /// HARD WORDING RULE: output is benchmark-relative ONLY — "clears the
    /// Voltaic advanced threshold for this scenario type". Never "top 10%",
    /// "top tier", or "most players": no population data exists, and Voltaic
    /// thresholds describe a standard, not a distribution of players.
    ///
    /// Emitted exclusively as a composer candidate (Section=Tip, Severity=1) so
    /// a benchmark line can never displace a diagnostic area or tip.
    /// </summary>
    public static class PercentileBenchmarks
    {
        public const string FactKey = "benchmark_standing";

        // Tier labels indexed by threshold position [0..3].
        private static readonly string[] TierLabels =
            { "advanced", "high-intermediate", "intermediate", "entry" };

        // ── Threshold tables ─────────────────────────────────────────────────
        // [advanced, high-intermediate, intermediate, entry]
        // Reaction (ms): LOWER is better — value must be AT OR BELOW the threshold.
        private static readonly Dictionary<string, Dictionary<string, int[]>> ReactionThresholds = new()
        {
            ["Flicking"] = new()
            {
                ["Easy"]      = new[] { 350, 500, 700, 900 },
                ["Medium"]    = new[] { 280, 400, 580, 780 },
                ["Hard"]      = new[] { 220, 320, 460, 640 },
                ["Nightmare"] = new[] { 170, 250, 370, 520 },
            },
            ["Switching"] = new()
            {
                ["Easy"]      = new[] { 380, 520, 700, 900 },
                ["Medium"]    = new[] { 290, 410, 580, 780 },
                ["Hard"]      = new[] { 220, 320, 460, 640 },
                ["Nightmare"] = new[] { 170, 250, 370, 530 },
            },
            ["Shotgun"] = new()
            {
                ["Easy"]      = new[] { 250, 360, 500, 680 },
                ["Medium"]    = new[] { 190, 280, 400, 560 },
                ["Hard"]      = new[] { 150, 220, 320, 450 },
                ["Nightmare"] = new[] { 120, 175, 260, 380 },
            },
            ["SmgAr"] = new()
            {
                ["Easy"]      = new[] { 400, 560, 750, 950 },
                ["Medium"]    = new[] { 300, 430, 600, 800 },
                ["Hard"]      = new[] { 220, 330, 480, 660 },
                ["Nightmare"] = new[] { 160, 250, 380, 540 },
            },
        };

        // Accuracy (%): HIGHER is better — value must be AT OR ABOVE the threshold.
        private static readonly Dictionary<string, Dictionary<string, int[]>> AccuracyThresholds = new()
        {
            ["Flicking"] = new()
            {
                ["Easy"]      = new[] { 95, 88, 78, 65 },
                ["Medium"]    = new[] { 92, 84, 73, 60 },
                ["Hard"]      = new[] { 88, 79, 68, 54 },
                ["Nightmare"] = new[] { 82, 72, 60, 47 },
            },
            ["Tracking"] = new()
            {
                ["Easy"]      = new[] { 90, 80, 68, 54 },
                ["Medium"]    = new[] { 86, 75, 63, 49 },
                ["Hard"]      = new[] { 81, 70, 57, 44 },
                ["Nightmare"] = new[] { 74, 63, 50, 37 },
            },
            ["Precision"] = new()
            {
                ["Easy"]      = new[] { 92, 83, 71, 57 },
                ["Medium"]    = new[] { 88, 78, 66, 52 },
                ["Hard"]      = new[] { 83, 72, 60, 46 },
                ["Nightmare"] = new[] { 76, 65, 53, 39 },
            },
            ["Adaptive"] = new()
            {
                ["Easy"]      = new[] { 88, 78, 66, 52 },
                ["Medium"]    = new[] { 83, 72, 60, 46 },
                ["Hard"]      = new[] { 77, 66, 53, 40 },
                ["Nightmare"] = new[] { 70, 59, 46, 33 },
            },
            ["Switching"] = new()
            {
                ["Easy"]      = new[] { 88, 78, 66, 52 },
                ["Medium"]    = new[] { 83, 72, 60, 46 },
                ["Hard"]      = new[] { 77, 66, 53, 40 },
                ["Nightmare"] = new[] { 70, 59, 46, 33 },
            },
        };

        /// <summary>Tier index 0–3 the value clears, or -1 when below all/no benchmark.</summary>
        public static int AccuracyTier(string scenario, string difficulty, double accuracy)
        {
            if (!AccuracyThresholds.TryGetValue(scenario, out var byDiff)) return -1;
            if (!byDiff.TryGetValue(difficulty, out var t)) return -1;
            for (int i = 0; i < t.Length; i++)
                if (accuracy >= t[i]) return i;
            return -1;
        }

        /// <summary>Tier index 0–3 the value clears (lower ms = better), or -1. Sniper: always -1.</summary>
        public static int ReactionTier(string scenario, string difficulty, double reactionMs)
        {
            if (scenario == "Sniper") return -1;   // reaction is not the Sniper metric
            if (reactionMs <= 0) return -1;
            if (!ReactionThresholds.TryGetValue(scenario, out var byDiff)) return -1;
            if (!byDiff.TryGetValue(difficulty, out var t)) return -1;
            for (int i = 0; i < t.Length; i++)
                if (reactionMs <= t[i]) return i;
            return -1;
        }

        /// <summary>
        /// The single benchmark observation for a drill, or null when the session
        /// clears no benchmark worth mentioning (advanced/high-intermediate only —
        /// "you cleared the entry threshold" is noise, not coaching).
        /// </summary>
        public static CoachObservation? BenchmarkObservation(AimTrainerResult r)
        {
            int accTier   = AccuracyTier(r.Scenario, r.Difficulty, r.Accuracy);
            int reactTier = ReactionTier(r.Scenario, r.Difficulty, r.AvgReactionMs);

            // Prefer the stronger standing; mention only meaningful tiers (0 or 1).
            bool accWins = accTier >= 0 && (reactTier < 0 || accTier <= reactTier);

            if (accWins && accTier is 0 or 1)
            {
                return new CoachObservation
                {
                    FactKey      = FactKey,
                    SourceEngine = nameof(PercentileBenchmarks),
                    Section      = CoachSection.Tip,
                    Polarity     = ObservationPolarity.Strength,
                    Severity     = 1,   // benchmark context never displaces a diagnostic
                    Message      = $"Your {r.Accuracy:F0}% accuracy clears the Voltaic {TierLabels[accTier]} " +
                                   $"threshold for {r.Scenario}-style scenarios at {r.Difficulty} difficulty.",
                    RequiredMetrics = new List<string>()
                };
            }

            if (!accWins && reactTier is 0 or 1)
            {
                // TASK-0.3: the noun follows the measurement — "reaction" only for
                // stimulus-anchored scenarios, otherwise "time per target".
                return new CoachObservation
                {
                    FactKey      = FactKey,
                    SourceEngine = nameof(PercentileBenchmarks),
                    Section      = CoachSection.Tip,
                    Polarity     = ObservationPolarity.Strength,
                    Severity     = 1,
                    Message      = $"Your {r.AvgReactionMs:F0}ms average {ReactionMetric.Noun(r.Scenario)} clears the Voltaic {TierLabels[reactTier]} " +
                                   $"threshold for {r.Scenario}-style scenarios at {r.Difficulty} difficulty.",
                    RequiredMetrics = new List<string> { "AvgReactionMs" }
                };
            }

            return null;
        }
    }
}
