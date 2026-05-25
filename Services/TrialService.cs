using CleanAimTracker.Windows;
using System;

namespace CleanAimTracker.Services
{
    public static class TrialService
    {
        // TASK-13: 30 free sessions instead of 7-day timer
        private const int FreeSessions = 30;

        // Developer bypass — grants full Pro access on the developer's own machine.
        // Environment.UserName is evaluated once at startup; no effect on any other user.
        private static readonly bool IsDeveloper =
            Environment.UserName.Equals("Zachk", StringComparison.OrdinalIgnoreCase);

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

        // Number of aim trainer drills the user has completed.
        // NOTE: SessionStorage holds raw mouse-movement tracker sessions (gameplay).
        //       AimTrainerStorage holds aim trainer drill results — these are separate
        //       stores and must not be interchanged. Trial progress is based on drills.
        public static int SessionsCompleted()
        {
            try { return AimTrainerStorage.LoadAll().Count; }
            catch { return 0; }
        }

        public static int SessionsRemaining()
        {
            int remaining = FreeSessions - SessionsCompleted();
            return Math.Max(0, remaining);
        }

        public static bool IsFullVersion()
        {
            if (IsDeveloper) return true;
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
