using CleanAimTracker.Windows;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace CleanAimTracker
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();

            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = ver != null
                ? $"Version {ver.Major}.{ver.Minor}.{ver.Build}"
                : "Version 1.0";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
            => Close();

        private void UpgradeBanner_Click(object sender, MouseButtonEventArgs e)
            => UpgradeDialog.Show();

        private void PrivacyLink_Click(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo(
                "https://github.com/zachking13x/cleanaimtracker-privacy")
                { UseShellExecute = true });
        }
    }
}
