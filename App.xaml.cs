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
        }
    }
}
