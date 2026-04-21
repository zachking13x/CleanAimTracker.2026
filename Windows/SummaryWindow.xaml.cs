using CleanAimTracker.Models;
using System.Windows;

namespace CleanAimTracker
{
    public partial class SummaryWindow : Window
    {
        public SummaryWindow(SessionSummary summary)
        {
            InitializeComponent();
            PopulateSummary(summary);
        }

        private void PopulateSummary(SessionSummary s)
        {
            // Session
            DurationText.Text = $"Duration: {s.Duration:mm\\:ss}";
            TotalSamplesText.Text = $"Total Samples: {s.TotalSamples:N0}";
            SensitivityInfoText.Text = $"DPI: {s.DPI}  |  Sens: {s.Sensitivity}  |  cm/360: {s.CmPer360:F2}";

            // Velocity
            AvgVelocityText.Text = $"Average: {s.AverageVelocity:F2} cm/s";
            PeakVelocityText.Text = $"Peak: {s.PeakVelocity:F2} cm/s";

            // Distance
            TotalDistanceText.Text = $"Total: {s.TotalDistanceCm:F2} cm";
            AvgDistPerEventText.Text = $"Avg/Event: {s.AvgDistPerEvent:F2}";
            PeakDistPerEventText.Text = $"Peak/Event: {s.PeakDistPerEvent:F2}";

            // Acceleration
            PeakAccelText.Text = $"Peak: {s.PeakAcceleration:F2} cm/s2";
            AvgAccelText.Text = $"Average: {s.AvgAcceleration:F2} cm/s2";

            // Flicks
            FlickCountText.Text = $"Total: {s.FlickCount}";
            SmallFlicksText.Text = $"Small (50-100 cm/s): {s.SmallFlickCount}";
            LargeFlicksText.Text = $"Large (100+ cm/s): {s.LargeFlickCount}";
            IdleBurstText.Text = $"Idle Bursts: {s.IdleBurstCount}";

            // Quality
            OverallQualityText.Text = $"Overall: {s.OverallQualityScore:F0}";
            SmoothnessText.Text = $"Smoothness: {s.SmoothnessScore:F0}";
            CorrectionSharpnessText.Text = $"Correction Sharpness: {s.CorrectionSharpness:F0}";
            ConsistencyText.Text = $"Movement Consistency: {s.MovementConsistency:F0}";

            // Idle & Jitter
            IdlePercentText.Text = $"Idle %: {s.IdlePercentage:F1}%";
            JitterText.Text = $"Jitter Events: {s.JitterAmount:F0}";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
