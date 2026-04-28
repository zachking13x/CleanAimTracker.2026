using CleanAimTracker.Models;
using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class SummaryWindow : Window
    {
        private readonly SessionSummary _s;
        private readonly SensitivityRecommendation _rec;

        public SummaryWindow(SessionSummary s, SensitivityRecommendation rec)
        {
            InitializeComponent();
            _s = s;
            _rec = rec;
            LoadSummary();
        }

        private void LoadSummary()
        {
            // Core stats
            TimeText.Text = $"Time: {_s.SessionSeconds:F1} s";
            DistanceText.Text = $"Distance: {_s.TotalDistanceCm:F1} cm";
            AvgSpeedText.Text = $"Avg Speed: {_s.AverageVelocity:F1} cm/s";
            PeakSpeedText.Text = $"Peak Speed: {_s.PeakVelocity:F1} cm/s";

            // Flicks
            FlicksText.Text = $"Flicks: {_s.FlickCount}";
            SmallFlicksText.Text = $"Small Flicks: {_s.SmallFlickCount}";
            LargeFlicksText.Text = $"Large Flicks: {_s.LargeFlickCount}";

            // Diagnostics
            SmoothnessText.Text = $"Smoothness: {_s.SmoothnessScore:F0}";
            SharpnessText.Text = $"Correction Sharpness: {_s.CorrectionSharpness:F0}";
            ConsistencyText.Text = $"Consistency: {_s.MovementConsistency:F0}";
            QualityText.Text = $"Overall Quality: {_s.OverallQualityScore:F0}";

            // Advanced diagnostics (from engine)
            CorrectionSharpnessText.Text = $"Sharpness Score: {_rec.CorrectionSharpnessScore:F0}";
            VelocityStabilityText.Text = $"Velocity Stability: {_rec.VelocityStabilityScore:F0}";
            IdlePenaltyText.Text = $"Idle Penalty: {_rec.IdlePenaltyScore:F0}";
            OverallDiagnosticText.Text = $"Weighted Diagnostic: {_rec.OverallDiagnostic:F0}";

            // Trend
            TrendText.Text = _rec.TrendSummary;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
