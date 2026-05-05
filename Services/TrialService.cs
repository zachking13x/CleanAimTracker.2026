using System;
using System.IO;
using System.Windows;
using CleanAimTracker.Windows;


namespace CleanAimTracker.Services
{
    /// <summary>
    /// Handles the 7‑day free trial and Pro feature gating.
    /// </summary>
    public static class TrialService
    {
        private const int TrialDays = 7;
        private static readonly string LicenseFile = "license.dat";

        /// <summary>
        /// Called on app startup. Ensures FirstLaunchDate is set.
        /// </summary>
        public static void Initialize()
        {
            var settings = SettingsService.Load();

            if (settings.FirstLaunchDate == DateTime.MinValue)
            {
                settings.FirstLaunchDate = DateTime.Now;
                SettingsService.Save(settings);
                LogService.Info("Trial started");
            }
        }

        /// <summary>
        /// Returns true if the user has purchased the full version.
        /// </summary>
        public static bool IsFullVersion()
        {
            // Later replaced with StoreContext license check.
            return File.Exists(LicenseFile);
        }

        /// <summary>
        /// Returns true if the trial is still active OR full version is unlocked.
        /// </summary>
        public static bool IsTrialActive()
        {
            if (IsFullVersion()) return true;

            var settings = SettingsService.Load();
            if (settings.FirstLaunchDate == DateTime.MinValue)
                return true;

            double daysUsed = (DateTime.Now - settings.FirstLaunchDate).TotalDays;
            return daysUsed < TrialDays;
        }

        /// <summary>
        /// Returns how many days remain in the trial.
        /// </summary>
        public static int DaysRemaining()
        {
            if (IsFullVersion()) return 0;

            var settings = SettingsService.Load();
            if (settings.FirstLaunchDate == DateTime.MinValue)
                return TrialDays;

            int remaining = TrialDays - (int)(DateTime.Now - settings.FirstLaunchDate).TotalDays;
            return Math.Max(0, remaining);
        }

        /// <summary>
        /// Returns the banner text shown in MainWindow.
        /// </summary>
        public static string GetBannerText()
        {
            if (IsFullVersion())
                return ""; // Hide banner entirely

            int days = DaysRemaining();

            if (days <= 0)
                return "Trial Expired — Upgrade Required";

            return $"Free Trial — {days} day{(days == 1 ? "" : "s")} remaining";
        }
        public static string GetStoreLink()
        {
            // Your real Microsoft Store Product ID
            return "ms-windows-store://pdp/?productid=9MVBDZBQ01DM";
        }


        /// <summary>
        /// Called before opening any Pro feature window.
        /// Returns true if access is allowed.
        /// </summary>
        public static bool RequestProAccess(string featureName)
        {
            // Full version → always allowed
            if (IsFullVersion()) return true;

            // Trial still active → allowed
            if (IsTrialActive()) return true;

            // Trial expired → block + show upgrade dialog
            MessageBox.Show(
                $"{featureName} is a Pro feature.\nYour trial has expired.",
                "Upgrade Required",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            UpgradeDialog.Show();
            return false;
        }
    }
}
