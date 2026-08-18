using CleanAimTracker.Services;
using System;
using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var s = SettingsService.Load();
            DpiInput.Text          = s.DPI.ToString();
            SensitivityInput.Text  = s.Sensitivity.ToString("F4");
            ThemeSelector.SelectedIndex = s.ThemeMode == "Light" ? 1 : 0;

            SoundToggle.IsChecked = s.SoundEnabled;
            ReportEveryDrillToggle.IsChecked = s.ReportAfterEveryDrill;
            NotificationsToggle.IsChecked = s.NotificationsEnabled;
            // Honest signal: if Windows itself has CAT toasts off, say so — a checked
            // toggle wouldn't deliver anything.
            NotificationsOsWarning.Visibility =
                ToastService.OsAllowsToasts() ? Visibility.Collapsed : Visibility.Visible;

            // CAT_TELEMETRY: only offer the control when the build can actually send
            // anything. Showing a privacy toggle on an inert build would imply data is
            // leaving the machine when none is.
            bool configured = !string.IsNullOrWhiteSpace(TelemetryConfig.ConnectionString);
            PrivacySection.Visibility = configured ? Visibility.Visible : Visibility.Collapsed;
            TelemetryToggle.IsChecked = s.TelemetryEnabled;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var s = SettingsService.Load();

            if (double.TryParse(DpiInput.Text, out double dpi) && dpi >= 100 && dpi <= 32000)
                s.DPI = (int)dpi;
            else if (double.TryParse(DpiInput.Text, out _))
            {
                MessageBox.Show("DPI must be between 100 and 32000.", "Invalid DPI",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (double.TryParse(SensitivityInput.Text, out double sens) && sens >= 0.001 && sens <= 100.0)
                s.Sensitivity = sens;
            else if (double.TryParse(SensitivityInput.Text, out _))
            {
                MessageBox.Show("Sensitivity must be between 0.001 and 100.", "Invalid Sensitivity",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            s.ThemeMode = ThemeSelector.SelectedIndex == 1 ? "Light" : "Dark";

            s.SoundEnabled = SoundToggle.IsChecked == true;
            s.ReportAfterEveryDrill = ReportEveryDrillToggle.IsChecked == true;
            SoundService.SetEnabled(s.SoundEnabled);   // apply immediately

            s.NotificationsEnabled   = NotificationsToggle.IsChecked == true;
            s.NotificationPermissionAsked = true;   // the user has now made an explicit choice

            SettingsService.Save(s);
            ThemeService.ApplyTheme(s.ThemeMode);

            // CAT_TELEMETRY: applied through the service, not written directly — it owns
            // sending the final opt-out event and discarding the install id, and it
            // re-reads settings itself so it can't be clobbered by the save above.
            if (PrivacySection.Visibility == Visibility.Visible)
                TelemetryService.SetEnabled(TelemetryToggle.IsChecked == true);

            // Apply the notification choice immediately: schedule fresh nudges if enabled,
            // or clear any pending ones if the user just turned them off.
            ToastService.RescheduleNudges();

            MessageBox.Show("Settings saved.", "Saved",
                MessageBoxButton.OK, MessageBoxImage.None);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => Close();

        // CAT_FIRST_FUN_AND_FUNNEL T1.2: hidden dev/tester view — tap the "Settings" header
        // 5× to read this install's LOCAL onboarding funnel (never transmitted anywhere).
        private int _devTapCount;
        private void DevFunnel_Tap(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (++_devTapCount < 5) return;
            _devTapCount = 0;
            try
            {
                var s = SettingsService.Load();
                string state = s.PreviewAsFreeUser ? "ON" : "OFF";
                var choice = MessageBox.Show(
                    OnboardingFunnelService.FormatReport() +
                    $"\n\n──  DEV TOOLS  ──\n" +
                    $"Preview as FREE user is currently {state}.\n" +
                    $"Toggle it to see the paywall / locked coach exactly as a customer does?\n\n" +
                    $"(Yes = flip it, then restart the app.)",
                    "Dev tools (local only)", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (choice == MessageBoxResult.Yes)
                {
                    s.PreviewAsFreeUser = !s.PreviewAsFreeUser;
                    SettingsService.Save(s);
                    MessageBox.Show(
                        s.PreviewAsFreeUser
                            ? "Preview-as-free is now ON. Restart the app — you'll see the app as a free user (locked coach, paywall cards, real banner)."
                            : "Preview-as-free is now OFF. Restart — your dev Pro access is back.",
                        "Dev tools", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch { /* dev view only — never disrupt settings */ }
        }

        // CAT_REVIEW_PROMPT Gate 2 / T2.1: minimal feedback — opens a prefilled mail to the
        // dev. Pull, never push: only ever reached from Settings, never an unprompted popup.
        // Doubles as a private valve so a frustrated user vents here, not via a 1-star review.
        private void Feedback_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string url = "mailto:zachking13@outlook.com?subject=" + Uri.EscapeDataString("CAT feedback");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex) { LogService.Error("Open feedback mail failed", ex); }
        }
    }
}
