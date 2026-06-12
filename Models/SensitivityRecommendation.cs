using System.Collections.Generic;

namespace CleanAimTracker.Models
{
    public class SensitivityRecommendation
    {
        // -----------------------------
        // CURRENT USER VALUES
        // -----------------------------
        public int CurrentDPI { get; set; }
        public double CurrentSensitivity { get; set; }
        public double CurrentCm360 { get; set; }

        // -----------------------------
        // GAME PROFILE VALUES
        // -----------------------------
        public string GameName { get; set; } = "";
        public double ProAverageCm360 { get; set; }
        public double Cm360RangeMin { get; set; }
        public double Cm360RangeMax { get; set; }

        // -----------------------------
        // RECOMMENDED VALUES
        // -----------------------------
        public int RecommendedDPI { get; set; }
        public double RecommendedSensitivity { get; set; }
        public double RecommendedCm360 { get; set; }
        // TASK-3.1: sensitivity displays at 2 decimals — never "7.6208"/"13.0000".
        public string SensitivityRangeText =>
            $"{RecommendedSensitivityMin:F2} – {RecommendedSensitivityMax:F2}";

        public double RecommendedSensitivityMin { get; set; }
        public double RecommendedSensitivityMax { get; set; }

        public double RecommendedRangeMin { get; set; }
        public double RecommendedRangeMax { get; set; }

        // -----------------------------
        // SCORE COMPONENTS
        // -----------------------------
        public double SmoothnessScore { get; set; }
        public double ConsistencyScore { get; set; }
        public double FlickControlScore { get; set; }
        public double CorrectionSharpnessScore { get; set; }
        public double VelocityStabilityScore { get; set; }
        public double JitterScore { get; set; }
        public double IdlePenaltyScore { get; set; }

        // -----------------------------
        // VERDICTS (STRING — REQUIRED)
        // -----------------------------
        public string DpiVerdict { get; set; } = "";
        public string SensVerdict { get; set; } = "";
        public string Cm360Verdict { get; set; } = "";
        public string OverallVerdict { get; set; } = "";
        public double OverallDiagnostic { get; set; }


        // -----------------------------
        // CONFIDENCE
        // -----------------------------
        public int Confidence { get; set; }

        public double ConfidenceScore { get; set; }

        // -----------------------------
        // EXPLANATION + TIPS
        // -----------------------------
        public string Explanation { get; set; } = "";
        public List<string> Tips { get; set; } = new();

        // -----------------------------
        // TREND ANALYSIS
        // -----------------------------
        public bool HasTrendData { get; set; }
        public string TrendSummary { get; set; } = "";
        public string TrendAnalysis { get; set; } = "";

        // -----------------------------
        // FLAGS
        // -----------------------------
        public bool MinimalChangeRecommended { get; set; }

        // TASK-3.1: confidence gate. Below the floor the engine recommends NO
        // change (recommended values = current values) and the verdicts carry
        // the collecting-data message instead of an actionable prescription.
        public bool IsActionable { get; set; } = true;

        // TASK-3.1: true when the recommended cm/360 differs from current by
        // more than 15% — surfaces MUST render the step-by-step transition plan.
        public bool RequiresTransitionPlan { get; set; }
    }
}
