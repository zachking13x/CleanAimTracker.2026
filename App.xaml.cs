using CleanAimTracker.Models;
using CleanAimTracker.Services;
using CleanAimTracker.Windows;
using System;
using System.Windows;
using System.Windows.Threading;

namespace CleanAimTracker
{
    public partial class App : Application
    {
        // Refresh Store entitlements every 30 minutes so subscription cancellations
        // and new purchases are reflected without restarting the app.
        private DispatcherTimer? _licenseRefreshTimer;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            LogService.Initialize();
            LogService.CleanOldLogs();

            // ── Global exception handlers ─────────────────────────────────────
            DispatcherUnhandledException += (_, args) =>
            {
                LogService.Fatal("Unhandled dispatcher exception", args.Exception);
                args.Handled = true;
                ShowCrashDialog(args.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                LogService.Fatal("Unhandled AppDomain exception",
                    ex ?? new Exception(args.ExceptionObject?.ToString() ?? "Unknown error"));
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                LogService.Error("Unobserved task exception", args.Exception);
                args.SetObserved();   // prevent process termination
            };
            // ─────────────────────────────────────────────────────────────────

            // Prevent WPF from shutting down when FirstLaunchWindow closes (it's the only
            // window at that point and the default OnLastWindowClose would kill the process
            // before MainWindow.Show() is reached).
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            GameProfile.ValidateProfiles(); // guard against bad yaw values like Fortnite's 0.5585 regression
            TrialService.Initialize();
            _ = LicenseService.InitializeAsync(); // background — does not block startup

            // Periodic license refresh — every 30 minutes keeps subscription status current.
            _licenseRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            _licenseRefreshTimer.Tick += async (_, _) => await LicenseService.RefreshEntitlementsAsync();
            _licenseRefreshTimer.Start();

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
        private static void ShowCrashDialog(Exception ex)
        {
            MessageBox.Show(
                "Something went wrong and CleanAimTracker ran into an unexpected error.\n\n" +
                "If this happened during a purchase, you were not charged — the transaction " +
                "was not completed. You can try again or use \"Already purchased? Restore\" " +
                "in the upgrade screen to recover access.\n\n" +
                "The error has been logged and will help us fix this in a future update.\n\n" +
                $"Error: {ex.GetType().Name}",
                "Unexpected Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
