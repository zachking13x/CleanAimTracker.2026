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

            // AUDIT A8: Skip used to set only the two flags above, but App.OnStartup gates
            // the calibration flow on (!CalibrationComplete && !OnboardingSkipped && no
            // stored drills). None of those changed here, so a user who explicitly chose
            // Skip and then closed the app without training was routed straight back into
            // onboarding on the next launch. Setting the flag the gate actually reads makes
            // the choice stick.
            s.OnboardingSkipped = true;

            SettingsService.Save(s);
            Close();
        }
    }
}
