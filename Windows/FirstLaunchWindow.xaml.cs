using CleanAimTracker.Services;
using System.Windows;
using System.Windows.Input;

namespace CleanAimTracker
{
    public partial class FirstLaunchWindow : Window
    {
        public FirstLaunchWindow()
        {
            InitializeComponent();
        }

        private void HitStart_Click(object sender, RoutedEventArgs e)
        {
            var s = SettingsService.Load();
            s.FirstLaunchComplete = true;
            s.OnboardingAutoStart = true;   // MainWindow will auto-start + 90s auto-stop
            SettingsService.Save(s);
            Close();
        }

        private void Skip_Click(object sender, MouseButtonEventArgs e)
        {
            var s = SettingsService.Load();
            s.FirstLaunchComplete = true;
            s.OnboardingAutoStart = false;
            SettingsService.Save(s);
            Close();
        }
    }
}
