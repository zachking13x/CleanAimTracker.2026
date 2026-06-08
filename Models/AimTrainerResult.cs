namespace CleanAimTracker.Models
{
    public class AimTrainerResult
    {
        public DateTime Timestamp { get; set; }

        public string Scenario    { get; set; } = "";
        public string SubVariant  { get; set; } = "";   // e.g. "Smooth", "Evasive", "Standard"
        public string Difficulty  { get; set; } = "";
        public int    DurationSeconds { get; set; }

        public int Hits { get; set; }
        public int Misses { get; set; }
        public double Accuracy { get; set; }

        public int Score { get; set; }

        public double AvgReactionMs { get; set; }
        public double BestReactionMs { get; set; }

        public int MaxStreak { get; set; }

        // Raw input derived metrics
        public double PathEfficiency          { get; set; } = 0;
        public double AvgClickOffset          { get; set; } = 0;
        public double OvershootPct            { get; set; } = 0;
        public double UndershootPct           { get; set; } = 0;
        public double AvgDirectionChangeLagMs { get; set; } = 0;
        public double FirstMotionAccuracy     { get; set; } = 0;
        public double HorizontalTrackingAcc   { get; set; } = 0;
        public double VerticalTrackingAcc     { get; set; } = 0;
        public double PeekEarlyClickPct       { get; set; } = 0;
        public double PeekLateClickPct        { get; set; } = 0;

        // Scenario classification
        public string Pillar                  { get; set; } = "";

        // Diagnostic assessment flag
        public bool   IsAssessmentSession     { get; set; } = false;
        public string AssessmentDimension     { get; set; } = "";
    }
}
