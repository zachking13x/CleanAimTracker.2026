using CleanAimTracker.Services;
using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class UpgradeDialog : Window
    {
        // CAT_TELEMETRY: which locked feature raised this dialog, carried through to the
        // UpgradeWindow so the funnel attributes the sale to the right prompt.
        private readonly string _featureName;

        public UpgradeDialog(string featureName = "")
        {
            InitializeComponent();
            _featureName = string.IsNullOrWhiteSpace(featureName) ? "generic" : featureName;
            TelemetryService.TrackPaywallShown("feature_gate_" + _featureName);

            // Prices from the single source of truth (Services/Pricing.cs).
            LifetimePrice.Text = Pricing.Lifetime;
            LifetimeBadge.Text = Pricing.LifetimeLabel.ToUpperInvariant();
            MonthlyPrice.Text  = Pricing.Monthly;

            if (!string.IsNullOrEmpty(featureName))
            {
                FeatureHeadline.Text = $"Unlock {featureName}";
            }
        }

        private void Upgrade_Click(object sender, RoutedEventArgs e)
        {
            var win = new UpgradeWindow("feature_gate_" + _featureName);
            win.Owner = this;
            win.ShowDialog();
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>Shows the upgrade dialog with an optional specific feature name in the headline.</summary>
        public static void Show(string featureName = "")
        {
            var win = new UpgradeDialog(featureName);
            win.Owner = Application.Current.MainWindow;
            win.ShowDialog();
        }
    }
}
