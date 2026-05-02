using CleanAimTracker.Models;
using System.Text;
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

            // Bind the entire window to the recommendation object
            DataContext = rec;
        }

        // ════════════════════════════════════════════════════════════
        //  COPY SETTINGS
        // ════════════════════════════════════════════════════════════
        private void CopySettings_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();

            sb.AppendLine("🎯 Recommended Settings");
            sb.AppendLine($"• DPI: {_rec.RecommendedDPI}");
            sb.AppendLine($"• Sensitivity: {_rec.RecommendedSensitivity:F4}");
            sb.AppendLine($"• Sensitivity Range: {_rec.RecommendedSensitivityMin:F4} – {_rec.RecommendedSensitivityMax:F4}");
            sb.AppendLine($"• cm/360: {_rec.RecommendedCm360:F2}");
            sb.AppendLine();
            sb.AppendLine("Confidence: " + _rec.Confidence + "%");
            sb.AppendLine();
            sb.AppendLine("Explanation:");
            sb.AppendLine(_rec.Explanation);

            Clipboard.SetText(sb.ToString());
            MessageBox.Show("Settings copied to clipboard.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ════════════════════════════════════════════════════════════
        //  COPY COMPARISON
        // ════════════════════════════════════════════════════════════
        private void CopyComparison_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();

            sb.AppendLine("📊 Current vs Recommended");
            sb.AppendLine();
            sb.AppendLine("Current:");
            sb.AppendLine($"• DPI: {_rec.CurrentDPI}");
            sb.AppendLine($"• Sensitivity: {_rec.CurrentSensitivity:F4}");
            sb.AppendLine($"• cm/360: {_rec.CurrentCm360:F2}");
            sb.AppendLine();
            sb.AppendLine("Recommended:");
            sb.AppendLine($"• DPI: {_rec.RecommendedDPI}");
            sb.AppendLine($"• Sensitivity: {_rec.RecommendedSensitivity:F4}");
            sb.AppendLine($"• cm/360: {_rec.RecommendedCm360:F2}");
            sb.AppendLine();
            sb.AppendLine("Verdicts:");
            sb.AppendLine($"• DPI: {_rec.DpiVerdict}");
            sb.AppendLine($"• Sensitivity: {_rec.SensVerdict}");
            sb.AppendLine($"• cm/360: {_rec.Cm360Verdict}");
            sb.AppendLine();
            sb.AppendLine("Overall:");
            sb.AppendLine(_rec.OverallVerdict);

            Clipboard.SetText(sb.ToString());
            MessageBox.Show("Comparison copied to clipboard.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ════════════════════════════════════════════════════════════
        //  CLOSE WINDOW
        // ════════════════════════════════════════════════════════════
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
