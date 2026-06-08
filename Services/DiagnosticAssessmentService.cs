using System;
using System.Collections.Generic;
using System.Linq;
using CleanAimTracker.Models;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Defines and evaluates the 8-dimension diagnostic assessment.
    /// Each dimension maps to one scenario + variant pair run for a fixed duration.
    /// After all 8 tests, <see cref="BuildProfile"/> synthesises the results into a
    /// <see cref="DiagnosticProfile"/> with ranked dimensions and a weakest/strongest label.
    /// </summary>
    public static class DiagnosticAssessmentService
    {
        // ------------------------------------------------------------------ //
        //  Test Definitions
        // ------------------------------------------------------------------ //

        public record AssessmentTest(
            string Dimension,
            string Scenario,
            string Variant,
            int    DurationSeconds,
            string Description);

        /// <summary>
        /// Ordered list of the 8 assessment tests.  Run them in order and pass
        /// each resulting <see cref="AimTrainerResult"/> to <see cref="BuildProfile"/>.
        /// </summary>
        public static readonly IReadOnlyList<AssessmentTest> Tests = new List<AssessmentTest>
        {
            new("CloseRangeStatic",    "StaticClicking",  "Standard",      30,
                "Quick clicks on close-range stationary targets"),

            new("LongRangeStatic",     "Precision",       "Standard",      30,
                "Deliberate shots on small distant targets"),

            new("HorizontalTracking",  "Tracking",        "Smooth",        30,
                "Sustained left-right tracking movement"),

            new("VerticalTracking",    "AirTracking",     "Falling",       30,
                "Sustained up-down tracking movement"),

            new("DiagonalTracking",    "AirTracking",     "Diagonal",      30,
                "Combined axis tracking on diagonal paths"),

            new("CloseSwitching",      "Switching",       "4-Target",      30,
                "Fast multi-target acquisition at close range"),

            new("FarSwitching",        "Switching",       "6-Target",      30,
                "Wide-angle multi-target flicks"),

            new("PeekReaction",        "PeekTraining",    "WideSwing",     30,
                "Reaction time and timing on peeking targets"),
        };

        // ------------------------------------------------------------------ //
        //  Score Calculation
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Converts a single test's <see cref="AimTrainerResult"/> into a 0–100 score
        /// for the given dimension.
        /// The score is a weighted blend of accuracy (70 %) and reaction time (30 %).
        /// Reaction time contribution is capped when no hits were recorded.
        /// </summary>
        public static double ScoreTest(AimTrainerResult result, AssessmentTest test)
        {
            if (result == null) return 0;

            double accuracyScore = Math.Clamp(result.Accuracy, 0, 100);

            // Reaction score: 100 = sub-200 ms, 0 = 1000 ms+
            double reactionScore = 0;
            if (result.Hits > 0 && result.AvgReactionMs > 0)
            {
                const double BestMs  = 200.0;
                const double WorstMs = 1000.0;
                reactionScore = Math.Clamp(
                    100.0 * (WorstMs - result.AvgReactionMs) / (WorstMs - BestMs),
                    0, 100);
            }

            return accuracyScore * 0.70 + reactionScore * 0.30;
        }

        // ------------------------------------------------------------------ //
        //  Profile Builder
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Builds a complete <see cref="DiagnosticProfile"/> from the 8 test results.
        /// Results must be ordered to match <see cref="Tests"/> (index 0–7).
        /// Fewer results are accepted — missing dimensions get a score of 0.
        /// </summary>
        public static DiagnosticProfile BuildProfile(
            IList<AimTrainerResult> results,
            int sessionNumber)
        {
            double score0 = GetScore(results, 0);
            double score1 = GetScore(results, 1);
            double score2 = GetScore(results, 2);
            double score3 = GetScore(results, 3);
            double score4 = GetScore(results, 4);
            double score5 = GetScore(results, 5);
            double score6 = GetScore(results, 6);
            double score7 = GetScore(results, 7);

            var dimensionScores = new Dictionary<string, double>
            {
                ["CloseRangeStatic"]    = score0,
                ["LongRangeStatic"]     = score1,
                ["HorizontalTracking"]  = score2,
                ["VerticalTracking"]    = score3,
                ["DiagonalTracking"]    = score4,
                ["CloseSwitching"]      = score5,
                ["FarSwitching"]        = score6,
                ["PeekReaction"]        = score7,
            };

            string weakest   = dimensionScores.OrderBy(kv => kv.Value).First().Key;
            string strongest = dimensionScores.OrderByDescending(kv => kv.Value).First().Key;

            return new DiagnosticProfile
            {
                CompletedAt          = DateTime.Now,
                CloseRangeStatic     = score0,
                LongRangeStatic      = score1,
                HorizontalTracking   = score2,
                VerticalTracking     = score3,
                DiagonalTracking     = score4,
                CloseSwitching       = score5,
                FarSwitching         = score6,
                PeekReaction         = score7,
                WeakestDimension     = weakest,
                StrongestDimension   = strongest,
                SessionNumber        = sessionNumber,
            };
        }

        // ------------------------------------------------------------------ //
        //  Recommendation
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns the scenario + variant most likely to address the player's
        /// weakest dimension from a completed diagnostic profile.
        /// </summary>
        public static (string Scenario, string Variant) GetRecommendedStartingScenario(
            DiagnosticProfile profile)
        {
            if (profile == null)
                return ("Precision", "Standard");

            return profile.WeakestDimension switch
            {
                "CloseRangeStatic"   => ("StaticClicking",  "Standard"),
                "LongRangeStatic"    => ("Precision",        "Standard"),
                "HorizontalTracking" => ("Tracking",         "Smooth"),
                "VerticalTracking"   => ("AirTracking",      "Falling"),
                "DiagonalTracking"   => ("AirTracking",      "Diagonal"),
                "CloseSwitching"     => ("Switching",        "4-Target"),
                "FarSwitching"       => ("Switching",        "6-Target"),
                "PeekReaction"       => ("PeekTraining",     "WideSwing"),
                _                    => ("Precision",        "Standard"),
            };
        }

        /// <summary>
        /// Returns a human-readable label for a dimension key.
        /// </summary>
        public static string GetDimensionLabel(string dimension)
        {
            return dimension switch
            {
                "CloseRangeStatic"   => "Close-Range Static",
                "LongRangeStatic"    => "Long-Range Static",
                "HorizontalTracking" => "Horizontal Tracking",
                "VerticalTracking"   => "Vertical Tracking",
                "DiagonalTracking"   => "Diagonal Tracking",
                "CloseSwitching"     => "Close Switching",
                "FarSwitching"       => "Far Switching",
                "PeekReaction"       => "Peek Reaction",
                _                    => dimension,
            };
        }

        // ── Internal ─────────────────────────────────────────────────────────

        private static double GetScore(IList<AimTrainerResult> results, int index)
        {
            if (index >= results.Count) return 0;
            return ScoreTest(results[index], Tests[index]);
        }
    }
}
