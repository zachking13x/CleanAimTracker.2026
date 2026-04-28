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
            // Sensitivity
            SensValue.Text = $"{_rec.RecommendedSensitivity:F4}";
            SensRange.Text = $"Range: {_rec.RecommendedSensitivityMin:F4} – {_rec.RecommendedSensitivityMax:F4}";

            // DPI
            DpiValue.Text = $"{_rec.RecommendedDPI} DPI";

            // cm/360
            Cm360Value.Text = $"{_rec.RecommendedCm360:F1} cm/360";
            Cm360Verdict.Text = _rec.Cm360Verdict;

            // Confidence
            ConfidenceValue.Text = $"{_rec.Confidence}%";

            // Explanation
            ExplanationText.Text = _rec.Explanation;

            // Tips
            TipsList.ItemsSource = _rec.Tips;

            // Trend
            TrendText.Text = _rec.TrendSummary;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
