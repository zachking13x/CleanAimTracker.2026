using CleanAimTracker.Models;
using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class RecommendedDrillsWindow : Window
    {
        public RecommendedDrillsWindow(SensitivityRecommendation rec)
        {
            InitializeComponent();

            SettingsText.Text =
                $"• Recommended cm/360: {rec.RecommendedCm360:F1}\n" +
                $"• Recommended DPI: {rec.RecommendedDPI}\n" +
                $"• Recommended Sensitivity: {rec.RecommendedSensitivity:F3}\n" +
                $"• Confidence: {rec.Confidence}%";

            DrillListText.Text =
                $"- Tracking (improves smoothness)\n" +
                $"- Flicking (improves correction sharpness)\n" +
                $"- Switching (improves consistency)\n";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

    }
}
