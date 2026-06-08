namespace CleanAimTracker.Models
{
    public class DiagnosticProfile
    {
        public DateTime CompletedAt      { get; set; }
        public double CloseRangeStatic   { get; set; } = 0;
        public double LongRangeStatic    { get; set; } = 0;
        public double HorizontalTracking { get; set; } = 0;
        public double VerticalTracking   { get; set; } = 0;
        public double DiagonalTracking   { get; set; } = 0;
        public double CloseSwitching     { get; set; } = 0;
        public double FarSwitching       { get; set; } = 0;
        public double PeekReaction       { get; set; } = 0;

        // Derived weakest and strongest dimensions
        public string WeakestDimension   { get; set; } = "";
        public string StrongestDimension { get; set; } = "";

        // Session number when this assessment was taken
        public int SessionNumber         { get; set; } = 0;
    }
}
