using CleanAimTracker.Models;
using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class RecommendationWindow : Window
    {
        private readonly SensitivityRecommendation _rec;

        public RecommendationWindow(SensitivityRecommendation rec)
        {
            InitializeComponent();
            _rec = rec;
            LoadRecommendation();
        }

        private void LoadRecommendation()
        {
            // ─────────────────────────────────────────────
            // Sensitivity
            // ─────────────────────────────────────────────
            SensValue.Text = $"{_rec.RecommendedSensitivity:F4}";
            SensRange.Text =
                $"Range: {_rec.RecommendedSensitivityMin:F4} – {_rec.RecommendedSensitivityMax:F4}";

            // ─────────────────────────────────────────────
            // DPI
            // ─────────────────────────────────────────────
            DpiValue.Text = $"{_rec.RecommendedDPI} DPI";

            // ─────────────────────────────────────────────
            // cm/360
            // ─────────────────────────────────────────────
            Cm360Value.Text = $"{_rec.RecommendedCm360:F1} cm/360";

            // If your engine already generates a verdict, use it
            if (!string.IsNullOrWhiteSpace(_rec.Cm360Verdict))
            {
                Cm360Verdict.Text = _rec.Cm360Verdict;
            }
            else
            {
                // Otherwise generate a clean fallback verdict
                Cm360Verdict.Text =
                    $"Recommended range: {_rec.Cm360RangeMin:F1} – {_rec.Cm360RangeMax:F1} cm/360";
            }

            // ─────────────────────────────────────────────
            // Confidence
            // ─────────────────────────────────────────────
            ConfidenceValue.Text = $"{_rec.Confidence}%";

            // ─────────────────────────────────────────────
            // Explanation
            // ─────────────────────────────────────────────
            ExplanationText.Text = _rec.Explanation;

            // ─────────────────────────────────────────────
            // Tips
            // ─────────────────────────────────────────────
            TipsList.ItemsSource = _rec.Tips;

            // ─────────────────────────────────────────────
            // Trend
            // ─────────────────────────────────────────────
            TrendText.Text = _rec.TrendSummary;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
