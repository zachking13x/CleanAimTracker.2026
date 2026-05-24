using System;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Windows toast notification service.
    /// Uses Windows.UI.Notifications (WinRT) via net8.0-windows10.0.19041.0.
    /// Uses global:: prefix throughout to avoid collision with CleanAimTracker.Windows namespace.
    /// </summary>
    public static class ToastService
    {
        // NOTE: for MSIX-packaged apps the system already knows the app identity.
        // The CreateToastNotifier() overload with no argument must be used — passing
        // an explicit AppId string here fails silently and drops all notifications.

        // ── Public API ────────────────────────────────────────────────

        /// <summary>
        /// Shows a re-engagement toast if the user hasn't trained today
        /// and their last aim trainer session was ≥ 20 hours ago.
        /// Silent if the user has no sessions yet or has already trained today.
        /// </summary>
        public static void CheckAndNotify()
        {
            try
            {
                var sessions = SessionStorage.LoadAll();
                if (sessions.Count == 0) return;

                var last = sessions.OrderByDescending(s => s.Timestamp).FirstOrDefault();
                if (last == null) return;

                if (last.Timestamp.Date == DateTime.Today) return;
                if ((DateTime.Now - last.Timestamp).TotalHours < 20) return;

                // Schedule for 30 minutes from now so it fires after the user closes the app,
                // not immediately while they are looking at the screen.
                var deliveryTime = DateTime.Now.AddMinutes(30);
                ScheduleToast(
                    "Time to train 🎯",
                    $"Last session was {DayText(last.Timestamp)} (quality: {last.OverallQualityScore:F0}). " +
                    "A quick session keeps your streak alive.",
                    deliveryTime);
            }
            catch { }
        }

        /// <summary>
        /// Context-aware reminder scheduled for tomorrow at the same hour (clamped 10 am–9 pm).
        /// Picks the most motivating message based on the user's actual state:
        /// streak urgency → pending challenge → new user coaching → accuracy reference.
        /// </summary>
        public static void ScheduleTomorrowReminder()
        {
            try
            {
                int hour         = Math.Clamp(DateTime.Now.Hour, 10, 21);
                var deliveryTime = DateTime.Now.Date.AddDays(1).AddHours(hour);

                var settings     = SettingsService.Load();
                int currentStreak = StreakService.GetStreakInfo().current; // always fresh, not stale from settings load
                var allDrills    = AimTrainerStorage.LoadAll();
                var lastDrill    = allDrills.OrderByDescending(r => r.Timestamp).FirstOrDefault();

                string title;
                string body;

                // Priority 1: meaningful streak at risk (≥3 days)
                if (currentStreak >= 3)
                {
                    title = "Your streak is on the line 🔥";
                    body  = $"Day {currentStreak} streak — train today to keep it alive.";
                }
                // Priority 2: daily challenge hasn't been completed
                else if (settings.LastChallengeDate.Date < DateTime.Today)
                {
                    title = "Daily challenge waiting 🎯";
                    body  = "A new challenge is ready. Complete it before midnight to stay consistent.";
                }
                // Priority 3: new user (fewer than 3 sessions)
                else if (allDrills.Count < 3)
                {
                    int n = allDrills.Count;
                    title = "Build the habit early 💪";
                    body  = $"Only {n} session{(n == 1 ? "" : "s")} in. Three sessions builds a real trend.";
                }
                // Priority 4: reference last accuracy
                else if (lastDrill != null)
                {
                    double acc = lastDrill.Accuracy;
                    if (acc >= 85)
                    {
                        title = $"You hit {acc:F0}% accuracy 🔥";
                        body  = "That's elite territory. Can you beat it today?";
                    }
                    else if (acc >= 70)
                    {
                        title = "Time to sharpen that aim 🎯";
                        body  = $"You were at {acc:F0}% last session. 5 minutes today compounds over time.";
                    }
                    else
                    {
                        title = "Time to train 🎯";
                        body  = "Consistency matters more than perfection. Jump in for 5 minutes.";
                    }
                }
                else
                {
                    title = "Time to train 🎯";
                    body  = "You set a reminder yesterday. 3 sessions builds a real trend — today's the day.";
                }

                ScheduleToast(title, body, deliveryTime);
            }
            catch { }
        }

        /// <summary>
        /// Schedules an 8 pm "streak at risk" warning if the user hasn't trained today
        /// and has built a streak of ≥ 2 days. Called on app startup.
        /// </summary>
        public static void ScheduleStreakAtRiskIfNeeded()
        {
            try
            {
                var settings = SettingsService.Load();
                if (settings.CurrentStreak < 2) return;

                // Already trained today — no warning needed
                var lastDrill = AimTrainerStorage.LoadLast();
                if (lastDrill != null && lastDrill.Timestamp.Date == DateTime.Today) return;

                // Already past 8 pm — too late to schedule for tonight
                if (DateTime.Now.Hour >= 20) return;

                var deliveryTime = DateTime.Today.AddHours(20); // 8 pm tonight
                int s = settings.CurrentStreak;
                ScheduleToast(
                    $"{s}-day streak at risk 🔥",
                    $"You've trained {s} days in a row. Train before midnight or the streak resets — even one quick session counts.",
                    deliveryTime);
            }
            catch { }
        }

        /// <summary>
        /// Shows an immediate re-engagement toast when the user has been absent ≥ 3 days.
        /// Marks <c>ReEngagementNotificationSent</c> so it only fires once per absence gap.
        /// Reset the flag (to false) after each new session so it can fire again next time.
        /// </summary>
        public static void CheckAndScheduleReEngagement()
        {
            try
            {
                var settings  = SettingsService.Load();
                if (settings.ReEngagementNotificationSent) return;

                var allDrills = AimTrainerStorage.LoadAll();
                if (allDrills.Count == 0) return;

                var lastDrill = allDrills.OrderByDescending(r => r.Timestamp).FirstOrDefault();
                if (lastDrill == null) return;

                int daysSince = (int)(DateTime.Today - lastDrill.Timestamp.Date).TotalDays;
                if (daysSince < 3) return;

                string title;
                string body;

                if (daysSince >= 7)
                {
                    title = "You've been away a week 👀";
                    body  = "Muscle memory fades fast. Even one session gets you back on track.";
                }
                else if (daysSince >= 5)
                {
                    title = "5 days since your last drill 😬";
                    body  = "Your aim is a skill — use it or lose it. Come back for 5 minutes.";
                }
                else
                {
                    title = "3-day gap detected 🎯";
                    body  = "Aim consistency breaks down quickly. A quick session now sets you straight.";
                }

                ShowToast(title, body);

                settings.ReEngagementNotificationSent = true;
                SettingsService.Save(settings);
            }
            catch { }
        }

        /// <summary>
        /// Schedules a weekly summary toast for Sunday at 7 pm.
        /// Only fires once per Sunday and only when ≥ 2 sessions were completed this week.
        /// </summary>
        public static void ScheduleWeeklySummaryIfNeeded()
        {
            try
            {
                if (DateTime.Today.DayOfWeek != DayOfWeek.Sunday) return;

                var settings = SettingsService.Load();
                if (settings.LastWeeklySummaryDate.Date == DateTime.Today) return;

                // Already past 7 pm — skip until next week
                if (DateTime.Now.Hour >= 19) return;

                var allDrills    = AimTrainerStorage.LoadAll();
                var weekStart    = DateTime.Today.AddDays(-6);
                var weekSessions = allDrills.Where(r => r.Timestamp.Date >= weekStart).ToList();
                if (weekSessions.Count < 2) return;

                double avgAcc  = weekSessions.Average(r => r.Accuracy);
                double bestAcc = weekSessions.Max(r => r.Accuracy);
                string body    = $"{weekSessions.Count} sessions this week · avg accuracy {avgAcc:F0}% · peak {bestAcc:F0}%";

                ScheduleToast("Your week in review 📊", body, DateTime.Today.AddHours(19));

                settings.LastWeeklySummaryDate = DateTime.Today;
                SettingsService.Save(settings);
            }
            catch { }
        }

        // ── Private helpers ───────────────────────────────────────────

        private static void ScheduleToast(string title, string body, DateTime deliveryTime)
        {
            var toastXml = global::Windows.UI.Notifications.ToastNotificationManager
                .GetTemplateContent(global::Windows.UI.Notifications.ToastTemplateType.ToastText02);

            var nodes = toastXml.GetElementsByTagName("text");
            nodes[0].AppendChild(toastXml.CreateTextNode(title));
            nodes[1].AppendChild(toastXml.CreateTextNode(body));

            var scheduled = new global::Windows.UI.Notifications.ScheduledToastNotification(
                toastXml, new DateTimeOffset(deliveryTime));

            global::Windows.UI.Notifications.ToastNotificationManager
                .CreateToastNotifier()
                .AddToSchedule(scheduled);
        }

        private static void ShowToast(string title, string body)
        {
            var toastXml = global::Windows.UI.Notifications.ToastNotificationManager
                .GetTemplateContent(global::Windows.UI.Notifications.ToastTemplateType.ToastText02);

            var nodes = toastXml.GetElementsByTagName("text");
            nodes[0].AppendChild(toastXml.CreateTextNode(title));
            nodes[1].AppendChild(toastXml.CreateTextNode(body));

            var toast = new global::Windows.UI.Notifications.ToastNotification(toastXml);
            global::Windows.UI.Notifications.ToastNotificationManager
                .CreateToastNotifier()
                .Show(toast);
        }

        private static string DayText(DateTime date)
            => date.Date == DateTime.Today.AddDays(-1)
                ? "yesterday"
                : $"{(int)(DateTime.Now - date).TotalDays} days ago";
    }
}
