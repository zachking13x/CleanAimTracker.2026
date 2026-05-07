using CleanAimTracker.Services;
using CleanAimTracker.Windows;
using System.Windows;

namespace CleanAimTracker
{
    public partial class FirstLaunchWindow : Window
    {
        public FirstLaunchWindow()
        {
            InitializeComponent();

            // Pre-fill from saved settings if any
            var s = SettingsService.Load();
            DpiInput.Text = s.DPI.ToString("F0");
            SensInput.Text = s.Sensitivity.ToString("F4");
        }

        private void GetStarted_Click(object sender, RoutedEventArgs e)
        {
            var s = SettingsService.Load();

            if (double.TryParse(DpiInput.Text, out double dpi) && dpi > 0)
                s.DPI = (int)dpi;

            if (double.TryParse(SensInput.Text, out double sens) && sens > 0)
                s.Sensitivity = sens;

            s.FirstLaunchComplete = true;
            SettingsService.Save(s);

            // App.xaml.cs opens MainWindow after ShowDialog() returns — do NOT open it here
            Close();
        }
    }
}
