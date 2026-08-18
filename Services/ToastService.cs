using CleanAimTracker.Models;
using System;
using System.Linq;
using WinToast = global::Windows.UI.Notifications;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// CAT_RETENTION_NOTIFICATIONS: the win-back channel. Uses WinRT
    /// <see cref="WinToast.ScheduledToastNotification"/> so toasts fire even when CAT is
    /// CLOSED — that's the entire point of a retention nudge. The schedule is re-built on
    /// every app close (so it reflects current streak/challenge state) and CLEARED on
    /// launch (so a stale nudge never fires after the user has already returned). Copy
    /// comes from <see cref="NotificationMessages"/> — authored, data-backed, no chores.
    ///
    /// For MSIX-packaged apps the system already knows the app identity; the no-argument
    /// CreateToastNotifier() overload must be used (an explicit AppId drops everything).
    /// </summary>
    public static class ToastService
    {
        // ScheduledToastNotification.Id is capped at 16 chars by WinRT — keep these short.
        private const string IdPrefix    = "catn";
        private const string IdStreak    = "catn_streak";
        private const string IdDaily     = "catn_daily";
        private const string IdWinback2  = "catn_wb2";
        private const string IdWinback4  = "catn_wb4";
        private const string IdFirstDrill1 = "catn_fd1";
        private const string IdFirstDrill2 = "catn_fd2";

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>True only when the OS currently permits CAT toasts (T1.3 honesty:
        /// detect a denied state instead of firing into a void).</summary>
        public static bool OsAllowsToasts()
        {
            try
            {
                return WinToast.ToastNotificationManager.CreateToastNotifier().Setting
                       == WinToast.NotificationSetting.Enabled;
            }
            catch { return false; }
        }

        /// <summary>Remove every CAT-scheduled toast still pending. Called on launch
        /// (the user is here — stale nudges shouldn't fire) and before each reschedule.</summary>
        public static void ClearScheduled()
        {
            try
            {
                var notifier = WinToast.ToastNotificationManager.CreateToastNotifier();
                foreach (var s in notifier.GetScheduledToastNotifications().ToList())
                    if (s.Id != null && s.Id.StartsWith(IdPrefix, StringComparison.Ordinal))
                        notifier.RemoveFromSchedule(s);
            }
            catch { }
        }

        /// <summary>
        /// Rebuild the pending nudge queue from current state. Call on app CLOSE and on
        /// session end. Schedules at most: a streak-at-risk warning tonight (loss
        /// aversion), a daily nudge tomorrow, and win-back nudges at +2 and +4 days.
        /// Never more than ~1/day. No-ops (and clears) if the user disabled notifications
        /// or the OS has them off.
        /// </summary>
        public static void RescheduleNudges()
        {
            try
            {
                ClearScheduled();

                // CAT_NUDGE_CLOBBER: start a fresh attribution buffer for this pass. Every
                // ScheduleToast below fills it; the single Save at the end commits it.
                _pendingAttribution.Clear();

                var settings = SettingsService.Load();
                if (!settings.NotificationsEnabled || !OsAllowsToasts())
                {
                    // Nothing will be scheduled, so nothing can be attributed. Persist the
                    // empty map rather than leaving stale entries pointing at toasts that
                    // were just cleared and never replaced.
                    if (settings != null && settings.ScheduledNudgeTimes.Count > 0)
                    {
                        settings.ScheduledNudgeTimes.Clear();
                        SettingsService.Save(settings);
                    }
                    return;   // don't pretend the channel works when it can't deliver
                }

                var ctx  = BuildContext(settings, out bool trainedToday, out DateTime? lastTs);

                int seed = settings.NotificationVariantSeed;
                var now  = DateTime.Now;
                int hour = Math.Clamp(settings.PreferredNudgeHour, 10, 21);

                // CAT_RETENTION_HOLES (2026-07-30): opened-but-never-trained users used to
                // hit `if (!ctx.HasAnySession) return;` and receive NOTHING — the highest
                // churn-risk group was the one we never nudged. They have no real numbers,
                // so they get the outcome-led first-drill nudge instead of the data-backed
                // pools, at +1 and +3 days. Then we're done: two asks, never nagging.
                if (!ctx.HasAnySession)
                {
                    var fd1 = NotificationMessages.BuildFirstDrillNudge(seed);
                    ScheduleToast(fd1.Title, fd1.Body, now.Date.AddDays(1).AddHours(hour), IdFirstDrill1);
                    var fd2 = NotificationMessages.BuildFirstDrillNudge(seed + 1);
                    ScheduleToast(fd2.Title, fd2.Body, now.Date.AddDays(3).AddHours(hour), IdFirstDrill2);

                    settings.NotificationVariantSeed = seed + 1;
                    CommitAttribution(settings);       // CAT_NUDGE_CLOBBER
                    SettingsService.Save(settings);
                    return;
                }

                // 1) Streak at risk TONIGHT — strongest, time-sensitive return driver.
                if (ctx.CurrentStreak >= 2 && !trainedToday && now.Hour < 22)
                {
                    var fire = now.Date.AddHours(20);                 // 8 pm tonight
                    if (fire <= now.AddMinutes(2)) fire = now.AddMinutes(2); // already evening → soon, still today
                    if (fire.Date == now.Date)
                    {
                        var copy = NotificationMessages.BuildDailyNudge(ctx, seed); // leads with streak when live
                        ScheduleToast(copy.Title, copy.Body, fire, IdStreak);
                    }
                }

                // 2) Daily nudge TOMORROW (no streak claim — it may reset at midnight).
                var tomorrow = now.Date.AddDays(1).AddHours(hour);
                var dailyCtx = Clone(ctx); dailyCtx.CurrentStreak = 0;
                var daily = NotificationMessages.BuildDailyNudge(dailyCtx, seed);
                ScheduleToast(daily.Title, daily.Body, tomorrow, IdDaily);

                // 3) Win-back at +2 and +4 days (curiosity). Cleared on launch if they return.
                var wb = NotificationMessages.BuildWinback(ctx, seed);
                ScheduleToast(wb.Title, wb.Body, now.Date.AddDays(2).AddHours(hour), IdWinback2);
                var wb4 = NotificationMessages.BuildWinback(ctx, seed + 1);  // different variant
                ScheduleToast(wb4.Title, wb4.Body, now.Date.AddDays(4).AddHours(hour), IdWinback4);

                // Advance the rotation so the next reschedule reads differently.
                settings.NotificationVariantSeed = seed + 1;
                if (lastTs is DateTime t) settings.PreferredNudgeHour = Math.Clamp(t.Hour, 10, 21);
                CommitAttribution(settings);           // CAT_NUDGE_CLOBBER
                SettingsService.Save(settings);
            }
            catch { }
        }

        /// <summary>Test/diagnostic hook: schedule a single nudge a few seconds out so the
        /// "fires when closed" acceptance can be verified on a real machine.</summary>
        public static void ScheduleDiagnosticToast(int secondsFromNow)
        {
            try
            {
                // NB: id is OUTSIDE the "catn" prefix so RescheduleNudges' clear-on-close
                // won't wipe it — it must survive the app closing to prove the channel.
                ScheduleToast("CAT notification test 🎯",
                    "If you can read this with the app closed, the win-back channel works.",
                    DateTime.Now.AddSeconds(Math.Max(5, secondsFromNow)), "diag_test");
            }
            catch { }
        }

        // ── Context ─────────────────────────────────────────────────────────────
        private static NudgeContext BuildContext(UserSettings settings, out bool trainedToday, out DateTime? lastTs)
        {
            trainedToday = false;
            lastTs = null;
            var ctx = new NudgeContext();

            var all = AimTrainerStorage.LoadAll().OrderByDescending(r => r.Timestamp).ToList();
            ctx.SessionCount = all.Count;
            ctx.HasAnySession = all.Count > 0;
            if (all.Count == 0) return ctx;

            var last = all[0];
            lastTs = last.Timestamp;
            trainedToday = last.Timestamp.Date == DateTime.Today;

            ctx.LastAccuracy = last.Accuracy;
            ctx.LastScenario = last.Scenario;
            ctx.LastScore    = last.Score;
            ctx.CurrentStreak = StreakService.GetStreakInfo().current;
            ctx.ChallengeDoneToday = settings.LastChallengeDate.Date == DateTime.Today;
            ctx.HasProfileUpdate = all.Count >= 5;

            var sameScenario = all.Where(r => r.Scenario == last.Scenario).ToList();
            ctx.ScenarioBestScore = sameScenario.Count > 0 ? sameScenario.Max(r => r.Score) : 0;

            // overshoot improvement = previous same-scenario overshoot − latest (positive = dropped).
            // V2 gate: only compare directional readings against directional readings —
            // the V1→V2 metric change alone would read as a huge fake "improvement".
            var overs = sameScenario.Where(r => r.ClickMetricVersion >= 2 && r.OvershootPct >= 0)
                                    .Take(2).ToList();
            if (overs.Count == 2)
            {
                double delta = overs[1].OvershootPct - overs[0].OvershootPct;
                if (delta > 0) ctx.OvershootDeltaPct = delta;
            }
            return ctx;
        }

        private static NudgeContext Clone(NudgeContext c) => new()
        {
            HasAnySession = c.HasAnySession, SessionCount = c.SessionCount, CurrentStreak = c.CurrentStreak,
            LastAccuracy = c.LastAccuracy, LastScenario = c.LastScenario, LastScore = c.LastScore,
            ScenarioBestScore = c.ScenarioBestScore, OvershootDeltaPct = c.OvershootDeltaPct,
            HasProfileUpdate = c.HasProfileUpdate, ChallengeDoneToday = c.ChallengeDoneToday,
            ChallengeText = c.ChallengeText
        };

        // ── WinRT scheduling ─────────────────────────────────────────────────────
        private static void ScheduleToast(string title, string body, DateTime when, string id)
        {
            if (when <= DateTime.Now) return;   // never schedule in the past

            var xml = WinToast.ToastNotificationManager
                .GetTemplateContent(WinToast.ToastTemplateType.ToastText02);
            var nodes = xml.GetElementsByTagName("text");
            nodes[0].AppendChild(xml.CreateTextNode(title));
            nodes[1].AppendChild(xml.CreateTextNode(body));

            // Default activation: tapping the toast launches/foregrounds the app (to the
            // main screen). Screen-specific deep-linking needs a COM toast activator,
            // which doesn't register reliably for the sideloaded package — deferred.
            var scheduled = new WinToast.ScheduledToastNotification(xml, new DateTimeOffset(when)) { Id = id };
            WinToast.ToastNotificationManager.CreateToastNotifier().AddToSchedule(scheduled);

            RecordScheduledForAttribution(id, when);
        }

        // ── CAT_TELEMETRY: nudge attribution ─────────────────────────────────────
        // There is no true click signal here. Deep-linked toast activation needs a
        // registered COM activator, which doesn't work reliably for this package (see
        // the note in ScheduleToast), so a tapped toast is indistinguishable from any
        // other launch. What we CAN observe: which nudges actually fired (a fired toast
        // leaves the pending-schedule list) and whether the app opened soon after. That
        // is a correlation, and it is reported as one — the event is called
        // launch_after_nudge, not notification_clicked, and carries attribution=inferred.

        /// <summary>How long after a nudge fires a launch is still plausibly attributable.</summary>
        private static readonly TimeSpan AttributionWindow = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Attribution entries accumulated during the CURRENT RescheduleNudges pass.
        ///
        /// CAT_NUDGE_CLOBBER (2026-08-16): this used to Load→mutate→Save settings on every
        /// scheduled toast. RescheduleNudges loads settings ONCE at the top, schedules
        /// (each write landing via its own load/save), then saves its own copy at the end —
        /// a copy whose ScheduledNudgeTimes was still empty from before the scheduling ran.
        /// The final save wiped every entry, deterministically, on every launch, for every
        /// user. That's why `launch_after_nudge` had never once fired in production and
        /// there was no signal on whether notifications bring anyone back.
        ///
        /// Buffering here and writing ONCE, as part of the same save that persists the
        /// variant seed, removes the conflicting write entirely.
        /// </summary>
        private static readonly Dictionary<string, DateTime> _pendingAttribution = new();

        /// <summary>Fold this pass's scheduled toasts onto the settings about to be saved.</summary>
        private static void CommitAttribution(UserSettings settings)
        {
            settings.ScheduledNudgeTimes.Clear();
            foreach (var kv in _pendingAttribution)
                settings.ScheduledNudgeTimes[kv.Key] = kv.Value;
        }

        private static void RecordScheduledForAttribution(string id, DateTime when)
            => _pendingAttribution[id] = when;

        /// <summary>
        /// Call ONCE on launch, BEFORE <see cref="ClearScheduled"/> — it needs the
        /// still-pending list to work out which nudges already fired. Reports at most
        /// one event (the most recent qualifying nudge) and then clears the map, so a
        /// single fired nudge can never be credited with two launches.
        /// </summary>
        public static void ReportNudgeAttribution()
        {
            try
            {
                var s = SettingsService.Load();
                if (s.ScheduledNudgeTimes.Count == 0) return;

                var pending = new HashSet<string>(StringComparer.Ordinal);
                try
                {
                    foreach (var t in WinToast.ToastNotificationManager
                                              .CreateToastNotifier()
                                              .GetScheduledToastNotifications())
                        if (t.Id != null) pending.Add(t.Id);
                }
                catch { /* an unreadable schedule means "assume nothing fired" */ }

                var now = DateTime.Now;
                string?  bestId   = null;
                DateTime bestTime = DateTime.MinValue;

                foreach (var kv in s.ScheduledNudgeTimes)
                {
                    if (pending.Contains(kv.Key)) continue;              // still queued — never fired
                    if (kv.Value > now) continue;                        // not due; removed some other way
                    if (now - kv.Value > AttributionWindow) continue;    // too long ago to credit
                    if (kv.Value > bestTime) { bestTime = kv.Value; bestId = kv.Key; }
                }

                if (bestId != null)
                    TelemetryService.TrackLaunchAfterNudge(
                        NudgeKind(bestId), (int)(now - bestTime).TotalMinutes);

                s.ScheduledNudgeTimes.Clear();
                SettingsService.Save(s);
            }
            catch { }
        }

        /// <summary>Toast id → a stable, non-identifying kind name for the event.</summary>
        private static string NudgeKind(string id) => id switch
        {
            IdStreak       => "streak_at_risk",
            IdDaily        => "daily",
            IdWinback2     => "winback_2d",
            IdWinback4     => "winback_4d",
            IdFirstDrill1  => "first_drill_1d",
            IdFirstDrill2  => "first_drill_3d",
            _              => "other",
        };
    }
}
