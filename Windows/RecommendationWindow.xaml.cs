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

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // ⭐ ADD THIS HERE ⭐
        private void OpenGlossary_Click(object sender, RoutedEventArgs e)
        {
            new GlossaryWindow().Show();
        }

        private void LoadRecommendation()
        {
            // These MUST match your XAML element names
            DpiText.Text = _rec.RecommendedDPI.ToString();
            SensText.Text = _rec.RecommendedSensitivity.ToString("F3");
            Cm360Text.Text = _rec.RecommendedCm360.ToString("F1");

            VerdictText.Text = _rec.OverallVerdict;
            DpiVerdictText.Text = _rec.DpiVerdict;
            SensVerdictText.Text = _rec.SensVerdict;
            Cm360VerdictText.Text = _rec.Cm360Verdict;

            TipsList.ItemsSource = _rec.Tips;

            TrendText.Text = _rec.HasTrendData
                ? _rec.TrendSummary
                : "Not enough data for trend analysis.";
        }
    }
}
