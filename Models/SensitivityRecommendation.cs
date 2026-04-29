using System.Collections.Generic;

namespace CleanAimTracker.Models
{
    public class SensitivityRecommendation
    {
        // ─────────────────────────────────────────────
        // Core recommended values
        // ─────────────────────────────────────────────
        public double RecommendedSensitivity { get; set; }
        public double RecommendedSensitivityMin { get; set; }
        public double RecommendedSensitivityMax { get; set; }

        public int RecommendedDPI { get; set; }
        public double RecommendedEDPI { get; set; }

        public double RecommendedCm360 { get; set; }
        public double Cm360RangeMin { get; set; }
        public double Cm360RangeMax { get; set; }

        // ─────────────────────────────────────────────
        // Confidence & explanation
        // ─────────────────────────────────────────────
        public int Confidence { get; set; } // 0–100
        public string Explanation { get; set; } = "";
        public List<string> Tips { get; set; } = new();

        // ─────────────────────────────────────────────
        // Advanced diagnostic metrics
        // ─────────────────────────────────────────────
        public double CorrectionSharpnessScore { get; set; }
        public double VelocityStabilityScore { get; set; }
        public double IdlePenaltyScore { get; set; }
        public double OverallDiagnostic { get; set; }

        // ─────────────────────────────────────────────
        // Profile-specific overrides
        // ─────────────────────────────────────────────
        public double RecommendedADS { get; set; }
        public double RecommendedZoomSensitivity { get; set; }
        public bool MinimalChangeRecommended { get; set; }

        // ─────────────────────────────────────────────
        // Current user values (engine references these)
        // ─────────────────────────────────────────────
        public double CurrentDPI { get; set; }
        public double CurrentSensitivity { get; set; }
        public double CurrentCm360 { get; set; }

        // ─────────────────────────────────────────────
        // Verdicts
        // ─────────────────────────────────────────────
        public string DpiVerdict { get; set; }
        public string SensVerdict { get; set; }
        public string Cm360Verdict { get; set; }
        public string OverallVerdict { get; set; }

        // ─────────────────────────────────────────────
        // Trend data
        // ─────────────────────────────────────────────
        public bool HasTrendData { get; set; }
        public string TrendSummary { get; set; } = "";

        // ─────────────────────────────────────────────
        // Extra metadata
        // ─────────────────────────────────────────────
        public string GameName { get; set; }
        public double ProAverageCm360 { get; set; }
        public double JitterScore { get; set; }
        public double SmoothnessScore { get; set; }
        public double FlickControlScore { get; set; }
        public double ConsistencyScore { get; set; }
        public string Reason { get; set; }
    }
}
