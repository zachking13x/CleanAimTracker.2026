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

        // ── TASK-1.3: low-activity session detection ─────────────────────────
        // Floors calibrated against real session history (2026-05/06 data):
        //   genuine gameplay sessions: AvgVel ≈ 1.0–1.25 cm/s, ≈ 59–74 cm of
        //   travel per minute. A known junk session (mouse mostly untouched):
        //   AvgVel 0.45 cm/s, 28 cm/min. Floors sit between the two clusters.
        // IdlePercentage > 80 kept per spec, but observed values are near 0
        // even on hour-long sessions, so velocity/distance floors are the
        // effective gates.
        public const double LowActivityIdlePctFloor       = 80.0;
        public const double LowActivityMinAvgVelocity     = 0.7;  // cm/s
        public const double LowActivityMinDistPerMinute   = 45.0; // cm of travel per minute

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsLowActivitySession
        {
            get
            {
                if (IdlePercentage > LowActivityIdlePctFloor) return true;
                if (AverageVelocity > 0 && AverageVelocity < LowActivityMinAvgVelocity) return true;
                if (SessionSeconds >= 60)
                {
                    // Hand-check: 74 cm over 158 s → 74/(158/60) = 28.1 cm/min < 45 → low.
                    //             3747 cm over 3447 s → 65.2 cm/min ≥ 45 → normal.
                    double distPerMinute = TotalDistanceCm / (SessionSeconds / 60.0);
                    if (distPerMinute < LowActivityMinDistPerMinute) return true;
                }
                return false;
            }
        }





    }
}
