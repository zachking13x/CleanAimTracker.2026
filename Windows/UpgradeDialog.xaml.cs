using CleanAimTracker.Services;
using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class UpgradeDialog : Window
    {
        public UpgradeDialog(string featureName = "")
        {
            InitializeComponent();

            if (!string.IsNullOrEmpty(featureName))
            {
                FeatureHeadline.Text = $"Unlock {featureName}";
            }
        }

        private void Upgrade_Click(object sender, RoutedEventArgs e)
        {
            var win = new UpgradeWindow();
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
