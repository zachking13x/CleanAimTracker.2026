namespace CleanAimTracker.Models
{
    public class RecommendationResult
    {
        public double MinSensitivity { get; set; }
        public double MaxSensitivity { get; set; }
        public int Confidence { get; set; }
        public string Explanation { get; set; }
    }
}
