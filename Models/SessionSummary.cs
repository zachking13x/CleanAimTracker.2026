namespace CleanAimTracker.Models
{
    public class SessionSummary
    {
        public string GameName { get; set; }
        public double Sensitivity { get; set; }
        public double EDPI { get; set; }
        public double Cm360 { get; set; }
        public int ShotsFired { get; set; }
        public int ShotsHit { get; set; }
        public double Accuracy { get; set; }
        public string Verdict { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeSpan Duration { get; set; }
        public int TotalSamples { get; set; }
        public int DPI { get; set; }
        public double CmPer360 { get; set; }
        public double AverageVelocity { get; set; }
        public double PeakVelocity { get; set; }
        public double TotalDistanceCm { get; set; }
        public double AvgDistPerEvent { get; set; }
        public double PeakDistPerEvent { get; set; }
        public double PeakAcceleration { get; set; }
        public double AvgAcceleration { get; set; }
        public int FlickCount { get; set; }
        public int SmallFlickCount { get; set; }
        public int LargeFlickCount { get; set; }
        public int IdleBurstCount { get; set; }
        public double SmoothnessScore { get; set; }
        public double CorrectionSharpness { get; set; }
        public double MovementConsistency { get; set; }
        public double OverallQualityScore { get; set; }
        public double IdlePercentage { get; set; }
        public double JitterAmount { get; set; }
        public string ProfileName { get; set; }
        public double SessionSeconds { get; set; }

        /// <summary>
        /// The actual in-game sensitivity value for the selected profile.
        /// Computed from DPI, raw sensitivity, and the profile's YawPerCount.
        /// When > 0 the recommendation engine uses this in preference to Sensitivity.
        /// </summary>
        public double GameSensitivity { get; set; }

        /// <summary>
        /// TASK-1.2: per-metric validity, keyed by metric property name
        /// ("SmoothnessScore", "MovementConsistency", "CorrectionSharpness",
        /// "OverallQualityScore"). Sessions persisted before this field exists
        /// deserialize to an empty dictionary — GetValidity treats missing
        /// entries as NotComputed, so legacy values are never narrated as valid.
        /// </summary>
        public Dictionary<string, MetricValidity> MetricValidities { get; set; } = new();

        public MetricValidity GetValidity(string metricName) =>
            MetricValidities.TryGetValue(metricName, out var v)
                ? v
                : MetricValidity.Invalid(MetricInvalidReason.NotComputed, 0);

        public bool IsMetricValid(string metricName) => GetValidity(metricName).IsValid;





    }
}
