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

        // ── CAT_BENCHMARK_DESTINATION (2026-08-03) ────────────────────────────
        // The coach was excellent at "here's what's wrong" and silent on "…compared
        // to what?". BenchmarkObservation only ever fired for tiers 0-1, as a
        // Severity-1 tip, inside the Pro-gated report — so a beginner (the player who
        // most needs a target) never saw a benchmark at all, and nobody ever saw what
        // came NEXT. Standing() answers both: where you sit on the ladder, and the
        // exact gap to the next rung. Rendered free, on every result.
        //
        // Same hard wording rule as above: these are Voltaic-derived STANDARDS, never
        // population percentiles. "Clears the intermediate threshold" — never "top 20%".

        /// <summary>Where a value sits on the ladder, and what it takes to climb one rung.</summary>
        public sealed record BenchmarkStanding(
            string  MetricLabel,      // "Accuracy" / "Pace"
            int     TierIndex,        // 0 = advanced … 3 = entry, -1 = below entry
            string  TierLabel,        // "advanced" … "entry" / "unranked"
            double  Value,
            string? NextTierLabel,    // null when already advanced
            double? NextThreshold,
            double? Gap,              // absolute distance to the next threshold
            bool    LowerIsBetter,
            double? TierThreshold);   // the threshold this value cleared; null when below entry

        // ── CAT_BENCHMARK_COVERAGE (2026-08-12) ───────────────────────────────
        // The accuracy tables cover five scenario families. CAT ships nineteen drills,
        // so the "here's your destination" card rendered blank on most of what people
        // actually play — including StaticClicking, the single most-played scenario.
        //
        // Two honest options existed, and this uses both:
        //   1. MAP scenarios whose accuracy is measured on the SAME scale as an existing
        //      family. StaticClicking is judged against Precision, DynamicClicking
        //      against Flicking, and so on. Each mapping below is a claim that the two
        //      drills score comparably, not a convenience.
        //   2. Report NOTHING for the rest. Shotgun (pellet spread), Sniper (placement,
        //      not speed) and the bot drills (a body takes two rounds, a head takes one)
        //      measure accuracy differently enough that borrowing a table would be
        //      inventing a standard. Those fall back to a personal-best destination in
        //      the UI, clearly labelled as personal — see AimTrainerResultWindow.
        private static readonly Dictionary<string, string> AccuracyFamily = new()
        {
            ["StaticClicking"]  = "Precision",   // stationary targets, precision-dominant
            ["PeekTraining"]    = "Precision",   // hold-and-place on an exposed window
            ["DynamicClicking"] = "Flicking",    // moving targets you click discretely
            ["SpeedSwitching"]  = "Switching",   // same task, tighter time pressure
            ["AirTracking"]     = "Tracking",    // continuous tracking, airborne path
            ["Evasive"]         = "Tracking",    // continuous tracking, erratic path
        };

        /// <summary>The threshold family a scenario is judged against, or null if none applies.</summary>
        public static string? FamilyFor(string scenario)
        {
            if (AccuracyThresholds.ContainsKey(scenario)) return scenario;
            return AccuracyFamily.TryGetValue(scenario, out var fam) ? fam : null;
        }

        /// <summary>Accuracy standing for a drill, or null when no honest table applies.</summary>
        public static BenchmarkStanding? AccuracyStanding(string scenario, string difficulty, double accuracy)
        {
            string? family = FamilyFor(scenario);
            if (family == null) return null;
            if (!AccuracyThresholds.TryGetValue(family, out var byDiff)) return null;
            if (!byDiff.TryGetValue(difficulty, out var t)) return null;
            return Build("Accuracy", AccuracyTier(family, difficulty, accuracy), accuracy, t, lowerIsBetter: false);
        }

        /// <summary>Pace standing for a drill, or null when it has no table / no valid reading.</summary>
        public static BenchmarkStanding? PaceStanding(string scenario, string difficulty, double reactionMs)
        {
            if (scenario == "Sniper" || reactionMs <= 0) return null;
            if (!ReactionThresholds.TryGetValue(scenario, out var byDiff)) return null;
            if (!byDiff.TryGetValue(difficulty, out var t)) return null;
            string label = ReactionMetric.IsTrueReaction(scenario) ? "Reaction" : "Pace";
            return Build(label, ReactionTier(scenario, difficulty, reactionMs), reactionMs, t, lowerIsBetter: true);
        }

        private static BenchmarkStanding Build(string metric, int tier, double value, int[] t, bool lowerIsBetter)
        {
            // t is ordered hardest → easiest (index 0 = advanced). "Next" is therefore
            // one index LOWER; below-entry (-1) climbs toward the last entry, index 3.
            int nextIdx = tier < 0 ? t.Length - 1 : tier - 1;

            string  tierLabel     = tier >= 0 ? TierLabels[tier] : "unranked";
            double? tierThreshold = tier >= 0 ? t[tier] : null;

            if (nextIdx < 0)
                return new BenchmarkStanding(metric, tier, tierLabel, value,
                                             null, null, null, lowerIsBetter, tierThreshold);

            double nextThreshold = t[nextIdx];
            double gap = lowerIsBetter ? value - nextThreshold : nextThreshold - value;
            return new BenchmarkStanding(metric, tier, tierLabel, value,
                                         TierLabels[nextIdx], nextThreshold, System.Math.Max(0, gap),
                                         lowerIsBetter, tierThreshold);
        }

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
