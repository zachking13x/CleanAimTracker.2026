using CleanAimTracker.Models;
using CleanAimTracker.Services;
using CleanAimTracker.Windows;
using System.Windows;

namespace CleanAimTracker
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Prevent WPF from shutting down when FirstLaunchWindow closes (it's the only
            // window at that point and the default OnLastWindowClose would kill the process
            // before MainWindow.Show() is reached).
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            GameProfile.ValidateProfiles(); // guard against bad yaw values like Fortnite's 0.5585 regression
            TrialService.Initialize();
            _ = LicenseService.InitializeAsync(); // background — does not block startup

            var settings = SettingsService.Load();

            ThemeService.ApplyTheme(settings.ThemeMode ?? "Dark");

            if (!settings.FirstLaunchComplete)
            {
                var onboarding = new FirstLaunchWindow();
                onboarding.ShowDialog();
            }

            var main = new MainWindow();
            main.Show();

            // Switch to OnMainWindowClose now that the real window is visible
            MainWindow    = main;
            ShutdownMode  = ShutdownMode.OnMainWindowClose;

            // Toast notifications — all non-critical, fire after main window shows
            ToastService.CheckAndNotify();
            ToastService.CheckAndScheduleReEngagement();
            ToastService.ScheduleStreakAtRiskIfNeeded();
            ToastService.ScheduleWeeklySummaryIfNeeded();
        }
    }
}
