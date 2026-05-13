using System;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// TASK-17: Windows toast notifications for re-engagement.
    /// Uses Windows.UI.Notifications (WinRT) available on net8.0-windows10.0.19041.0.
    /// Called from App.xaml.cs on startup.
    /// </summary>
    public static class ToastService
    {
        private const string AppId = "CleanAimTracker";

        /// <summary>
        /// Shows a re-engagement toast if the user hasn't trained today
        /// and their last session was ≥ 20 hours ago.
        /// Silent if the user has no sessions yet or has already trained today.
        /// </summary>
        public static void CheckAndNotify()
        {
            try
            {
                var sessions = SessionStorage.LoadAll();
                if (sessions.Count == 0) return;          // first-timer — don't spam

                var last = sessions.OrderByDescending(s => s.Timestamp).FirstOrDefault();
                if (last == null) return;

                // Already trained today → no toast
                if (last.Timestamp.Date == DateTime.Today) return;

                // Less than 20 hours ago → too soon
                if ((DateTime.Now - last.Timestamp).TotalHours < 20) return;

                ShowReEngagementToast(last.Timestamp, last.OverallQualityScore);
            }
            catch
            {
                // Silently swallow — toast is non-critical
            }
        }

        /// <summary>
        /// Schedules a Windows toast for tomorrow at the same hour the user is training now.
        /// Called when the user opts in via the "See You Tomorrow" prompt on Close.
        /// </summary>
        public static void ScheduleTomorrowReminder()
        {
            try
            {
                // Deliver at the same hour tomorrow, clamped to a polite window (10 am – 9 pm)
                int hour         = Math.Clamp(DateTime.Now.Hour, 10, 21);
                var deliveryTime = new DateTimeOffset(DateTime.Now.Date.AddDays(1).AddHours(hour));

                string title   = "Time to train 🎯";
                string message = "You set a reminder yesterday. 3 sessions builds a real trend — today's the day.";

                var toastXml = global::Windows.UI.Notifications.ToastNotificationManager
                    .GetTemplateContent(global::Windows.UI.Notifications.ToastTemplateType.ToastText02);

                var textNodes = toastXml.GetElementsByTagName("text");
                textNodes[0].AppendChild(toastXml.CreateTextNode(title));
                textNodes[1].AppendChild(toastXml.CreateTextNode(message));

                var scheduled = new global::Windows.UI.Notifications.ScheduledToastNotification(
                    toastXml, deliveryTime);

                global::Windows.UI.Notifications.ToastNotificationManager
                    .CreateToastNotifier(AppId)
                    .AddToSchedule(scheduled);
            }
            catch
            {
                // Silently swallow — non-critical
            }
        }

        private static void ShowReEngagementToast(DateTime lastDate, double lastQuality)
        {
            try
            {
                string dayText = lastDate.Date == DateTime.Today.AddDays(-1)
                    ? "yesterday"
                    : $"{(int)(DateTime.Now - lastDate).TotalDays} days ago";

                string title   = "Time to train 🎯";
                string message = $"Last session was {dayText} (quality: {lastQuality:F0}). " +
                                 "A quick session keeps your streak alive.";

                // Use global:: to avoid collision with CleanAimTracker.Windows namespace
                var toastXml = global::Windows.UI.Notifications.ToastNotificationManager
                    .GetTemplateContent(global::Windows.UI.Notifications.ToastTemplateType.ToastText02);

                var textNodes = toastXml.GetElementsByTagName("text");
                textNodes[0].AppendChild(toastXml.CreateTextNode(title));
                textNodes[1].AppendChild(toastXml.CreateTextNode(message));

                var toast    = new global::Windows.UI.Notifications.ToastNotification(toastXml);
                var notifier = global::Windows.UI.Notifications.ToastNotificationManager
                    .CreateToastNotifier(AppId);

                notifier.Show(toast);
            }
            catch
            {
                // Toast not supported in this environment — ignore
            }
        }
    }
}
