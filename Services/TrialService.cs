using CleanAimTracker.Windows;
using System;

namespace CleanAimTracker.Services
{
    public static class TrialService
    {
        private const int TrialDays = 7;

        public static void Initialize()
        {
            var settings = SettingsService.Load();
            if (settings.FirstLaunchDate == DateTime.MinValue ||
                settings.FirstLaunchDate == default)
            {
                settings.FirstLaunchDate = DateTime.UtcNow;
                SettingsService.Save(settings);
                LogService.Info("Trial started");
            }
        }

        public static bool IsTrialActive()
        {
            if (IsFullVersion()) return true;
            var settings = SettingsService.Load();
            if (settings.FirstLaunchDate == DateTime.MinValue) return true;
            return (DateTime.UtcNow - settings.FirstLaunchDate).TotalDays <= TrialDays;
        }

        public static int DaysRemaining()
        {
            var settings = SettingsService.Load();
            if (settings.FirstLaunchDate == DateTime.MinValue) return TrialDays;
            int remaining = TrialDays - (int)(DateTime.UtcNow - settings.FirstLaunchDate).TotalDays;
            return Math.Max(0, remaining);
        }

        public static bool IsFullVersion()
        {
            // Dev override for testing
            string? devFlag = Environment.GetEnvironmentVariable("CLEANAIMTRACKER_FULLVERSION");
            if (devFlag == "1") return true;

            // Store license check
            return LicenseService.HasPro
                || LicenseService.HasTrainer
                || LicenseService.HasLifetime;
        }

        public static bool CanAccessProFeature()
            => IsFullVersion() || IsTrialActive();

        public static bool RequestProAccess(string featureName)
        {
            if (CanAccessProFeature()) return true;

            // FIXED: Show() takes 0 arguments
            UpgradeDialog.Show();
            return false;
        }

        public static string GetStatusText()
        {
            if (IsFullVersion()) return "Pro";
            int days = DaysRemaining();
            if (days <= 0) return "Trial Expired";
            return $"Trial – {days} day{(days == 1 ? "" : "s")} left";
        }

        public static string GetBannerText()
        {
            if (IsFullVersion()) return "";
            int days = DaysRemaining();
            if (days <= 0) return "⚠ Trial expired — Upgrade to unlock all features";
            if (days <= 2) return $"⚠ Trial ends in {days} day{(days == 1 ? "" : "s")} — Upgrade now";
            return $"Free Trial — {days} day{(days == 1 ? "" : "s")} remaining";
        }

        public static string GetStoreLink()
        {
            // FIXED: Real Store Product ID
            return "ms-windows-store://pdp/?productid=9MVBDZBQ01DM";
        }
    }
}