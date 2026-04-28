namespace CleanAimTracker.Models
{
    public class SensitivityRecommendation
    {
        // Core recommended values
        public double RecommendedSensitivity { get; set; }
        public double RecommendedEDPI { get; set; }
        public double RecommendedCm360 { get; set; }
        public string Reason { get; set; }
        // Sensitivity range (recommended min/max)
        public double RecommendedSensitivityMin { get; set; }
        public double RecommendedSensitivityMax { get; set; }

        // Confidence score (0–100)
        public int Confidence { get; set; }

        // Explanation paragraph (why these settings were chosen)
        public string Explanation { get; set; }

        // Additional diagnostic metrics
        public double CorrectionSharpnessScore { get; set; }
        public double VelocityStabilityScore { get; set; }
        public double IdlePenaltyScore { get; set; }

        // Profile-specific overrides (ADS, zoom, etc.)
        public double RecommendedADS { get; set; }
        public double RecommendedZoomSensitivity { get; set; }

        // Muscle memory protection (true = keep changes small)
        public bool MinimalChangeRecommended { get; set; }

        // Current user values (engine references these)
        public double CurrentDPI { get; set; }
        public double CurrentCm360 { get; set; }

        // Ranges used by RecommendationEngine
        public double Cm360RangeMin { get; set; }
        public double Cm360RangeMax { get; set; }

        // Verdict text
        public string Cm360Verdict { get; set; }

        // Consistency score used in multiple lines of the engine
        public double ConsistencyScore { get; set; }

        public double CurrentSensitivity { get; set; }
        public string GameName { get; set; }
        public double ProAverageCm360 { get; set; }
        public double JitterScore { get; set; }
        public double SmoothnessScore { get; set; }
        public double FlickControlScore { get; set; }
        public double OverallDiagnostic { get; set; }

        public int RecommendedDPI { get; set; }
        public string DpiVerdict { get; set; }
        public string SensVerdict { get; set; }
        public string OverallVerdict { get; set; }
        public List<string> Tips { get; set; }
        public bool HasTrendData { get; set; }
        public string TrendSummary { get; set; }



    }
}
