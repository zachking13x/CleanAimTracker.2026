using System;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Manages the 7-day free trial period.
    /// Records first launch date and checks remaining days.
    /// When published to the Microsoft Store, replace this with
    /// Windows.Services.Store.StoreContext for real license checks.
    /// </summary>
    public static class TrialService
    {
        private const int TrialDays = 7;

        /// <summary>Initialize trial tracking on first launch.</summary>
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

        /// <summary>Returns true if the trial is still active.</summary>
        public static bool IsTrialActive()
        {
            var settings = SettingsService.Load();
            if (settings.FirstLaunchDate == DateTime.MinValue) return true;
            return (DateTime.Now - settings.FirstLaunchDate).TotalDays <= TrialDays;
        }

        /// <summary>Returns the number of days remaining in the trial.</summary>
        public static int DaysRemaining()
        {
            var settings = SettingsService.Load();
            if (settings.FirstLaunchDate == DateTime.MinValue) return TrialDays;
            int remaining = TrialDays - (int)(DateTime.Now - settings.FirstLaunchDate).TotalDays;
            return Math.Max(0, remaining);
        }

        /// <summary>Returns true if the user has purchased the full version.</summary>
        public static bool IsFullVersion()
        {
            // TODO: Replace with actual Store license check when publishing.
            // For now, always returns false (trial mode).
            // When integrating with Microsoft Store:
            //   var context = StoreContext.GetDefault();
            //   var license = await context.GetAppLicenseAsync();
            //   return license.IsActive && !license.IsTrial;
            return false;
        }

        /// <summary>Returns the trial status text for the UI banner.</summary>
        public static string GetStatusText()
        {
            if (IsFullVersion())
                return "Full Version";

            int days = DaysRemaining();
            if (days <= 0)
                return "Trial Expired - Upgrade to continue";

            return $"Free Trial - {days} day{(days == 1 ? "" : "s")} remaining";
        }

        /// <summary>
        /// Returns the Microsoft Store link for upgrading.
        /// Replace with your actual Store product ID after publishing.
        /// </summary>
        public static string GetStoreLink()
        {
            // TODO: Replace XXXXX with your actual Store Product ID
            return "ms-windows-store://pdp/?productid=XXXXX";
        }
    }
}
