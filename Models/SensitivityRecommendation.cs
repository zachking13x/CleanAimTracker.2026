namespace CleanAimTracker.Models
{
    public class SensitivityRecommendation
    {
        // Core recommended values
        public double RecommendedSensitivity { get; set; }
        public double RecommendedEDPI { get; set; }
        public double RecommendedCm360 { get; set; }
        public string Reason { get; set; }

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
    }
}
