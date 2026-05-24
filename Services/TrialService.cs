using CleanAimTracker.Windows;
using System;

namespace CleanAimTracker.Services
{
    public static class TrialService
    {
        // TASK-13: 30 free sessions instead of 7-day timer
        private const int FreeSessions = 30;

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

        // TASK-13: Active while session count ≤ 30 (or user is licensed)
        public static bool IsTrialActive()
        {
            if (IsFullVersion()) return true;
            return SessionsCompleted() < FreeSessions;
        }

        // Number of sessions the user has completed
        public static int SessionsCompleted()
        {
            try { return SessionStorage.LoadAll().Count; }
            catch { return 0; }
        }

        public static int SessionsRemaining()
        {
            int remaining = FreeSessions - SessionsCompleted();
            return Math.Max(0, remaining);
        }

        public static bool IsFullVersion()
        {
            return LicenseService.HasPro
                || LicenseService.HasTrainer
                || LicenseService.HasLifetime;
        }

        public static bool CanAccessProFeature()
            => IsFullVersion() || IsTrialActive();

        public static bool RequestProAccess(string featureName)
        {
            if (CanAccessProFeature()) return true;

            UpgradeDialog.Show(featureName);
            return false;
        }

        public static string GetStatusText()
        {
            if (IsFullVersion()) return "Pro";
            int remaining = SessionsRemaining();
            if (remaining <= 0) return "Free limit reached";
            return $"Free — {remaining} session{(remaining == 1 ? "" : "s")} left";
        }

        // TASK-12: Only show banner after at least one session is completed
        public static string GetBannerText()
        {
            if (IsFullVersion()) return "";

            int completed = SessionsCompleted();
            if (completed == 0) return "";      // TASK-12: suppress until first session done

            int remaining = SessionsRemaining();
            if (remaining <= 0) return $"🎯 {completed} sessions done — ready for Pro?";
            if (remaining <= 5) return $"⚡ {remaining} free session{(remaining == 1 ? "" : "s")} left";
            return $"Free — {remaining} sessions remaining";
        }

        /// <summary>True when the free trial has been fully used up.</summary>
        public static bool IsAtFreeLimit()
            => !IsFullVersion() && SessionsRemaining() <= 0;

        // TASK-11: Returns true if this session count is a value-moment milestone
        public static bool IsValueMoment(int sessionCount)
        {
            return sessionCount == 3 || sessionCount == 10 || sessionCount == 25;
        }

        public static string GetValueMomentMessage(int sessionCount)
        {
            return sessionCount switch
            {
                3  => "You've completed 3 sessions! Pro unlocks AI coaching, full history, and export.",
                10 => $"10 sessions in — you're building real habits! You have {FreeSessions - 10} free sessions left. After that, Pro keeps your history, AI coaching, and trends going.",
                25 => $"25 sessions! You're seriously committed. {FreeSessions - 25} free sessions left — Pro gives you unlimited sessions, AI coaching, and full trend history.",
                _  => ""
            };
        }

        public static string GetStoreLink()
        {
            return "ms-windows-store://pdp/?productid=9MVBDZBQ01DM";
        }
    }
}
