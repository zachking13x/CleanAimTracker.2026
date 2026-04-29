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

            LoadRecommendedSettings();
            LoadSummary();
        }

        // ─────────────────────────────────────────────────────────────
        // Recommended Settings Section
        // ─────────────────────────────────────────────────────────────
        private void LoadRecommendedSettings()
        {
            // Sensitivity
            RecommendedSensitivityText.Text =
                $"Recommended Sensitivity: {_rec.RecommendedSensitivity:F4}";

            RecommendedSensitivityRangeText.Text =
                $"Range: {_rec.RecommendedSensitivityMin:F4} – {_rec.RecommendedSensitivityMax:F4}";

            // DPI
            RecommendedDpiText.Text =
                $"Recommended DPI: {_rec.RecommendedDPI} DPI";

            // cm/360
            RecommendedCm360Text.Text =
                $"Recommended cm/360: {_rec.RecommendedCm360:F1} cm";

            RecommendedCm360RangeText.Text =
                $"Range: {_rec.Cm360RangeMin:F1} – {_rec.Cm360RangeMax:F1} cm";

            // Warning if user is outside recommended range
            if (_s.CmPer360 < _rec.Cm360RangeMin)
            {
                RecommendedCm360WarningText.Text =
                    $"Your current cm/360 ({_s.CmPer360:F1}) is below the recommended range.";
            }
            else if (_s.CmPer360 > _rec.Cm360RangeMax)
            {
                RecommendedCm360WarningText.Text =
                    $"Your current cm/360 ({_s.CmPer360:F1}) is above the recommended range.";
            }
            else
            {
                RecommendedCm360WarningText.Text = "";
            }

            // Confidence
            ConfidenceText.Text =
                $"Confidence: {_rec.Confidence}%";

            // Explanation
            ExplanationText.Text = _rec.Explanation;

            // Tips
            if (_rec.Tips != null && _rec.Tips.Count > 0)
            {
                TipsText.Text = string.Join("\n• ", _rec.Tips)
                    .Insert(0, "• ");
            }
            else
            {
                TipsText.Text = "No tips available.";
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Core Stats Section
        // ─────────────────────────────────────────────────────────────
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
