using CleanAimTracker.Models;
using CleanAimTracker.Services;
using CleanAimTracker.Windows;
using System;
using System.Linq;
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
            SoundService.SetEnabled(settings.SoundEnabled);   // game-feel audio honors the mute setting

            // TASK-4.1: ONE first-run flow. New users get the calibration flow
            // (Welcome → Brief → 4 tests → First Insight → First Drill), which
            // sets FirstLaunchComplete itself — the legacy FirstLaunchWindow
            // wizard can never stack on top of it. It remains only as the
            // fallback for legacy installs that predate CalibrationComplete.
            bool isNewUser = !settings.CalibrationComplete
                && !settings.OnboardingSkipped
                && AimTrainerStorage.LoadAll().Count == 0;
            if (isNewUser)
            {
                // CAT_FIRST_FUN_AND_FUNNEL T1.1: local-only funnel (nothing transmitted).
                OnboardingFunnelService.Record(OnboardingFunnelService.AppFirstOpen);
                OnboardingFunnelService.Record(OnboardingFunnelService.OnboardingShown);
                new OnboardingCalibrationWindow().ShowDialog();
                settings = SettingsService.Load(); // onboarding mutates settings
            }
            else if (!settings.FirstLaunchComplete)
            {
                new FirstLaunchWindow().ShowDialog();
            }

            var main = new MainWindow();
            main.Show();

            // Switch to OnMainWindowClose now that the real window is visible
            MainWindow    = main;
            ShutdownMode  = ShutdownMode.OnMainWindowClose;

            // CAT_RETENTION_NOTIFICATIONS: the user is here now, so clear any pending
            // win-back/streak nudges (a stale one firing after they've returned is noise)
            // and IMMEDIATELY rebuild the queue from current state.
            //
            // CAT_RETENTION_HOLES (2026-07-30): this used to clear-only and defer the
            // rebuild to OnExit. But OnExit does NOT run on a kill, crash, or OS-initiated
            // terminate — and on those paths the user was left with an EMPTY queue, i.e.
            // no nudges at all, silently, forever. Rescheduling here means the queue is
            // never empty for longer than this method takes; OnExit still refreshes it
            // with end-of-session state.
            // CAT_TELEMETRY: must run BEFORE ClearScheduled — it works out which nudges
            // already fired by looking at what is NO LONGER in the pending list, and
            // clearing wipes that evidence.
            try { ToastService.ReportNudgeAttribution(); } catch { }

            ToastService.ClearScheduled();
            try { ToastService.RescheduleNudges(); } catch { }

            // CAT_TELEMETRY: the denominator every funnel is measured against. Inert
            // until a connection string is configured AND the user has seen the notice.
            try
            {
                TelemetryService.TrackAppLaunch(
                    AimTrainerStorage.LoadAll().Count(r => !r.IsAssessmentSession),
                    TrialService.IsFullVersion());
            }
            catch { }
        }

        // Rebuild the scheduled win-back queue as the app closes — so nudges reflect the
        // latest state and, crucially, are queued with the OS to fire while CAT is closed.
        protected override void OnExit(ExitEventArgs e)
        {
            try { ToastService.RescheduleNudges(); } catch { }
            // The in-memory channel batches on a ~30s timer, so an un-flushed exit loses
            // the whole session's events. OnExit is best-effort (it does not run on a
            // kill or crash), which is acceptable: a lost session is a gap in the data,
            // never a broken app.
            try { TelemetryService.Shutdown(); } catch { }
            base.OnExit(e);
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
