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
            // FirstLaunchComplete is intentionally NOT set here.
            // It is set only after the full onboarding flow completes (step 6).
            // If the user closes mid-flow they will see onboarding again on next launch.
            s.OnboardingAutoStart = true;
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
